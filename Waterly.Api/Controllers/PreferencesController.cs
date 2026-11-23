using Microsoft.AspNetCore.Mvc;
using Waterly.Api.DTOs.Preferences;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

// Account-level appearance settings and the cosmetic catalog. Always for the signed-in user only.
[Route("api")]
public class PreferencesController(IPreferenceService preferenceService) : ApiControllerBase
{
    [HttpGet("users/me/preferences")]
    public async Task<ActionResult<ApiResponse<PreferencesResponse>>> GetPreferences(CancellationToken cancellationToken)
    {
        var result = await preferenceService.GetPreferencesAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    // Full replacement: { colorScheme, bottleTheme, profileFrame, waterAnimation, badgeEffect, mascotAccessory }.
    // 403 rewardLocked if a cosmetic is not earned yet, 400 invalidReward if it is the wrong type, 404 rewardNotFound if unknown.
    [HttpPut("users/me/preferences")]
    public async Task<ActionResult<ApiResponse<PreferencesResponse>>> UpdatePreferences(UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        var result = await preferenceService.UpdatePreferencesAsync(CurrentUserId, request, cancellationToken);
        return Success(result, "Tercihler kaydedildi.");
    }

    // ?type=BottleTheme (optional)
    [HttpGet("cosmetics")]
    public async Task<ActionResult<ApiResponse<List<CosmeticItemResponse>>>> GetCosmetics([FromQuery] CosmeticsQuery query, CancellationToken cancellationToken)
    {
        var result = await preferenceService.GetCosmeticsAsync(CurrentUserId, query, cancellationToken);
        return Success(result);
    }
}
