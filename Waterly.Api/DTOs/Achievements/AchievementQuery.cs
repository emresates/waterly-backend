using System.ComponentModel.DataAnnotations;
using Waterly.Api.Models;

namespace Waterly.Api.DTOs.Achievements;

// Query string: ?category=Streak&rarity=Rare&unlocked=false&page=1&pageSize=50
public class AchievementQuery
{
    public AchievementCategory? Category { get; set; }
    public AchievementRarity? Rarity { get; set; }
    public bool? Unlocked { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "page en az 1 olmalıdır.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize 1-100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 100;
}
