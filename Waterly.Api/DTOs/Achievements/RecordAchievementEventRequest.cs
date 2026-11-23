using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Achievements;

// The client only reports that something happened. It can never send an achievement code, XP or a level,
// and the day the event belongs to is decided by the server clock, not by the device.
public class RecordAchievementEventRequest
{
    [Required(ErrorMessage = "clientEventId zorunludur.")]
    public Guid? ClientEventId { get; set; }

    // One of the whitelisted types, e.g. "WEEKLY_REPORT_OPENED".
    [Required(ErrorMessage = "eventType zorunludur.")]
    [StringLength(40)]
    public string EventType { get; set; } = string.Empty;

    // Only for BOTTLE_THEME_USED (theme key, e.g. "ocean") and CALENDAR_DAY_VIEWED (viewed day, "yyyy-MM-dd", optional).
    [StringLength(32)]
    public string? EventValue { get; set; }
}
