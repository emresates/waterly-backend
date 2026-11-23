using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Achievements;

// Either a list of achievement ids, or { "all": true } to mark every unseen achievement.
public class MarkAchievementsSeenRequest
{
    [MaxLength(100, ErrorMessage = "Tek seferde en fazla 100 başarım işaretlenebilir.")]
    public List<int> AchievementIds { get; set; } = [];

    public bool All { get; set; }
}

public class MarkAchievementsSeenResponse
{
    public int MarkedCount { get; init; }
}
