namespace Waterly.Api.DTOs.Hydration;

public class IntakeResponse
{
    public Guid Id { get; init; }
    public Guid ClientEventId { get; init; }
    public int AmountMl { get; init; }
    public DateTimeOffset ConsumedAt { get; init; }
    public DateOnly LocalDate { get; init; }
    public string LocalTime { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
