namespace Waterly.Api.Models;

// Account-level appearance settings, so they come back when the user changes phone.
// Earning a reward (UserRewards) and using it (the Active* columns here) are separate:
// a reward can only be selected after it has been earned.
public class UserPreferences
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public ColorScheme ColorScheme { get; set; } = ColorScheme.System;

    // Each column points to a Reward of the matching RewardType. Null = nothing selected (bottle theme falls back to the default).
    public int? ActiveBottleThemeId { get; set; }
    public int? ActiveProfileFrameId { get; set; }
    public int? ActiveWaterAnimationId { get; set; }
    public int? ActiveBadgeEffectId { get; set; }
    public int? ActiveMascotAccessoryId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
