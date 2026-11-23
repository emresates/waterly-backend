using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Auth;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.Models;
using Waterly.Api.Services;
using Waterly.Api.Settings;

namespace Waterly.Api.Tests.Infrastructure;

// Builds the real services by hand (no web server) on top of the real test database and a fake clock.
public sealed class ServiceTestContext : IAsyncDisposable
{
    public AppDbContext Db { get; }
    public FakeClock Clock { get; }
    public AuthService Auth { get; }
    public UserService Users { get; }
    public GoalService Goals { get; }
    public HydrationService Hydration { get; }
    public StatisticsService Statistics { get; }
    public HealthService Health { get; }

    public ServiceTestContext(DatabaseFixture database, DateTimeOffset now)
    {
        Db = database.CreateDbContext();
        Clock = new FakeClock(now);

        var jwtOptions = Options.Create(new JwtSettings
        {
            Issuer = "test",
            Audience = "test",
            SigningKey = "test-signing-key-that-is-long-enough-123456"
        });
        var passwordHasher = new PasswordHasher<User>();

        Users = new UserService(Db, passwordHasher, Clock);
        Auth = new AuthService(Db, new TokenService(jwtOptions, Clock), passwordHasher, jwtOptions, Clock, NullLogger<AuthService>.Instance);
        Goals = new GoalService(Db, Users, Clock);
        Hydration = new HydrationService(Db, Goals, Users, Clock);
        Statistics = new StatisticsService(Db, Hydration, Goals, Users, Clock);
        Health = new HealthService(Db, Goals, Users, Clock);
    }

    public async Task<Guid> RegisterUserAsync(string timeZone = "Europe/Istanbul")
    {
        var response = await Auth.RegisterAsync(new RegisterRequest
        {
            Name = "Test User",
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Passw0rd!",
            TimeZone = timeZone
        }, CancellationToken.None);

        return response.User.Id;
    }

    public Task<IntakeResultResponse> AddIntakeAsync(Guid userId, int amountMl, DateTimeOffset? consumedAt = null, string? timeZone = null) =>
        Hydration.AddIntakeAsync(userId, new AddIntakeRequest
        {
            AmountMl = amountMl,
            ConsumedAt = consumedAt,
            ClientEventId = Guid.NewGuid(),
            TimeZone = timeZone
        }, CancellationToken.None);

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
