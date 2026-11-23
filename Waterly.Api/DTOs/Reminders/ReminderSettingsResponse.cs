namespace Waterly.Api.DTOs.Reminders;

public class ReminderSettingsResponse
{
    public bool Enabled { get; init; }
    public int IntervalMinutes { get; init; }
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public int DefaultGlassMl { get; init; }
    public bool SoundEnabled { get; init; }
    public bool VibrationEnabled { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    // Local "HH:mm" times the phone should schedule every day. Empty when reminders are disabled.
    public List<string> DailyReminderTimes { get; init; } = [];
}
