namespace Waterly.Api.Models;

// One row per (user, achievement). The unique index makes it impossible to earn the same achievement twice.
public class UserAchievement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int AchievementId { get; set; }
    public DateTimeOffset UnlockedAt { get; set; }

    // Set when the app has shown the unlock animation.
    public DateTimeOffset? SeenAt { get; set; }

    public User User { get; set; } = null!;
    public Achievement Achievement { get; set; } = null!;
}
