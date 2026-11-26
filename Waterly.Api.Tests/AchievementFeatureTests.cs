using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Achievements;
using Waterly.Api.DTOs.Goals;
using Waterly.Api.DTOs.Health;
using Waterly.Api.DTOs.Preferences;
using Waterly.Api.DTOs.Reminders;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Models;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

// Features that some achievements depend on: preferences / themes, easter egg, comeback tracking,
// and a completeness check that every achievement in the catalog can really be unlocked.
[Collection(DatabaseCollection.Name)]
public partial class AchievementFeatureTests(DatabaseFixture database) : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero); // 13:00 Istanbul
    private readonly WaterlyApiFactory _factory = new(database);

    // ------------------------------------------------------------------ completeness

    [Fact]
    public async Task EveryAchievementInTheCatalog_CanActuallyBeUnlocked()
    {
        // 2023-01-01 00:00 UTC = 03:00 in Istanbul. Registration at 06:00 local.
        var day0 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = new ServiceTestContext(database, day0.AddHours(3));
        var userId = await context.RegisterUserAsync();

        context.Clock.Now = day0.AddHours(3).AddMinutes(1);
        await context.Goals.UpdateCurrentGoalAsync(userId, new UpdateGoalRequest { TargetMl = 10000 }, CancellationToken.None);

        // 1000 consecutive days, then 15 inactive days, then 3 comeback days.
        // Every day: 08:00 (3400 ml), 12:30 (3300 ml) and 19:00 from a reminder notification (3300 ml) = goal of 10000 ml.
        await context.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO "WaterIntakes" ("Id", "UserId", "AmountMl", "ConsumedAt", "LocalDate", "TimeZone", "ClientEventId", "CreatedAt", "UpdatedAt", "Source")
            SELECT gen_random_uuid(), {userId}, s.amount, t.ts, (t.ts AT TIME ZONE 'Europe/Istanbul')::date, 'Europe/Istanbul',
                   gen_random_uuid(), t.ts, t.ts, s.source
            FROM generate_series(0, 1017) AS d(n)
            CROSS JOIN (VALUES (300, 3400, 'Manual'), (570, 3300, 'Manual'), (960, 3300, 'ReminderNotification')) AS s(minutes, amount, source)
            CROSS JOIN LATERAL (SELECT {day0} + make_interval(days => d.n, mins => s.minutes) AS ts) AS t
            WHERE d.n < 1000 OR d.n >= 1015
            """);

        // Weekly report in 5 different ISO weeks.
        for (var week = 0; week < 5; week++)
        {
            context.Clock.Now = day0.AddDays(1 + 7 * week).AddHours(10);
            await RecordAsync(context, userId, AchievementEventTypes.WeeklyReportOpened);
        }

        // "Today" = the last comeback day, 21:00 local.
        context.Clock.Now = day0.AddDays(1017).AddHours(18);
        var today = DateTimeHelper.GetToday(context.Clock, "Europe/Istanbul");

        foreach (var eventType in new[]
                 {
                     AchievementEventTypes.AppOpened, AchievementEventTypes.OnboardingCompleted, AchievementEventTypes.StatisticsOpened,
                     AchievementEventTypes.HistoryOpened, AchievementEventTypes.MonthlyReportOpened,
                     AchievementEventTypes.HealthRecommendationViewed, AchievementEventTypes.BottleEasterEggDiscovered
                 })
        {
            await RecordAsync(context, userId, eventType);
        }
        await RecordAsync(context, userId, AchievementEventTypes.CalendarDayViewed, today.AddDays(-3).ToString("yyyy-MM-dd"));

        // Dark mode + three different bottle themes, selected through the preferences API.
        await SavePreferencesAsync(context, userId, ColorScheme.Dark, "BOTTLE_CLASSIC");
        await SavePreferencesAsync(context, userId, ColorScheme.Dark, "BOTTLE_OCEAN");
        await SavePreferencesAsync(context, userId, ColorScheme.Dark, "BOTTLE_MINT");

        await context.Reminders.UpdateSettingsAsync(userId, new UpdateReminderSettingsRequest
        {
            Enabled = true, IntervalMinutes = 60, StartTime = "08:00", EndTime = "21:00",
            TimeZone = "Europe/Istanbul", DefaultGlassMl = 330, SoundEnabled = true, VibrationEnabled = false
        }, CancellationToken.None);

        await context.Health.SaveActivityAsync(userId, new SaveActivityRequest
        {
            Source = ActivitySource.AppleHealth,
            Days = [new ActivityDayRequest { Date = today, Steps = 8000, ExerciseMinutes = 20 }]
        }, CancellationToken.None);

        await context.AddIntakeAsync(userId, 250, timeZone: "Asia/Tokyo");
        await context.Achievements.EvaluateAchievementsAsync(userId, CancellationToken.None);

        var unlocked = await context.Db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.Achievement.Code)
            .ToListAsync();
        var missing = AchievementCatalog.All.Select(a => a.Code).Except(unlocked).ToList();
        Assert.True(missing.Count == 0, $"Unreachable achievements: {string.Join(", ", missing)}");
        Assert.Equal(76, unlocked.Count);

        var progress = await context.Achievements.GetUserProgressAsync(userId, CancellationToken.None);
        Assert.Equal(AchievementCatalog.All.Sum(a => a.XpReward), progress.TotalXp);
        Assert.Equal(100, progress.Level);
        Assert.True(progress.IsMaxLevel);

        // Every reward is earned as well.
        Assert.Equal(RewardCatalog.All.Count, await context.Db.UserRewards.CountAsync(ur => ur.UserId == userId));
    }

    [Fact]
    public void CompatibilityMatrix_ListsEveryAchievementExactlyOnce()
    {
        var matrix = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "ACHIEVEMENT_MATRIX.md"));
        var codesInMatrix = MatrixRowRegex().Matches(matrix).Select(m => m.Groups[1].Value).ToList();

        Assert.Equal(76, codesInMatrix.Count);
        Assert.Equal(AchievementCatalog.All.Select(a => a.Code).Order(), codesInMatrix.Order());
    }

    // ------------------------------------------------------------------ preferences & themes

    [Fact]
    public async Task Preferences_HaveDefaults_AndArePersistedOnTheAccount()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var defaults = await context.Preferences.GetPreferencesAsync(userId, CancellationToken.None);
        Assert.Equal(ColorScheme.System, defaults.ColorScheme);
        Assert.Equal("BOTTLE_CLASSIC", defaults.BottleTheme.Code);
        Assert.Null(defaults.UpdatedAt);
        Assert.Null(defaults.ProfileFrame);

        await SavePreferencesAsync(context, userId, ColorScheme.Light, "BOTTLE_OCEAN");

        // A different DbContext = a new phone reading the same account.
        await using var newPhone = new ServiceTestContext(database, Now);
        var restored = await newPhone.Preferences.GetPreferencesAsync(userId, CancellationToken.None);
        Assert.Equal(ColorScheme.Light, restored.ColorScheme);
        Assert.Equal("BOTTLE_OCEAN", restored.BottleTheme.Code);
        Assert.Equal("bottle_ocean", restored.BottleTheme.AssetKey);
        Assert.True(restored.BottleTheme.IsActive);
        Assert.NotNull(restored.UpdatedAt);
    }

    [Fact]
    public async Task LockedWrongOrUnknownCosmetics_CannotBeSelected()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var locked = await Assert.ThrowsAsync<AppException>(() => SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_GOLD"));
        Assert.Equal((403, ErrorCodes.RewardLocked), (locked.StatusCode, locked.ErrorCode));

        var lockedFrame = await Assert.ThrowsAsync<AppException>(() =>
            SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_CLASSIC", profileFrame: "FRAME_AQUA"));
        Assert.Equal(ErrorCodes.RewardLocked, lockedFrame.ErrorCode);

        var wrongType = await Assert.ThrowsAsync<AppException>(() => SavePreferencesAsync(context, userId, ColorScheme.System, "FRAME_AQUA"));
        Assert.Equal((400, ErrorCodes.InvalidReward), (wrongType.StatusCode, wrongType.ErrorCode));

        var unknown = await Assert.ThrowsAsync<AppException>(() => SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_HACKED"));
        Assert.Equal((404, ErrorCodes.RewardNotFound), (unknown.StatusCode, unknown.ErrorCode));

        Assert.False(await context.Db.UserPreferences.AnyAsync(p => p.UserId == userId));
    }

    [Fact]
    public async Task LevelThemes_BecomeSelectableOnceEarned()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var before = await context.Preferences.GetCosmeticsAsync(userId, new CosmeticsQuery { Type = RewardType.BottleTheme }, CancellationToken.None);
        Assert.Equal(7, before.Count);
        Assert.Equal(["BOTTLE_CLASSIC", "BOTTLE_MINT", "BOTTLE_OCEAN"], before.Where(c => c.IsUnlocked).Select(c => c.Code).Order());
        Assert.All(before.Where(c => c.IsStarter), c => Assert.True(c.IsUnlocked));

        await context.GrantAchievementsAsync(userId, "STREAK_14", "GOAL_DAYS_30");   // 700 XP of earlier progress
        await context.AddIntakeAsync(userId, 2000);   // + 180 XP (+ secrets it triggers) -> level 8 -> BOTTLE_SKY_BLUE (level 5)

        var saved = await SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_SKY_BLUE");
        Assert.Equal("BOTTLE_SKY_BLUE", saved.BottleTheme.Code);
        Assert.NotNull(saved.BottleTheme.UnlockedAt);
    }

    [Fact]
    public async Task DarkMode_UnlocksSecretOnlyOnce()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var light = await SavePreferencesAsync(context, userId, ColorScheme.Light, "BOTTLE_CLASSIC");
        Assert.DoesNotContain(light.Gamification!.NewlyUnlocked, a => a.Code == "SECRET_DARK_MODE");

        var dark = await SavePreferencesAsync(context, userId, ColorScheme.Dark, "BOTTLE_CLASSIC");
        Assert.Contains(dark.Gamification!.NewlyUnlocked, a => a.Code == "SECRET_DARK_MODE");
        var xp = dark.Gamification.Progress.TotalXp;

        await SavePreferencesAsync(context, userId, ColorScheme.Light, "BOTTLE_CLASSIC");
        var darkAgain = await SavePreferencesAsync(context, userId, ColorScheme.Dark, "BOTTLE_CLASSIC");
        Assert.Empty(darkAgain.Gamification!.NewlyUnlocked);
        Assert.Equal(xp, darkAgain.Gamification.Progress.TotalXp);
        Assert.Equal(1, await context.Db.UserAchievementEvents.CountAsync(e => e.UserId == userId && e.EventType == AchievementEventTypes.DarkThemeEnabled));
    }

    [Fact]
    public async Task UsingThreeDifferentThemes_UnlocksThemesSecret_RepeatsDoNotCount()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        await SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_CLASSIC");
        await SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_CLASSIC");
        await SavePreferencesAsync(context, userId, ColorScheme.System, "bottle_ocean");   // codes are case-insensitive
        await SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_OCEAN");
        Assert.DoesNotContain("SECRET_THEMES_3", await UnlockedCodesAsync(context, userId));

        var third = await SavePreferencesAsync(context, userId, ColorScheme.System, "BOTTLE_MINT");
        Assert.Contains(third.Gamification!.NewlyUnlocked, a => a.Code == "SECRET_THEMES_3");
    }

    // ------------------------------------------------------------------ easter egg, comeback, activity

    [Fact]
    public async Task BottleEasterEgg_UnlocksOnce_AndCannotBeFarmed()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var first = await RecordAsync(context, userId, AchievementEventTypes.BottleEasterEggDiscovered);
        Assert.Contains(first.Gamification!.NewlyUnlocked, a => a.Code == "SECRET_BOTTLE_TAPS_10");
        var xp = first.Gamification.Progress.TotalXp;

        for (var day = 1; day <= 3; day++)
        {
            context.Clock.Now = Now.AddDays(day);
            Assert.Equal(AchievementEventStatus.AlreadyCounted, (await RecordAsync(context, userId, AchievementEventTypes.BottleEasterEggDiscovered)).Status);
        }

        Assert.Equal(xp, (await context.Achievements.GetUserProgressAsync(userId, CancellationToken.None)).TotalXp);
        Assert.Equal(1, await context.Db.UserAchievementEvents.CountAsync(e => e.UserId == userId && e.EventType == AchievementEventTypes.BottleEasterEggDiscovered));
    }

    [Fact]
    public async Task LastActiveDay_IsTracked_AndSessionsAndAppOpensCountForComeback()
    {
        var start = Now.AddDays(-20);
        await using var context = new ServiceTestContext(database, start);
        var userId = await context.RegisterUserAsync();
        Assert.Equal(DateOnly.FromDateTime(start.UtcDateTime), (await context.Achievements.GetSummaryAsync(userId, CancellationToken.None)).LastActiveDate);

        // 15 days later the user only opens the app (no water): this is a return after 14 full inactive days.
        context.Clock.Now = start.AddDays(16);
        await RecordAsync(context, userId, AchievementEventTypes.AppOpened);
        Assert.Contains("SECRET_RETURN_14_DAYS", await UnlockedCodesAsync(context, userId));

        // Opening the app many times on the same day is still one active day.
        await RecordAsync(context, userId, AchievementEventTypes.AppOpened);
        await RecordAsync(context, userId, AchievementEventTypes.AppOpened);
        context.Clock.Now = start.AddDays(17);
        await context.Achievements.TryRecordSessionAsync(userId, alwaysEvaluate: false, CancellationToken.None);   // token refresh
        Assert.DoesNotContain("SECRET_COMEBACK_3_DAYS", await UnlockedCodesAsync(context, userId));

        // Missing a day in between is fine: the comeback counts distinct active days.
        context.Clock.Now = start.AddDays(19);
        await RecordAsync(context, userId, AchievementEventTypes.AppOpened);
        Assert.Contains("SECRET_COMEBACK_3_DAYS", await UnlockedCodesAsync(context, userId));

        var summary = await context.Achievements.GetSummaryAsync(userId, CancellationToken.None);
        Assert.Equal(DateOnly.FromDateTime(start.AddDays(19).UtcDateTime), summary.LastActiveDate);
    }

    // ------------------------------------------------------------------ HTTP contract

    [Fact]
    public async Task PreferencesAndCosmeticsEndpoints_FollowTheApiContract()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var get = await WaterlyApiFactory.ReadAsync<PreferencesResponse>(await client.GetAsync("/api/users/me/preferences"));
        Assert.Equal("BOTTLE_CLASSIC", get.Data!.BottleTheme.Code);

        var put = await client.PutAsJsonAsync("/api/users/me/preferences", new { colorScheme = "Dark", bottleTheme = "BOTTLE_MINT" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = (await WaterlyApiFactory.ReadAsync<PreferencesResponse>(put)).Data!;
        Assert.Equal(ColorScheme.Dark, saved.ColorScheme);
        Assert.Contains(saved.Gamification!.NewlyUnlocked, a => a.Code == "SECRET_DARK_MODE");

        var locked = await client.PutAsJsonAsync("/api/users/me/preferences", new { colorScheme = "Dark", bottleTheme = "BOTTLE_MYTHIC" });
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Equal(ErrorCodes.RewardLocked, (await WaterlyApiFactory.ReadAsync<object>(locked)).ErrorCode);

        var missingTheme = await client.PutAsJsonAsync("/api/users/me/preferences", new { colorScheme = "Dark" });
        Assert.Equal(HttpStatusCode.BadRequest, missingTheme.StatusCode);

        var cosmetics = (await WaterlyApiFactory.ReadAsync<List<CosmeticItemResponse>>(await client.GetAsync("/api/cosmetics"))).Data!;
        Assert.Equal(RewardCatalog.All.Count, cosmetics.Count);
        Assert.Equal("BOTTLE_MINT", Assert.Single(cosmetics, c => c.IsActive).Code);

        var frames = (await WaterlyApiFactory.ReadAsync<List<CosmeticItemResponse>>(await client.GetAsync("/api/cosmetics?type=ProfileFrame"))).Data!;
        Assert.All(frames, f => Assert.False(f.IsUnlocked));
    }

    // ------------------------------------------------------------------ helpers

    private static Task<PreferencesResponse> SavePreferencesAsync(ServiceTestContext context, Guid userId, ColorScheme colorScheme,
        string bottleTheme, string? profileFrame = null) =>
        context.Preferences.UpdatePreferencesAsync(userId, new UpdatePreferencesRequest
        {
            ColorScheme = colorScheme,
            BottleTheme = bottleTheme,
            ProfileFrame = profileFrame
        }, CancellationToken.None);

    private static Task<RecordAchievementEventResponse> RecordAsync(ServiceTestContext context, Guid userId, string eventType, string? value = null) =>
        context.Achievements.RecordEventAsync(userId, new RecordAchievementEventRequest
        {
            ClientEventId = Guid.NewGuid(),
            EventType = eventType,
            EventValue = value
        }, CancellationToken.None);

    private static async Task<List<string>> UnlockedCodesAsync(ServiceTestContext context, Guid userId) =>
        await context.Db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.Achievement.Code)
            .ToListAsync();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Waterly.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (Waterly.slnx) not found.");
    }

    // A matrix row starts with "| `CODE` |".
    [GeneratedRegex(@"^\|\s*`([A-Z0-9_]+)`\s*\|", RegexOptions.Multiline)]
    private static partial Regex MatrixRowRegex();

    public void Dispose() => _factory.Dispose();
}
