using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Waterly.Api.DTOs.Achievements;
using Waterly.Api.Models;

namespace Waterly.Api.DTOs.Preferences;

// One cosmetic reward with the user's status. Used by GET /api/cosmetics and inside the preferences.
public class CosmeticItemResponse
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RewardType RewardType { get; init; }

    // File name of the image / animation bundled in the mobile app.
    public string AssetKey { get; init; } = string.Empty;

    public int? RequiredLevel { get; init; }
    public string? RequiredAchievementCode { get; init; }

    // Free for everyone (starter bottle themes).
    public bool IsStarter { get; init; }
    public bool IsUnlocked { get; init; }
    public DateTimeOffset? UnlockedAt { get; init; }

    // Currently selected in the user's preferences.
    public bool IsActive { get; init; }
}

public class PreferencesResponse
{
    public ColorScheme ColorScheme { get; init; }

    // Never null: falls back to the default starter theme if the user has not picked one.
    public CosmeticItemResponse BottleTheme { get; init; } = null!;
    public CosmeticItemResponse? ProfileFrame { get; init; }
    public CosmeticItemResponse? WaterAnimation { get; init; }
    public CosmeticItemResponse? BadgeEffect { get; init; }
    public CosmeticItemResponse? MascotAccessory { get; init; }

    // Null until the user saves preferences for the first time.
    public DateTimeOffset? UpdatedAt { get; init; }

    // Only on PUT: achievements unlocked by this change (e.g. SECRET_DARK_MODE, SECRET_THEMES_3).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AchievementEvaluationResponse? Gamification { get; init; }
}

// Full replacement of the appearance settings. Cosmetics are reward codes; null = nothing selected.
public class UpdatePreferencesRequest
{
    [Required(ErrorMessage = "colorScheme zorunludur (System, Light veya Dark).")]
    public ColorScheme? ColorScheme { get; set; }

    [Required(ErrorMessage = "bottleTheme zorunludur, örneğin BOTTLE_CLASSIC.")]
    [StringLength(64)]
    public string BottleTheme { get; set; } = string.Empty;

    [StringLength(64)]
    public string? ProfileFrame { get; set; }

    [StringLength(64)]
    public string? WaterAnimation { get; set; }

    [StringLength(64)]
    public string? BadgeEffect { get; set; }

    [StringLength(64)]
    public string? MascotAccessory { get; set; }
}

// Query string: ?type=BottleTheme
public class CosmeticsQuery
{
    public RewardType? Type { get; set; }
}
