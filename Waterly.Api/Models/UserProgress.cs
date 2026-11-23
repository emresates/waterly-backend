namespace Waterly.Api.Models;

// One row per user. TotalXp is always recalculated as the sum of XpReward of the user's unlocked achievements,
// so it can never drift away from UserAchievements.
public class UserProgress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
