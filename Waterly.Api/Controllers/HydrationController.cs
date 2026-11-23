using Microsoft.AspNetCore.Mvc;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

[Route("api/hydration")]
public class HydrationController(IHydrationService hydrationService) : ApiControllerBase
{
    [HttpGet("today")]
    public async Task<ActionResult<ApiResponse<TodayResponse>>> GetToday(CancellationToken cancellationToken)
    {
        var result = await hydrationService.GetTodayAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    // Returns 201 when a new intake is stored, 200 when the same clientEventId was already processed.
    [HttpPost("intakes")]
    public async Task<ActionResult<ApiResponse<IntakeResultResponse>>> AddIntake(AddIntakeRequest request, CancellationToken cancellationToken)
    {
        var result = await hydrationService.AddIntakeAsync(CurrentUserId, request, cancellationToken);
        return result.AlreadyProcessed
            ? Success(result, "Bu işlem daha önce kaydedilmiş.")
            : CreatedResponse(result, "Su tüketimi kaydedildi.");
    }

    // Uploads intakes that were queued on the phone while it was offline.
    [HttpPost("intakes/sync")]
    public async Task<ActionResult<ApiResponse<SyncIntakesResponse>>> SyncIntakes(SyncIntakesRequest request, CancellationToken cancellationToken)
    {
        var result = await hydrationService.SyncIntakesAsync(CurrentUserId, request, cancellationToken);
        return Success(result, "Senkronizasyon tamamlandı.");
    }

    [HttpGet("intakes")]
    public async Task<ActionResult<ApiResponse<PagedResponse<IntakeResponse>>>> GetIntakes([FromQuery] DateRangeQuery query, CancellationToken cancellationToken)
    {
        var result = await hydrationService.GetIntakesAsync(CurrentUserId, query, cancellationToken);
        return Success(result);
    }

    [HttpPut("intakes/{id:guid}")]
    public async Task<ActionResult<ApiResponse<IntakeResultResponse>>> UpdateIntake(Guid id, UpdateIntakeRequest request, CancellationToken cancellationToken)
    {
        var result = await hydrationService.UpdateIntakeAsync(CurrentUserId, id, request, cancellationToken);
        return Success(result, "Su tüketimi güncellendi.");
    }

    [HttpDelete("intakes/{id:guid}")]
    public async Task<ActionResult<ApiResponse<DailyProgressResponse>>> DeleteIntake(Guid id, CancellationToken cancellationToken)
    {
        var result = await hydrationService.DeleteIntakeAsync(CurrentUserId, id, cancellationToken);
        return Success(result, "Su tüketimi silindi.");
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<PagedResponse<DailyProgressResponse>>>> GetHistory([FromQuery] DateRangeQuery query, CancellationToken cancellationToken)
    {
        var result = await hydrationService.GetHistoryAsync(CurrentUserId, query, cancellationToken);
        return Success(result);
    }
}
