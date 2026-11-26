using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    public ReminderService Reminders { get; }
    public AchievementService Achievements { get; }
    public PreferenceService Preferences { get; }

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

        Achievements = new AchievementService(Db, Clock, NullLogger<AchievementService>.Instance);
        Users = new UserService(Db, passwordHasher, Clock);
        Auth = new AuthService(Db, new TokenService(jwtOptions, Clock), passwordHasher, jwtOptions, Achievements, Clock, NullLogger<AuthService>.Instance);
        Goals = new GoalService(Db, Users, Achievements, Clock);
        Hydration = new HydrationService(Db, Goals, Users, Achievements, Clock);
        Statistics = new StatisticsService(Db, Hydration, Goals, Users, Clock);
        Health = new HealthService(Db, Goals, Users, Achievements, Clock);
        Reminders = new ReminderService(Db, Users, Achievements, Clock);
        Preferences = new PreferenceService(Db, Achievements, Clock);
    }

    // Adds water "as it happens": moves the clock to the moment of drinking first, so the intake is not back-dated.
    public Task<IntakeResultResponse> DrinkAtAsync(Guid userId, int amountMl, DateTimeOffset moment, IntakeSource? source = null)
    {
        Clock.Now = moment;
        return Hydration.AddIntakeAsync(userId, new AddIntakeRequest
        {
            AmountMl = amountMl,
            ClientEventId = Guid.NewGuid(),
            Source = source
        }, CancellationToken.None);
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

    // Marks catalog achievements as earned without playing them out, to reach a level quickly.
    // XP, level and level rewards follow on the next evaluation, exactly as in production.
    public async Task GrantAchievementsAsync(Guid userId, params string[] codes)
    {
        var ids = await Db.Achievements.Where(a => codes.Contains(a.Code)).Select(a => a.Id).ToListAsync();
        Db.UserAchievements.AddRange(ids.Select(id => new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AchievementId = id,
            UnlockedAt = Clock.Now
        }));
        await Db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
