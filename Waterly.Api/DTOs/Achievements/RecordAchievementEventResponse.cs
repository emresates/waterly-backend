namespace Waterly.Api.DTOs.Achievements;

public enum AchievementEventStatus
{
    // Stored and counted.
    Recorded,

    // The same clientEventId was already received. Nothing changed.
    AlreadyProcessed,

    // A different event of the same kind was already counted for this period (same day / ISO week / theme / one-time).
    AlreadyCounted
}

public class RecordAchievementEventResponse
{
    public Guid ClientEventId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public AchievementEventStatus Status { get; init; }

    // Filled when the event was recorded. Null if the achievement check failed (the event itself is still stored).
    public AchievementEvaluationResponse? Gamification { get; init; }
}
