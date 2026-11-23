using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Achievements;
using Waterly.Api.DTOs.Auth;
using Waterly.Api.DTOs.Goals;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.DTOs.Reminders;
using Waterly.Api.DTOs.Users;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Models;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class AchievementTests(DatabaseFixture database)
{
    // 2026-11-23 10:00 UTC = 13:00 in Istanbul (UTC+3), a Monday.
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ catalog & seed

    [Fact]
    public async Task Seed_Contains76AchievementsWithUniqueCodes_AndAllRewards()
    {
        await using var db = database.CreateDbContext();

        Assert.Equal(76, AchievementCatalog.All.Count);
        Assert.Equal(76, await db.Achievements.CountAsync());
        Assert.Equal(76, AchievementCatalog.All.Select(a => a.Code).Distinct().Count());
        Assert.Equal(76, AchievementCatalog.All.Select(a => a.Id).Distinct().Count());
        Assert.Equal(76, await db.Achievements.Select(a => a.Code).Distinct().CountAsync());

        var perCategory = (await db.Achievements.Select(a => a.Category).ToListAsync())
            .GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(10, perCategory[AchievementCategory.GettingStarted]);
        Assert.Equal(14, perCategory[AchievementCategory.Streak]);
        Assert.Equal(12, perCategory[AchievementCategory.WaterVolume]);
        Assert.Equal(10, perCategory[AchievementCategory.GlassCollection]);
        Assert.Equal(10, perCategory[AchievementCategory.DailyHabits]);
        Assert.Equal(10, perCategory[AchievementCategory.Discovery]);
        Assert.Equal(10, perCategory[AchievementCategory.Secret]);

        // Only the Secret category is hidden.
        Assert.All(AchievementCatalog.All, a => Assert.Equal(a.Category == AchievementCategory.Secret, a.IsSecret));

        var firstGoal = await db.Achievements.SingleAsync(a => a.Code == "FIRST_GOAL");
        Assert.Equal((50, AchievementRarity.Rare), (firstGoal.XpReward, firstGoal.Rarity));
        var yearStreak = await db.Achievements.SingleAsync(a => a.Code == "STREAK_365");
        Assert.Equal((10000, AchievementRarity.Mythic, 365), (yearStreak.XpReward, yearStreak.Rarity, yearStreak.TargetValue));

        Assert.Equal(RewardCatalog.All.Count, await db.Rewards.CountAsync());
        var codes = AchievementCatalog.All.Select(a => a.Code).ToHashSet();
        Assert.All(RewardCatalog.All.Where(r => r.RequiredAchievementCode is not null),
            r => Assert.Contains(r.RequiredAchievementCode!, codes));
    }

    // ------------------------------------------------------------------ levels

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(404, 9)]
    [InlineData(405, 10)]
    [InlineData(12004, 49)]
    [InlineData(12005, 50)]
    [InlineData(49004, 99)]
    [InlineData(49005, 100)]
    [InlineData(1_000_000, 100)]
    public void Level_FollowsTheThresholdFormula(int totalXp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, LevelCalculator.LevelFor(totalXp));
    }

    [Fact]
    public void LevelResponse_ContainsEverythingTheAppNeeds()
    {
        var level = LevelCalculator.Build(800);
        Assert.Equal(13, level.Level);
        Assert.Equal(720, level.CurrentLevelXp);   // 5 * 12^2
        Assert.Equal(845, level.NextLevelXp);      // 5 * 13^2
        Assert.Equal(80, level.XpIntoLevel);
        Assert.Equal(45, level.XpToNextLevel);
        Assert.Equal(64.0m, level.ProgressPercentage);
        Assert.False(level.IsMaxLevel);

        var max = LevelCalculator.Build(60000);
        Assert.Equal(100, max.Level);
        Assert.Equal(60000, max.TotalXp);          // XP keeps growing at the cap
        Assert.Null(max.NextLevelXp);
        Assert.Equal(0, max.XpToNextLevel);
        Assert.Equal(100m, max.ProgressPercentage);
        Assert.True(max.IsMaxLevel);
    }

    // ------------------------------------------------------------------ first water

    [Fact]
    public async Task FirstGlassOnRegistrationDay_UnlocksSeveralAchievementsAtOnce()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        Assert.Equal(["WELCOME"], await UnlockedCodesAsync(context, userId));

        var result = await context.AddIntakeAsync(userId, 250);

        var gamification = result.Gamification!;
        Assert.Equal(["FIRST_DROP", "FIRST_GLASS", "GLASSES_1"], gamification.NewlyUnlocked.Select(a => a.Code).Order());
        Assert.All(gamification.NewlyUnlocked, a => Assert.True(a.IsUnlocked));

        // WELCOME 10 + FIRST_DROP 10 + FIRST_GLASS 15 + GLASSES_1 10
        Assert.Equal(45, gamification.Progress.TotalXp);
        Assert.Equal(4, gamification.Progress.Level);
        Assert.Equal(2, gamification.PreviousLevel);
        Assert.True(gamification.LeveledUp);
        await AssertXpIsConsistentAsync(context, userId, 45);
    }

    [Fact]
    public async Task SameClientEventIdTwice_DoesNotGiveXpTwice()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        var request = new AddIntakeRequest { AmountMl = 250, ClientEventId = Guid.NewGuid() };

        var first = await context.Hydration.AddIntakeAsync(userId, request, CancellationToken.None);
        var second = await context.Hydration.AddIntakeAsync(userId, request, CancellationToken.None);

        Assert.NotEmpty(first.Gamification!.NewlyUnlocked);
        Assert.True(second.AlreadyProcessed);
        Assert.Empty(second.Gamification!.NewlyUnlocked);
        Assert.Equal(first.Gamification.Progress.TotalXp, second.Gamification.Progress.TotalXp);
        await AssertXpIsConsistentAsync(context, userId, 45);
    }

    [Fact]
    public async Task EvaluatingAgain_NeverUnlocksTheSameAchievementTwice()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        await context.AddIntakeAsync(userId, 2000);

        var again = await context.Achievements.EvaluateAchievementsAsync(userId, CancellationToken.None);
        var andAgain = await context.Achievements.EvaluateAchievementsAsync(userId, CancellationToken.None);

        Assert.Empty(again.NewlyUnlocked);
        Assert.Empty(andAgain.NewlyUnlocked);
        var rows = await context.Db.UserAchievements.AsNoTracking().Where(ua => ua.UserId == userId).Select(ua => ua.AchievementId).ToListAsync();
        Assert.Equal(rows.Count, rows.Distinct().Count());

        // The database refuses a second row for the same achievement no matter how it is attempted.
        await using var other = database.CreateDbContext();
        other.UserAchievements.Add(new UserAchievement { Id = Guid.NewGuid(), UserId = userId, AchievementId = rows[0], UnlockedAt = Now });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        Assert.True(DbExceptionHelper.IsUniqueViolation(exception));
    }

    // ------------------------------------------------------------------ anti-farming rules

    [Fact]
    public async Task GlassEquivalents_Are250MlOfTheCountedTotal_RoundedDown()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        await context.AddIntakeAsync(userId, 240);
        Assert.DoesNotContain("GLASSES_1", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.AddIntakeAsync(userId, 10);   // 250 ml
        Assert.Contains("GLASSES_1", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.AddIntakeAsync(userId, 990);  // 1240 ml -> 4 glasses
        var glasses5 = await GetAchievementAsync(context, userId, "GLASSES_5");
        Assert.Equal(4, glasses5.CurrentValue);
        Assert.Equal(80m, glasses5.ProgressPercentage);
        Assert.False(glasses5.IsUnlocked);
    }

    [Fact]
    public async Task WaterAboveTheDailyGoal_DoesNotSpeedUpVolumeAchievements()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        await context.AddIntakeAsync(userId, 5000);   // goal is 2000

        var unlocked = await UnlockedCodesAsync(context, userId);
        Assert.Contains("TOTAL_ML_1000", unlocked);
        Assert.DoesNotContain("TOTAL_ML_5000", unlocked);
        Assert.DoesNotContain("GLASSES_10", unlocked);
        Assert.Equal(2000, (await GetAchievementAsync(context, userId, "TOTAL_ML_5000")).CurrentValue);
        Assert.Equal(8, (await GetAchievementAsync(context, userId, "GLASSES_10")).CurrentValue);

        // The real consumption is untouched.
        var today = await context.Hydration.GetTodayAsync(userId, CancellationToken.None);
        Assert.Equal(5000, today.Progress.ConsumedMl);
    }

    [Fact]
    public async Task BackDatedWater_DoesNotCountForAchievements_ButShortOfflineDelaysDo()
    {
        await using var context = new ServiceTestContext(database, Now.AddDays(-10));
        var userId = await context.RegisterUserAsync();
        context.Clock.Now = Now;

        await context.AddIntakeAsync(userId, 2000, Now.AddDays(-5));   // typed in 5 days later
        Assert.DoesNotContain("FIRST_GOAL", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.AddIntakeAsync(userId, 2000, Now.AddDays(-2));   // offline queue synced 2 days later
        Assert.Contains("FIRST_GOAL", (await UnlockedCodesAsync(context, userId)).ToHashSet());
    }

    [Fact]
    public async Task EditingAndDeletingWater_RecalculatesProgress_ButKeepsBadgesAndXp()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        var added = await context.AddIntakeAsync(userId, 500);
        Assert.Equal(2, (await GetAchievementAsync(context, userId, "GLASSES_5")).CurrentValue);

        await context.Hydration.UpdateIntakeAsync(userId, added.Intake.Id,
            new UpdateIntakeRequest { AmountMl = 1000, ConsumedAt = Now }, CancellationToken.None);
        Assert.Equal(4, (await GetAchievementAsync(context, userId, "GLASSES_5")).CurrentValue);
        var xpAfterEdit = await TotalXpAsync(context, userId);

        await context.Hydration.DeleteIntakeAsync(userId, added.Intake.Id, CancellationToken.None);
        Assert.Equal(0, (await GetAchievementAsync(context, userId, "GLASSES_5")).CurrentValue);
        Assert.Contains("GLASSES_1", await UnlockedCodesAsync(context, userId));   // not taken back
        Assert.Equal(xpAfterEdit, await TotalXpAsync(context, userId));

        // Adding the same water again earns nothing new.
        var readded = await context.AddIntakeAsync(userId, 1000);
        Assert.Empty(readded.Gamification!.NewlyUnlocked);
        await AssertXpIsConsistentAsync(context, userId, xpAfterEdit);
    }

    // ------------------------------------------------------------------ streaks & goals

    [Fact]
    public async Task Streak_UnlocksEveryLevelUpToTheLongestRun_AndUnfinishedTodayDoesNotReset()
    {
        var start = Now.AddDays(-10);
        await using var context = new ServiceTestContext(database, start);
        var userId = await context.RegisterUserAsync();

        for (var day = 0; day < 7; day++)
        {
            await context.DrinkAtAsync(userId, 2000, start.AddDays(day));
        }

        var unlocked = await UnlockedCodesAsync(context, userId);
        Assert.Superset(new HashSet<string> { "STREAK_2", "STREAK_3", "STREAK_7", "FIRST_GOAL" }, unlocked.ToHashSet());
        Assert.DoesNotContain("STREAK_10", unlocked);
        Assert.DoesNotContain("SECOND_CHANCE", unlocked);
        Assert.Contains("MASCOT_SUNGLASSES", await RewardCodesAsync(context, userId));   // reward for STREAK_7

        // Day 8 morning, nothing drunk yet: the current streak is still 7.
        context.Clock.Now = start.AddDays(7).AddHours(-4);
        Assert.Equal(7, (await GetAchievementAsync(context, userId, "STREAK_10")).CurrentValue);

        // Two full days missed: current streak drops, earned streak badges stay.
        context.Clock.Now = start.AddDays(9);
        Assert.Equal(0, (await GetAchievementAsync(context, userId, "STREAK_10")).CurrentValue);
        Assert.Contains("STREAK_7", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        // Completing the goal again after the break.
        var comeback = await context.DrinkAtAsync(userId, 2000, start.AddDays(9));
        Assert.Contains("SECOND_CHANCE", comeback.Gamification!.NewlyUnlocked.Select(a => a.Code));
    }

    [Fact]
    public async Task OnlyARealGoalChange_UnlocksGoalCustomized()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        await context.Goals.UpdateCurrentGoalAsync(userId, new UpdateGoalRequest { TargetMl = 2000 }, CancellationToken.None);
        Assert.DoesNotContain("GOAL_CUSTOMIZED", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.Goals.UpdateCurrentGoalAsync(userId, new UpdateGoalRequest { TargetMl = 1500 }, CancellationToken.None);
        Assert.Contains("GOAL_CUSTOMIZED", (await UnlockedCodesAsync(context, userId)).ToHashSet());
    }

    [Fact]
    public async Task LoweringTheGoal_UsesTheGoalOfEachDay()
    {
        await using var context = new ServiceTestContext(database, Now.AddDays(-1));
        var userId = await context.RegisterUserAsync();
        await context.DrinkAtAsync(userId, 1500, Now.AddDays(-1));   // yesterday: 1500 / 2000

        context.Clock.Now = Now;
        await context.Goals.UpdateCurrentGoalAsync(userId, new UpdateGoalRequest { TargetMl = 1500 }, CancellationToken.None);
        Assert.DoesNotContain("FIRST_GOAL", await UnlockedCodesAsync(context, userId));   // yesterday keeps its 2000 goal

        await context.DrinkAtAsync(userId, 1500, Now);                // today: 1500 / 1500
        Assert.Contains("FIRST_GOAL", (await UnlockedCodesAsync(context, userId)).ToHashSet());
    }

    // ------------------------------------------------------------------ habits, reminders, settings

    [Fact]
    public async Task DailyHabits_UseLocalTimeWindows_AndIgnoreTinySipsAndLateNight()
    {
        // 03:00 UTC = 06:00 in Istanbul.
        var dayStart = new DateTimeOffset(2026, 11, 23, 3, 0, 0, TimeSpan.Zero);
        await using var context = new ServiceTestContext(database, dayStart);
        var userId = await context.RegisterUserAsync();

        await context.DrinkAtAsync(userId, 50, dayStart.AddMinutes(30));     // 06:30, too small for habits
        await context.DrinkAtAsync(userId, 200, dayStart.AddHours(2));       // 08:00
        Assert.Contains("MORNING_FIRST", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.DrinkAtAsync(userId, 200, dayStart.AddHours(7));       // 13:00
        await context.DrinkAtAsync(userId, 200, dayStart.AddHours(17));      // 23:00, outside every window
        Assert.DoesNotContain("BALANCED_DAY_FIRST", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        var evening = await context.DrinkAtAsync(userId, 200, dayStart.AddHours(13));   // 19:00
        Assert.Contains("BALANCED_DAY_FIRST", evening.Gamification!.NewlyUnlocked.Select(a => a.Code));
    }

    [Fact]
    public async Task ReminderNotificationSource_IsStored_AndCountsForReminderAchievements()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var manual = await context.AddIntakeAsync(userId, 250);             // old client, no source
        Assert.Equal(IntakeSource.Manual, manual.Intake.Source);
        Assert.DoesNotContain("REMINDER_ACTION_FIRST", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        var fromReminder = await context.DrinkAtAsync(userId, 250, Now.AddMinutes(5), IntakeSource.ReminderNotification);
        Assert.Equal(IntakeSource.ReminderNotification, fromReminder.Intake.Source);
        Assert.Contains("REMINDER_ACTION_FIRST", fromReminder.Gamification!.NewlyUnlocked.Select(a => a.Code));
    }

    [Fact]
    public async Task ReminderSettings_UnlockConfiguredAndCustomizedAchievements()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        context.Clock.Now = Now.AddMinutes(1);

        var defaults = new UpdateReminderSettingsRequest
        {
            Enabled = true, IntervalMinutes = 120, StartTime = "09:00", EndTime = "22:00",
            TimeZone = "Europe/Istanbul", DefaultGlassMl = 250, SoundEnabled = true, VibrationEnabled = true
        };
        await context.Reminders.UpdateSettingsAsync(userId, defaults, CancellationToken.None);
        var unlocked = await UnlockedCodesAsync(context, userId);
        Assert.Contains("REMINDER_CONFIGURED", unlocked);
        Assert.DoesNotContain("REMINDER_CUSTOMIZED", unlocked);
        Assert.DoesNotContain("GLASS_SIZE_CUSTOMIZED", unlocked);

        defaults.IntervalMinutes = 60;
        defaults.DefaultGlassMl = 330;
        await context.Reminders.UpdateSettingsAsync(userId, defaults, CancellationToken.None);
        unlocked = await UnlockedCodesAsync(context, userId);
        Assert.Contains("REMINDER_CUSTOMIZED", unlocked);
        Assert.Contains("GLASS_SIZE_CUSTOMIZED", unlocked);
    }

    // ------------------------------------------------------------------ mobile events

    [Fact]
    public async Task MobileEvents_AreIdempotent_AndCountedOncePerPeriod()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        var eventId = Guid.NewGuid();

        var first = await RecordAsync(context, userId, AchievementEventTypes.StatisticsOpened, id: eventId);
        Assert.Equal(AchievementEventStatus.Recorded, first.Status);
        Assert.Contains("STATS_FIRST_VIEW", first.Gamification!.NewlyUnlocked.Select(a => a.Code));
        var xp = await TotalXpAsync(context, userId);

        var retry = await RecordAsync(context, userId, AchievementEventTypes.StatisticsOpened, id: eventId);
        Assert.Equal(AchievementEventStatus.AlreadyProcessed, retry.Status);

        var sameDay = await RecordAsync(context, userId, AchievementEventTypes.StatisticsOpened);
        Assert.Equal(AchievementEventStatus.AlreadyCounted, sameDay.Status);
        Assert.Equal(xp, await TotalXpAsync(context, userId));

        // Weekly reports count distinct ISO weeks, not openings.
        for (var week = 0; week < 5; week++)
        {
            context.Clock.Now = Now.AddDays(7 * week);
            Assert.Equal(AchievementEventStatus.Recorded, (await RecordAsync(context, userId, AchievementEventTypes.WeeklyReportOpened)).Status);
            Assert.Equal(AchievementEventStatus.AlreadyCounted, (await RecordAsync(context, userId, AchievementEventTypes.WeeklyReportOpened)).Status);

            var unlocked = await UnlockedCodesAsync(context, userId);
            Assert.Equal(week >= 3, unlocked.Contains("WEEKLY_REPORT_WEEKS_4"));
            Assert.Equal(week >= 4, unlocked.Contains("WEEKLY_REPORT_WEEKS_5"));
        }

        // The same theme never counts twice, whatever its casing.
        Assert.Equal(AchievementEventStatus.Recorded, (await RecordAsync(context, userId, AchievementEventTypes.BottleThemeUsed, "bottle_ocean")).Status);
        Assert.Equal(AchievementEventStatus.AlreadyCounted, (await RecordAsync(context, userId, AchievementEventTypes.BottleThemeUsed, "BOTTLE_OCEAN")).Status);
        await RecordAsync(context, userId, AchievementEventTypes.BottleThemeUsed, "bottle_mint");
        Assert.DoesNotContain("SECRET_THEMES_3", (await UnlockedCodesAsync(context, userId)).ToHashSet());
        await RecordAsync(context, userId, AchievementEventTypes.BottleThemeUsed, "bottle_classic");
        Assert.Contains("SECRET_THEMES_3", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await AssertXpIsConsistentAsync(context, userId);
    }

    [Theory]
    [InlineData("SESSION_STARTED", null)]          // server-only
    [InlineData("GIVE_ME_XP", null)]
    [InlineData("BOTTLE_THEME_USED", null)]        // theme required
    [InlineData("BOTTLE_THEME_USED", "<script>")]
    [InlineData("BOTTLE_THEME_USED", "bottle_gold")]   // exists, but not earned yet
    [InlineData("BOTTLE_THEME_USED", "my_custom_theme")] // not a real theme
    [InlineData("STATISTICS_OPENED", "extra")]     // no value allowed
    [InlineData("CALENDAR_DAY_VIEWED", "2026-11-23")] // must be a past day
    [InlineData("CALENDAR_DAY_VIEWED", "yesterday")]
    public async Task InvalidMobileEvents_AreRejected(string eventType, string? value)
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        var eventsBefore = await context.Db.UserAchievementEvents.CountAsync(e => e.UserId == userId);

        var exception = await Assert.ThrowsAsync<AppException>(() => RecordAsync(context, userId, eventType, value));
        Assert.Equal(ErrorCodes.InvalidAchievementEvent, exception.ErrorCode);
        Assert.Equal(eventsBefore, await context.Db.UserAchievementEvents.CountAsync(e => e.UserId == userId));
    }

    [Fact]
    public async Task CalendarDayViewed_AcceptsAPastDay()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var result = await RecordAsync(context, userId, AchievementEventTypes.CalendarDayViewed, "2026-11-20");

        Assert.Equal(AchievementEventStatus.Recorded, result.Status);
        Assert.Contains("CALENDAR_DAY_VIEW", result.Gamification!.NewlyUnlocked.Select(a => a.Code));
    }

    // ------------------------------------------------------------------ secrets

    [Fact]
    public async Task SecretFirst_UnlocksOnce_WithoutCountingItself()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var result = await RecordAsync(context, userId, AchievementEventTypes.DarkThemeEnabled);
        Assert.Equal(["SECRET_DARK_MODE", "SECRET_FIRST"], result.Gamification!.NewlyUnlocked.Select(a => a.Code).Order());

        for (var i = 0; i < 3; i++)
        {
            Assert.Empty((await context.Achievements.EvaluateAchievementsAsync(userId, CancellationToken.None)).NewlyUnlocked);
        }

        Assert.Equal(1, await context.Db.UserAchievements.CountAsync(ua => ua.UserId == userId && ua.Achievement.Code == "SECRET_FIRST"));
    }

    [Fact]
    public async Task LockedSecrets_AreHidden_UnlockedSecretsAreShown()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var locked = await context.Achievements.GetAchievementsAsync(userId,
            new AchievementQuery { Category = AchievementCategory.Secret }, CancellationToken.None);
        Assert.Equal(10, locked.TotalCount);
        Assert.All(locked.Items, a =>
        {
            Assert.False(a.IsUnlocked);
            Assert.True(a.IsSecret);
            Assert.Null(a.Code);
            Assert.Null(a.CurrentValue);
            Assert.Null(a.TargetValue);
            Assert.Equal("Gizli Başarım", a.Name);
            Assert.Equal("secret_locked", a.IconKey);
        });

        await RecordAsync(context, userId, AchievementEventTypes.BottleEasterEggDiscovered);

        var after = await context.Achievements.GetAchievementsAsync(userId,
            new AchievementQuery { Category = AchievementCategory.Secret }, CancellationToken.None);
        var easterEgg = Assert.Single(after.Items, a => a.Code == "SECRET_BOTTLE_TAPS_10");
        Assert.True(easterEgg.IsUnlocked);
        Assert.Equal("Küçük Sürpriz", easterEgg.Name);
        Assert.Equal(8, after.Items.Count(a => a.Code is null));   // everything else stays hidden
    }

    [Fact]
    public async Task ReturningAfter14InactiveDays_AndStayingActive_UnlocksComebackSecrets()
    {
        var start = Now.AddDays(-30);
        await using var context = new ServiceTestContext(database, start);
        var userId = await context.RegisterUserAsync();
        await context.DrinkAtAsync(userId, 250, start);

        await context.DrinkAtAsync(userId, 250, start.AddDays(14));    // only 13 full days in between
        Assert.DoesNotContain("SECRET_RETURN_14_DAYS", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.DrinkAtAsync(userId, 250, start.AddDays(29));    // 14 full days in between
        Assert.Contains("SECRET_RETURN_14_DAYS", (await UnlockedCodesAsync(context, userId)).ToHashSet());
        Assert.DoesNotContain("SECRET_COMEBACK_3_DAYS", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.DrinkAtAsync(userId, 250, start.AddDays(29).AddHours(2));   // same day: still 1 active day
        await context.DrinkAtAsync(userId, 250, start.AddDays(30));
        Assert.DoesNotContain("SECRET_COMEBACK_3_DAYS", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        await context.DrinkAtAsync(userId, 250, start.AddDays(31));
        Assert.Contains("SECRET_COMEBACK_3_DAYS", (await UnlockedCodesAsync(context, userId)).ToHashSet());
    }

    [Fact]
    public async Task WaterInANewTimeZone_UnlocksTravelSecret()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        await context.AddIntakeAsync(userId, 250);
        Assert.DoesNotContain("SECRET_TIMEZONE_TRAVEL", (await UnlockedCodesAsync(context, userId)).ToHashSet());

        var tokyo = await context.AddIntakeAsync(userId, 250, timeZone: "Asia/Tokyo");
        Assert.Contains("SECRET_TIMEZONE_TRAVEL", tokyo.Gamification!.NewlyUnlocked.Select(a => a.Code));
    }

    // ------------------------------------------------------------------ rewards

    [Fact]
    public async Task Rewards_UnlockByLevel_OnlyOnce()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var result = await context.AddIntakeAsync(userId, 2000);

        // WELCOME 10, FIRST_DROP 10, FIRST_GLASS 15, FIRST_DAY_500 20, FIRST_HALF_GOAL 20, FIRST_GOAL 50,
        // TOTAL_ML_1000 20, GLASSES_1 10, GLASSES_5 25 = 180 XP = level 7 (threshold 180).
        Assert.Equal(180, result.Gamification!.Progress.TotalXp);
        Assert.Equal(7, result.Gamification.Progress.Level);
        Assert.Equal(["BOTTLE_SKY_BLUE"], result.Gamification.NewRewards.Select(r => r.Code));
        Assert.Equal("bottle_sky_blue", result.Gamification.NewRewards[0].AssetKey);

        Assert.Empty((await context.Achievements.EvaluateAchievementsAsync(userId, CancellationToken.None)).NewRewards);
        // Starter themes (level 1) were already granted at registration.
        Assert.Equal(["BOTTLE_SKY_BLUE", "BOTTLE_CLASSIC", "BOTTLE_OCEAN", "BOTTLE_MINT"], await RewardCodesAsync(context, userId));

        var rewards = await context.Achievements.GetUserRewardsAsync(userId, CancellationToken.None);
        Assert.Equal(4, rewards.Count);
        Assert.All(rewards, r => Assert.Equal(RewardType.BottleTheme, r.RewardType));
    }

    // ------------------------------------------------------------------ existing users, isolation, concurrency

    [Fact]
    public async Task UsersFromBeforeTheAchievementSystem_GetEarnedAchievementsOnLogin_Idempotently()
    {
        var start = Now.AddDays(-20);
        await using var context = new ServiceTestContext(database, start);
        var email = $"{Guid.NewGuid():N}@example.com";
        var registered = await context.Auth.RegisterAsync(new RegisterRequest
        {
            Name = "Old User", Email = email, Password = "Passw0rd!", TimeZone = "Europe/Istanbul"
        }, CancellationToken.None);
        var userId = registered.User.Id;

        // Simulate an account from before this feature: no achievement data, only water history.
        await context.Db.UserAchievements.Where(ua => ua.UserId == userId).ExecuteDeleteAsync();
        await context.Db.UserRewards.Where(ur => ur.UserId == userId).ExecuteDeleteAsync();
        await context.Db.UserProgress.Where(p => p.UserId == userId).ExecuteDeleteAsync();
        await context.Db.UserAchievementEvents.Where(e => e.UserId == userId).ExecuteDeleteAsync();
        for (var day = 0; day < 8; day++)
        {
            var moment = start.AddDays(day);
            context.Db.WaterIntakes.Add(new WaterIntake
            {
                Id = Guid.NewGuid(), UserId = userId, AmountMl = 2000, ConsumedAt = moment,
                LocalDate = DateTimeHelper.ToLocalDate(moment, "Europe/Istanbul"), TimeZone = "Europe/Istanbul",
                ClientEventId = Guid.NewGuid(), CreatedAt = moment, UpdatedAt = moment
            });
        }
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();

        context.Clock.Now = Now;
        await context.Auth.LoginAsync(new LoginRequest { Email = email, Password = "Passw0rd!" }, CancellationToken.None);

        var unlocked = await UnlockedCodesAsync(context, userId);
        Assert.Superset(new HashSet<string> { "WELCOME", "FIRST_DROP", "FIRST_GOAL", "STREAK_7", "ACTIVE_DAYS_7", "TOTAL_ML_10000" }, unlocked.ToHashSet());

        // Phone-only interactions are never assumed for old accounts.
        Assert.DoesNotContain("ONBOARDING_COMPLETED", unlocked);
        Assert.DoesNotContain("STATS_FIRST_VIEW", unlocked);

        var xp = await TotalXpAsync(context, userId);
        await AssertXpIsConsistentAsync(context, userId, xp);

        Assert.Empty((await context.Achievements.EvaluateAchievementsAsync(userId, CancellationToken.None)).NewlyUnlocked);
        await context.Auth.LoginAsync(new LoginRequest { Email = email, Password = "Passw0rd!" }, CancellationToken.None);
        Assert.Equal(unlocked, await UnlockedCodesAsync(context, userId));
        Assert.Equal(xp, await TotalXpAsync(context, userId));
    }

    [Fact]
    public async Task ParallelEvaluations_NeverCreateDuplicatesOrDoubleXp()
    {
        await using var setup = new ServiceTestContext(database, Now);
        var userId = await setup.RegisterUserAsync();
        await setup.Db.UserAchievements.Where(ua => ua.UserId == userId).ExecuteDeleteAsync();
        await setup.Db.UserProgress.Where(p => p.UserId == userId).ExecuteDeleteAsync();
        setup.Db.WaterIntakes.Add(new WaterIntake
        {
            Id = Guid.NewGuid(), UserId = userId, AmountMl = 2000, ConsumedAt = Now, LocalDate = new DateOnly(2026, 11, 23),
            TimeZone = "Europe/Istanbul", ClientEventId = Guid.NewGuid(), CreatedAt = Now, UpdatedAt = Now
        });
        await setup.Db.SaveChangesAsync();

        var contexts = Enumerable.Range(0, 6).Select(_ => new ServiceTestContext(database, Now)).ToList();
        try
        {
            var results = await Task.WhenAll(contexts.Select(c => c.Achievements.EvaluateAchievementsAsync(userId, CancellationToken.None)));

            // Exactly one evaluation unlocked the achievements; the others found nothing new.
            Assert.Single(results, r => r.NewlyUnlocked.Count > 0);
            Assert.All(results, r => Assert.Equal(180, r.Progress.TotalXp));
        }
        finally
        {
            foreach (var c in contexts)
            {
                await c.DisposeAsync();
            }
        }

        await AssertXpIsConsistentAsync(setup, userId, 180);
        Assert.Equal(1, await setup.Db.UserProgress.CountAsync(p => p.UserId == userId));
    }

    [Fact]
    public async Task Achievements_AreIsolatedBetweenUsers()
    {
        await using var context = new ServiceTestContext(database, Now);
        var alice = await context.RegisterUserAsync();
        var bob = await context.RegisterUserAsync();

        await context.AddIntakeAsync(alice, 250);

        Assert.Equal(["WELCOME"], await UnlockedCodesAsync(context, bob));
        Assert.Equal(10, await TotalXpAsync(context, bob));
        Assert.Equal(45, await TotalXpAsync(context, alice));

        // Bob marking achievement ids only touches his own rows.
        var firstDropId = (await context.Db.Achievements.SingleAsync(a => a.Code == "FIRST_DROP")).Id;
        var marked = await context.Achievements.MarkAchievementsSeenAsync(bob,
            new MarkAchievementsSeenRequest { AchievementIds = [firstDropId] }, CancellationToken.None);
        Assert.Equal(0, marked.MarkedCount);
        Assert.Contains(await context.Achievements.GetUnseenAchievementsAsync(alice, CancellationToken.None), a => a.Code == "FIRST_DROP");

        Assert.Equal(1, (await context.Achievements.MarkAchievementsSeenAsync(bob, new MarkAchievementsSeenRequest { All = true }, CancellationToken.None)).MarkedCount);
        Assert.Empty(await context.Achievements.GetUnseenAchievementsAsync(bob, CancellationToken.None));
        Assert.Equal(4, (await context.Achievements.GetUnseenAchievementsAsync(alice, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task DeletingTheAccount_RemovesAchievementData()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        await context.AddIntakeAsync(userId, 2000);
        await RecordAsync(context, userId, AchievementEventTypes.AppOpened);

        await context.Users.DeleteCurrentUserAsync(userId, new DeleteAccountRequest { Password = "Passw0rd!" }, CancellationToken.None);

        Assert.False(await context.Db.UserAchievements.AnyAsync(ua => ua.UserId == userId));
        Assert.False(await context.Db.UserProgress.AnyAsync(p => p.UserId == userId));
        Assert.False(await context.Db.UserRewards.AnyAsync(r => r.UserId == userId));
        Assert.False(await context.Db.UserAchievementEvents.AnyAsync(e => e.UserId == userId));
        Assert.Equal(76, await context.Db.Achievements.CountAsync());   // the catalog is not affected
    }

    [Fact]
    public async Task FailingAchievementCheck_NeverFailsTheMainAction()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        // Evaluating a user that does not exist fails inside; the safe variant reports null instead of throwing.
        Assert.Null(await context.Achievements.TryEvaluateAchievementsAsync(Guid.NewGuid(), CancellationToken.None));

        // The shared DbContext is still usable afterwards and water is saved normally.
        var result = await context.AddIntakeAsync(userId, 250);
        Assert.Equal(250, result.Progress.ConsumedMl);
        Assert.NotNull(result.Gamification);
    }

    // ------------------------------------------------------------------ helpers

    private static Task<RecordAchievementEventResponse> RecordAsync(ServiceTestContext context, Guid userId, string eventType,
        string? value = null, Guid? id = null) =>
        context.Achievements.RecordEventAsync(userId, new RecordAchievementEventRequest
        {
            ClientEventId = id ?? Guid.NewGuid(),
            EventType = eventType,
            EventValue = value
        }, CancellationToken.None);

    private static async Task<List<string>> UnlockedCodesAsync(ServiceTestContext context, Guid userId) =>
        (await context.Db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.Achievement.Code)
            .ToListAsync())
        .Order()
        .ToList();

    private static async Task<List<string>> RewardCodesAsync(ServiceTestContext context, Guid userId) =>
        await context.Db.UserRewards.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .OrderBy(ur => ur.RewardId)
            .Select(ur => ur.Reward.Code)
            .ToListAsync();

    private static Task<int> TotalXpAsync(ServiceTestContext context, Guid userId) =>
        context.Db.UserProgress.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.TotalXp).SingleAsync();

    private static async Task<AchievementResponse> GetAchievementAsync(ServiceTestContext context, Guid userId, string code)
    {
        var all = await context.Achievements.GetAchievementsAsync(userId, new AchievementQuery(), CancellationToken.None);
        return all.Items.Single(a => a.Code == code);
    }

    // UserProgress.TotalXp must always equal the XP of the unlocked achievements, and Level must match the formula.
    private static async Task AssertXpIsConsistentAsync(ServiceTestContext context, Guid userId, int? expectedXp = null)
    {
        var progress = await context.Db.UserProgress.AsNoTracking().SingleAsync(p => p.UserId == userId);
        var earned = await context.Db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .SumAsync(ua => ua.Achievement.XpReward);

        Assert.Equal(earned, progress.TotalXp);
        Assert.Equal(LevelCalculator.LevelFor(earned), progress.Level);
        if (expectedXp is { } xp)
        {
            Assert.Equal(xp, progress.TotalXp);
        }
    }
}
