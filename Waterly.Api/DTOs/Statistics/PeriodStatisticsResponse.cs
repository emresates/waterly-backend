using Waterly.Api.DTOs.Hydration;

namespace Waterly.Api.DTOs.Statistics;

// Shared by weekly and monthly statistics.
public class PeriodStatisticsResponse
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    // One item per calendar day in the range, ready to be drawn as a chart.
    public List<DailyProgressResponse> Days { get; init; } = [];

    // Days that count for averages: not in the future and not before the user started tracking.
    public int TrackedDays { get; init; }
    public int TotalConsumedMl { get; init; }
    public int AverageDailyMl { get; init; }
    public int CompletedDays { get; init; }
    public decimal SuccessRate { get; init; }
    public DailyProgressResponse? HighestDay { get; init; }
    public DailyProgressResponse? LowestDay { get; init; }
}
