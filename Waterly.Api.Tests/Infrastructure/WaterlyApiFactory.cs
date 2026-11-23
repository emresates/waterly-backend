using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Waterly.Api.DTOs.Auth;
using Waterly.Api.DTOs.Responses;

namespace Waterly.Api.Tests.Infrastructure;

// Runs the whole API in memory (real middleware, real JWT, real PostgreSQL) for HTTP level tests.
public sealed class WaterlyApiFactory(DatabaseFixture database) : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", database.ConnectionString);
        builder.UseSetting("Jwt:Issuer", "Waterly.Tests");
        builder.UseSetting("Jwt:Audience", "Waterly.Tests");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("RateLimiting:AuthPermitLimitPerMinute", "10000");
        builder.UseSetting("RateLimiting:GeneralPermitLimitPerMinute", "10000");
    }

    public async Task<AuthResponse> RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Api User",
            email = $"{Guid.NewGuid():N}@example.com",
            password = "Passw0rd!"
        });
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<AuthResponse>(response)).Data!;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public static async Task<ApiResponse<T>> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions))!;
}
