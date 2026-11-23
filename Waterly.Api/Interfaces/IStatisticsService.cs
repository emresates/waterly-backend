using Waterly.Api.DTOs.Statistics;

namespace Waterly.Api.Interfaces;

public interface IStatisticsService
{
    Task<DailyStatisticsResponse> GetDailyAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken);
    Task<PeriodStatisticsResponse> GetWeeklyAsync(Guid userId, DateOnly? endDate, CancellationToken cancellationToken);
    Task<PeriodStatisticsResponse> GetMonthlyAsync(Guid userId, MonthlyStatisticsQuery query, CancellationToken cancellationToken);
    Task<StreakResponse> GetStreakAsync(Guid userId, CancellationToken cancellationToken);
}
