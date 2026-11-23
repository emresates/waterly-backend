namespace Waterly.Api.Models;

public class ReminderSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; }

    // Local wall-clock times. If EndTime is earlier than StartTime the window crosses midnight.
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public string TimeZone { get; set; } = string.Empty;
    public int DefaultGlassMl { get; set; }
    public bool SoundEnabled { get; set; }
    public bool VibrationEnabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
