using Waterly.Api.Models;

namespace Waterly.Api.DTOs.Achievements;

// A locked secret achievement is returned as a placeholder: code, name, description, icon and progress are hidden.
public class AchievementResponse
{
    public int Id { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AchievementCategory Category { get; init; }
    public AchievementRarity Rarity { get; init; }
    public int XpReward { get; init; }
    public string IconKey { get; init; } = string.Empty;
    public bool IsSecret { get; init; }
    public bool IsUnlocked { get; init; }
    public long? CurrentValue { get; init; }
    public int? TargetValue { get; init; }
    public decimal? ProgressPercentage { get; init; }
    public DateTimeOffset? UnlockedAt { get; init; }
    public DateTimeOffset? SeenAt { get; init; }
}
