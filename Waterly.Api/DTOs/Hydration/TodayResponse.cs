namespace Waterly.Api.DTOs.Hydration;

public class TodayResponse
{
    public string TimeZone { get; init; } = string.Empty;
    public DailyProgressResponse Progress { get; init; } = null!;
    public List<IntakeResponse> Intakes { get; init; } = [];
}
