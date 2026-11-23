namespace Waterly.Api.DTOs.Statistics;

public class StreakResponse
{
    public int CurrentStreak { get; init; }
    public int LongestStreak { get; init; }
    public bool TodayCompleted { get; init; }
    public DateOnly? LastCompletedDate { get; init; }
}
