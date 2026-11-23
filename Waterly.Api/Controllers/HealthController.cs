using Microsoft.AspNetCore.Mvc;
using Waterly.Api.DTOs.Health;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

// Health *activity* data (HealthKit / Health Connect summaries).
// The server status check is a separate endpoint: GET /health
[Route("api/health")]
public class HealthController(IHealthService healthService) : ApiControllerBase
{
    [HttpPost("activity")]
    public async Task<ActionResult<ApiResponse<List<ActivityResponse>>>> SaveActivity(SaveActivityRequest request, CancellationToken cancellationToken)
    {
        var result = await healthService.SaveActivityAsync(CurrentUserId, request, cancellationToken);
        return Success(result, "Aktivite verileri kaydedildi.");
    }

    [HttpGet("activity")]
    public async Task<ActionResult<ApiResponse<List<ActivityResponse>>>> GetActivity([FromQuery] ActivityQuery query, CancellationToken cancellationToken)
    {
        var result = await healthService.GetActivityAsync(CurrentUserId, query, cancellationToken);
        return Success(result);
    }

    [HttpGet("recommendation")]
    public async Task<ActionResult<ApiResponse<RecommendationResponse>>> GetRecommendation([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await healthService.GetRecommendationAsync(CurrentUserId, date, cancellationToken);
        return Success(result);
    }
}
