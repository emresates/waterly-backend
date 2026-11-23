namespace Waterly.Api.DTOs.Achievements;

public class AchievementSummaryResponse
{
    public int TotalAchievements { get; init; }
    public int UnlockedAchievements { get; init; }
    public int LockedAchievements { get; init; }
    public decimal CompletionPercentage { get; init; }
    public int TotalXp { get; init; }
    public int Level { get; init; }
    public decimal LevelProgressPercentage { get; init; }
    public int CommonUnlocked { get; init; }
    public int RareUnlocked { get; init; }
    public int EpicUnlocked { get; init; }
    public int LegendaryUnlocked { get; init; }
    public int MythicUnlocked { get; init; }
    public int UnseenAchievements { get; init; }
}
