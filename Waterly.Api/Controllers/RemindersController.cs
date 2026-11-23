using Microsoft.AspNetCore.Mvc;
using Waterly.Api.DTOs.Reminders;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

[Route("api/reminders")]
public class RemindersController(IReminderService reminderService) : ApiControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<ApiResponse<ReminderSettingsResponse>>> GetSettings(CancellationToken cancellationToken)
    {
        var result = await reminderService.GetSettingsAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    [HttpPut("settings")]
    public async Task<ActionResult<ApiResponse<ReminderSettingsResponse>>> UpdateSettings(UpdateReminderSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await reminderService.UpdateSettingsAsync(CurrentUserId, request, cancellationToken);
        return Success(result, "Hatırlatma ayarları güncellendi.");
    }
}
