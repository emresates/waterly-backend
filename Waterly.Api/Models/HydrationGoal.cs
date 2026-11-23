namespace Waterly.Api.Models;

// A goal is never edited or deleted. Every change adds a new row that is valid
// from EffectiveFrom onwards, so past days keep the goal they had at that time.
public class HydrationGoal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int TargetMl { get; set; }

    // First local calendar day this goal applies to.
    public DateOnly EffectiveFrom { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
