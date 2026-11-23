namespace Waterly.Api.Models;

public class UserReward
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int RewardId { get; set; }
    public DateTimeOffset UnlockedAt { get; set; }

    public User User { get; set; } = null!;
    public Reward Reward { get; set; } = null!;
}
