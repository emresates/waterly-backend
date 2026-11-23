namespace Waterly.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Always stored trimmed and lower-case so the unique index is case-insensitive.
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // IANA time zone id, e.g. "Europe/Istanbul".
    public string TimeZone { get; set; } = "Europe/Istanbul";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ReminderSettings? ReminderSettings { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<WaterIntake> WaterIntakes { get; set; } = [];
    public List<HydrationGoal> HydrationGoals { get; set; } = [];
    public List<DailyActivity> DailyActivities { get; set; } = [];
}
