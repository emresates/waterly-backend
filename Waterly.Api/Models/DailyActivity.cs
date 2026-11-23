namespace Waterly.Api.Models;

// Only a small daily summary is stored. No heart rate, body or medical data.
public class DailyActivity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int Steps { get; set; }
    public int ExerciseMinutes { get; set; }
    public decimal? ActiveCalories { get; set; }
    public ActivitySource Source { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
