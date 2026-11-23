using Waterly.Api.Models;

namespace Waterly.Api.DTOs.Achievements;

public class RewardResponse
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RewardType RewardType { get; init; }

    // Name of the image / animation bundled in the mobile app.
    public string AssetKey { get; init; } = string.Empty;

    public int? RequiredLevel { get; init; }
    public string? RequiredAchievementCode { get; init; }
    public DateTimeOffset UnlockedAt { get; init; }
}
