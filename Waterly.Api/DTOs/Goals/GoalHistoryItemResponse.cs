namespace Waterly.Api.DTOs.Goals;

public class GoalHistoryItemResponse
{
    public Guid Id { get; init; }
    public int TargetMl { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
