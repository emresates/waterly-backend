namespace Waterly.Api.DTOs.Hydration;

// Progress of a single local day. Also used as a row in history and statistics.
public class DailyProgressResponse
{
    public DateOnly Date { get; init; }
    public int DailyGoalMl { get; init; }
    public int ConsumedMl { get; init; }
    public int RemainingMl { get; init; }
    public decimal ProgressPercentage { get; init; }
    public bool GoalCompleted { get; init; }
    public int IntakeCount { get; init; }
}
