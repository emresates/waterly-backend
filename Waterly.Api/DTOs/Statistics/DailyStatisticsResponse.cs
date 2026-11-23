using Waterly.Api.DTOs.Hydration;

namespace Waterly.Api.DTOs.Statistics;

public class HourlyConsumption
{
    public int Hour { get; init; }
    public int AmountMl { get; init; }
    public int IntakeCount { get; init; }
}

public class DailyStatisticsResponse
{
    public DailyProgressResponse Progress { get; init; } = null!;

    // Always 24 items (hour 0..23) in the time zone each intake was recorded in.
    public List<HourlyConsumption> HourlyDistribution { get; init; } = [];
}
