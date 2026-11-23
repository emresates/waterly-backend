namespace Waterly.Api.DTOs.Achievements;

// Everything the app needs to draw the level bar, so it never has to repeat the formula.
public class UserLevelResponse
{
    public int Level { get; init; }
    public int TotalXp { get; init; }

    // Total XP at which the current level started.
    public int CurrentLevelXp { get; init; }

    // Total XP needed for the next level. Null at the maximum level.
    public int? NextLevelXp { get; init; }

    public int XpIntoLevel { get; init; }
    public int XpToNextLevel { get; init; }
    public decimal ProgressPercentage { get; init; }
    public int MaxLevel { get; init; } = Helpers.LevelCalculator.MaxLevel;
    public bool IsMaxLevel { get; init; }
}
