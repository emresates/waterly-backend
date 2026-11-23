namespace Waterly.Api.Models;

// A small interaction reported by the mobile app (e.g. "statistics screen opened") or recorded by the server
// (successful session). Only whitelisted event types and small, validated values are stored. No free-form JSON.
public class UserAchievementEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Idempotency key generated on the phone. Unique per user.
    public Guid ClientEventId { get; set; }

    // One of AchievementEventTypes.
    public string EventType { get; set; } = string.Empty;

    // Validated small value, only for some event types (bottle theme key, viewed calendar day).
    public string? EventValue { get; set; }

    // Small value derived by the server, e.g. the ISO week "2026-W48" of a weekly report.
    public string? EventMetadata { get; set; }

    // Server-built key that limits how often an event can count: "day:2026-11-23", "week:2026-W48", "once", "theme:ocean".
    // Unique per (user, event type), so opening the same screen 100 times a day stores a single row.
    public string DedupeKey { get; set; } = string.Empty;

    // The user's local day according to the server clock. Never taken from the device.
    public DateOnly LocalDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
