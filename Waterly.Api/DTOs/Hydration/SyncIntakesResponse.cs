using Waterly.Api.DTOs.Achievements;

namespace Waterly.Api.DTOs.Hydration;

public enum SyncItemStatus
{
    Created,
    AlreadyProcessed,
    Failed
}

public class SyncIntakeItemResult
{
    public Guid? ClientEventId { get; init; }
    public SyncItemStatus Status { get; init; }
    public IntakeResponse? Intake { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
}

public class SyncIntakesResponse
{
    public int CreatedCount { get; init; }
    public int AlreadyProcessedCount { get; init; }
    public int FailedCount { get; init; }
    public List<SyncIntakeItemResult> Results { get; init; } = [];

    // Progress of today after the sync, so the app can refresh its main screen at once.
    public DailyProgressResponse Today { get; init; } = null!;

    // Achievements earned by the whole sync, evaluated once at the end.
    public AchievementEvaluationResponse? Gamification { get; init; }
}
