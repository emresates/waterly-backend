namespace Waterly.Api.Models;

// What an achievement measures. Every value is calculated on the server from stored data;
// the client can never send a metric value, an achievement code, XP or a level.
public enum AchievementMetric
{
    // --- Server data: sessions, water, goals, settings ---
    SessionStarted,
    IntakeCount,
    RegistrationDayMl,
    BestDailyGoalPercentage,
    GoalCompletedDays,
    GoalCustomized,
    ReminderSettingsSaved,
    ActiveDays,
    StreakDays,
    SecondChance,
    CappedTotalMl,
    GlassEquivalents,
    MorningStartDays,
    NoonDays,
    EveningDays,
    BalancedDays,
    ReminderActionDays,
    ReminderCustomized,
    GlassSizeCustomized,
    HealthConnected,
    TimeZonesUsed,
    LongestInactiveGapDays,
    ComebackActiveDays,

    // --- Mobile events (low-risk, cosmetic achievements only) ---
    OnboardingCompleted,
    StatisticsViewed,
    HistoryViewed,
    WeeklyReportWeeks,
    MonthlyReportViewed,
    CalendarDayViewed,
    HealthRecommendationViewed,
    DarkThemeEnabled,
    BottleThemesUsed,
    BottleEasterEggDiscovered,

    // --- Meta: based on other unlocked achievements (an achievement never counts itself) ---
    AchievementsUnlocked,
    SecretAchievementsUnlocked
}
