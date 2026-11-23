namespace Waterly.Api.DTOs.Hydration;

public class IntakeResultResponse
{
    public IntakeResponse Intake { get; init; } = null!;

    // Progress of the day the intake belongs to (not necessarily today).
    public DailyProgressResponse Progress { get; init; } = null!;

    // True when the same clientEventId was already processed before and nothing new was saved.
    public bool AlreadyProcessed { get; init; }
}
