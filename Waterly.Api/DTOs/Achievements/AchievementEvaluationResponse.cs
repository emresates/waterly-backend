namespace Waterly.Api.DTOs.Achievements;

// Result of checking a user's achievements after an action (water added, event recorded, ...).
// NewlyUnlocked is empty when nothing new was earned; the app can play animations for these items.
public class AchievementEvaluationResponse
{
    public List<AchievementResponse> NewlyUnlocked { get; init; } = [];
    public List<RewardResponse> NewRewards { get; init; } = [];
    public UserLevelResponse Progress { get; init; } = null!;
    public int PreviousLevel { get; init; }
    public bool LeveledUp { get; init; }
}
