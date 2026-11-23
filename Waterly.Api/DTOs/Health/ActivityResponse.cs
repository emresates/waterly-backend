using Waterly.Api.Models;

namespace Waterly.Api.DTOs.Health;

public class ActivityResponse
{
    public DateOnly Date { get; init; }
    public int Steps { get; init; }
    public int ExerciseMinutes { get; init; }
    public decimal? ActiveCalories { get; init; }
    public ActivitySource Source { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
