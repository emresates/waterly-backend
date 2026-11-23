namespace Waterly.Api.Models;

public static class AchievementEventTypes
{
    // Sent by the mobile app.
    public const string AppOpened = "APP_OPENED";
    public const string OnboardingCompleted = "ONBOARDING_COMPLETED";
    public const string StatisticsOpened = "STATISTICS_OPENED";
    public const string HistoryOpened = "HISTORY_OPENED";
    public const string WeeklyReportOpened = "WEEKLY_REPORT_OPENED";
    public const string MonthlyReportOpened = "MONTHLY_REPORT_OPENED";
    public const string CalendarDayViewed = "CALENDAR_DAY_VIEWED";
    public const string HealthRecommendationViewed = "HEALTH_RECOMMENDATION_VIEWED";
    public const string DarkThemeEnabled = "DARK_THEME_ENABLED";
    public const string BottleThemeUsed = "BOTTLE_THEME_USED";
    public const string BottleEasterEggDiscovered = "BOTTLE_EASTER_EGG_DISCOVERED";

    // Recorded only by the server (register / login / token refresh). Rejected when sent by a client.
    public const string SessionStarted = "SESSION_STARTED";

    public static readonly IReadOnlySet<string> ClientAllowed = new HashSet<string>(StringComparer.Ordinal)
    {
        AppOpened,
        OnboardingCompleted,
        StatisticsOpened,
        HistoryOpened,
        WeeklyReportOpened,
        MonthlyReportOpened,
        CalendarDayViewed,
        HealthRecommendationViewed,
        DarkThemeEnabled,
        BottleThemeUsed,
        BottleEasterEggDiscovered
    };
}
