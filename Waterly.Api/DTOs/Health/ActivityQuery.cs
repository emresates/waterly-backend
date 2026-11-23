namespace Waterly.Api.DTOs.Health;

// Defaults to the last 7 days.
public class ActivityQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
