using Microsoft.AspNetCore.Mvc;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.DTOs.Statistics;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

[Route("api/statistics")]
public class StatisticsController(IStatisticsService statisticsService) : ApiControllerBase
{
    // ?date=2026-11-23 (optional, defaults to today in the user's time zone)
    [HttpGet("daily")]
    public async Task<ActionResult<ApiResponse<DailyStatisticsResponse>>> GetDaily([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await statisticsService.GetDailyAsync(CurrentUserId, date, cancellationToken);
        return Success(result);
    }

    // ?endDate=2026-11-23 (optional) -> the 7 days ending on that date
    [HttpGet("weekly")]
    public async Task<ActionResult<ApiResponse<PeriodStatisticsResponse>>> GetWeekly([FromQuery] DateOnly? endDate, CancellationToken cancellationToken)
    {
        var result = await statisticsService.GetWeeklyAsync(CurrentUserId, endDate, cancellationToken);
        return Success(result);
    }

    // No parameters -> last 30 days. ?year=2026&month=11 -> that calendar month.
    [HttpGet("monthly")]
    public async Task<ActionResult<ApiResponse<PeriodStatisticsResponse>>> GetMonthly([FromQuery] MonthlyStatisticsQuery query, CancellationToken cancellationToken)
    {
        var result = await statisticsService.GetMonthlyAsync(CurrentUserId, query, cancellationToken);
        return Success(result);
    }

    [HttpGet("streak")]
    public async Task<ActionResult<ApiResponse<StreakResponse>>> GetStreak(CancellationToken cancellationToken)
    {
        var result = await statisticsService.GetStreakAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }
}
