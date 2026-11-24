using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Waterly.Api.Controllers;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Exceptions;
using Waterly.Api.Interfaces;
using Waterly.Api.Middleware;
using Waterly.Api.Models;
using Waterly.Api.Services;
using Waterly.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration (secrets come from user-secrets in development, environment variables in production)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured. See README.md.");
}

var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();
if (Encoding.UTF8.GetByteCount(jwtSettings.SigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes long. See README.md.");
}
builder.Services.Configure<JwtSettings>(jwtSection);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimitPerMinute", 10);
var generalPermitLimit = builder.Configuration.GetValue("RateLimiting:GeneralPermitLimitPerMinute", 120);
var achievementEventPermitLimit = builder.Configuration.GetValue("RateLimiting:AchievementEventsPermitLimitPerMinute", 30);

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IHydrationService, HydrationService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();

// ---------------------------------------------------------------------------
// Controllers, JSON and validation responses
// ---------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Data annotation errors are returned in the same ApiResponse format as every other error.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key.TrimStart('$', '.')),
                    entry => entry.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Geçersiz değer." : e.ErrorMessage).ToArray());

            var response = ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed,
                "Gönderilen bilgiler geçersiz.", errors);
            return new BadRequestObjectResult(response);
        };
    });

// ---------------------------------------------------------------------------
// Authentication & authorization (JWT access token in the "Authorization: Bearer ..." header)
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as they are in the token ("sub", "email") instead of long .NET claim URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ExceptionMiddleware.WriteErrorAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized, "Bu işlem için giriş yapmalısınız.");
            },
            OnForbidden = context => ExceptionMiddleware.WriteErrorAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                ErrorCodes.Forbidden, "Bu işlem için yetkiniz yok.")
        };
    });
builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// CORS (only needed for browser clients such as Expo Web; native apps are not affected by CORS)
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ---------------------------------------------------------------------------
// Rate limiting
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) => new ValueTask(ExceptionMiddleware.WriteErrorAsync(context.HttpContext,
        StatusCodes.Status429TooManyRequests, ErrorCodes.TooManyRequests, "Çok fazla istek gönderildi. Lütfen biraz bekleyip tekrar deneyin."));

    // Login / register / refresh: limited per IP address.
    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));

    // Achievement events: limited per signed-in user, so a client cannot flood the event table.
    options.AddPolicy(RateLimitPolicies.AchievementEvents, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = achievementEventPermitLimit, Window = TimeSpan.FromMinutes(1) }));

    // Every request: limited per signed-in user (or per IP address for anonymous requests).
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = generalPermitLimit, Window = TimeSpan.FromMinutes(1) }));
});

// ---------------------------------------------------------------------------
// Health check (GET /health) and Swagger
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Waterly API",
        Version = "v1",
        Description = "Su içme takip uygulaması için REST API."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Login yanıtındaki accessToken değerini girin (\"Bearer \" yazmadan)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// ===========================================================================
// HTTP pipeline (order matters)
// ===========================================================================
var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Waterly API");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() })
        });
    }
}).DisableRateLimiting();

var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(port))
{
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}

// Makes the auto-generated Program class visible to the integration tests.
public partial class Program;
