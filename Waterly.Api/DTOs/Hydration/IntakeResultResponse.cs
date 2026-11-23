using Waterly.Api.DTOs.Achievements;

namespace Waterly.Api.DTOs.Hydration;

public class IntakeResultResponse
{
    public IntakeResponse Intake { get; init; } = null!;

    // Progress of the day the intake belongs to (not necessarily today).
    public DailyProgressResponse Progress { get; init; } = null!;

    // True when the same clientEventId was already processed before and nothing new was saved.
    public bool AlreadyProcessed { get; init; }

    // Achievements / XP / level after this change. NewlyUnlocked is empty when nothing new was earned.
    // Null only if the achievement check failed; the water record itself is saved either way.
    public AchievementEvaluationResponse? Gamification { get; init; }
}
