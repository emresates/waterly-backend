namespace Waterly.Api.Models;

// Catalog row, seeded from Data/AchievementCatalog.cs. Id and Code are permanent and never reused.
public class Achievement
{
    public int Id { get; set; }

    // Stable identifier. The mobile app can also use it as a translation key (e.g. "achievements.FIRST_DROP.name").
    public string Code { get; set; } = string.Empty;

    // Turkish default texts. Other languages can be added later in a separate translation table keyed by Code.
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public AchievementCategory Category { get; set; }
    public AchievementRarity Rarity { get; set; }
    public AchievementMetric MetricType { get; set; }
    public int TargetValue { get; set; }
    public int XpReward { get; set; }

    // Name of the badge asset bundled in the mobile app. No images are stored on the server.
    public string IconKey { get; set; } = string.Empty;

    public bool IsSecret { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
