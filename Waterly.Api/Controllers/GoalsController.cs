using Microsoft.AspNetCore.Mvc;
using Waterly.Api.DTOs.Goals;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

[Route("api/goals")]
public class GoalsController(IGoalService goalService) : ApiControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<CurrentGoalResponse>>> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await goalService.GetCurrentGoalAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    [HttpPut("current")]
    public async Task<ActionResult<ApiResponse<CurrentGoalResponse>>> UpdateCurrent(UpdateGoalRequest request, CancellationToken cancellationToken)
    {
        var result = await goalService.UpdateCurrentGoalAsync(CurrentUserId, request, cancellationToken);
        return Success(result, "Günlük hedef güncellendi.");
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<GoalHistoryItemResponse>>>> GetHistory(CancellationToken cancellationToken)
    {
        var result = await goalService.GetGoalHistoryAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }
}
