namespace Waterly.Api.Models;

// Cosmetic reward, seeded from Data/RewardCatalog.cs.
// Unlocked when every requirement that is set is met (a level, an achievement, or both).
public class Reward
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RewardType RewardType { get; set; }

    // Name of the image / animation bundled in the mobile app. No files are stored on the server.
    public string AssetKey { get; set; } = string.Empty;

    public int? RequiredLevel { get; set; }
    public string? RequiredAchievementCode { get; set; }
}
