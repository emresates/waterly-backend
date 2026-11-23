using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Waterly.Api.DTOs.Achievements;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

// Every endpoint works on the signed-in user only (user id from the JWT). GET endpoints have no side effects.
[Route("api/achievements")]
public class AchievementsController(IAchievementService achievementService) : ApiControllerBase
{
    // Full catalog with the user's progress. ?category=Streak&rarity=Rare&unlocked=false&page=1&pageSize=100
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<AchievementResponse>>>> GetAchievements([FromQuery] AchievementQuery query, CancellationToken cancellationToken)
    {
        var result = await achievementService.GetAchievementsAsync(CurrentUserId, query, cancellationToken);
        return Success(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<List<AchievementResponse>>>> GetMyAchievements(CancellationToken cancellationToken)
    {
        var result = await achievementService.GetUserAchievementsAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    [HttpGet("me/summary")]
    public async Task<ActionResult<ApiResponse<AchievementSummaryResponse>>> GetSummary(CancellationToken cancellationToken)
    {
        var result = await achievementService.GetSummaryAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    [HttpGet("me/progress")]
    public async Task<ActionResult<ApiResponse<UserLevelResponse>>> GetProgress(CancellationToken cancellationToken)
    {
        var result = await achievementService.GetUserProgressAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    // ?count=10 (max 50)
    [HttpGet("me/recent")]
    public async Task<ActionResult<ApiResponse<List<AchievementResponse>>>> GetRecent([FromQuery] int count, CancellationToken cancellationToken)
    {
        var result = await achievementService.GetRecentAchievementsAsync(CurrentUserId, count, cancellationToken);
        return Success(result);
    }

    // Unlocked achievements the app has not shown with an animation yet (oldest first).
    [HttpGet("me/unseen")]
    public async Task<ActionResult<ApiResponse<List<AchievementResponse>>>> GetUnseen(CancellationToken cancellationToken)
    {
        var result = await achievementService.GetUnseenAchievementsAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    // { "achievementIds": [2, 3] } or { "all": true }
    [HttpPost("me/seen")]
    public async Task<ActionResult<ApiResponse<MarkAchievementsSeenResponse>>> MarkSeen(MarkAchievementsSeenRequest request, CancellationToken cancellationToken)
    {
        var result = await achievementService.MarkAchievementsSeenAsync(CurrentUserId, request, cancellationToken);
        return Success(result, "Başarımlar görüldü olarak işaretlendi.");
    }

    // Recalculates everything from stored data (idempotent). The app can call it on start-up to catch up.
    [HttpPost("me/evaluate")]
    public async Task<ActionResult<ApiResponse<AchievementEvaluationResponse>>> Evaluate(CancellationToken cancellationToken)
    {
        var result = await achievementService.EvaluateAchievementsAsync(CurrentUserId, cancellationToken);
        return Success(result, "Başarımlar güncellendi.");
    }

    [HttpGet("me/rewards")]
    public async Task<ActionResult<ApiResponse<List<RewardResponse>>>> GetRewards(CancellationToken cancellationToken)
    {
        var result = await achievementService.GetUserRewardsAsync(CurrentUserId, cancellationToken);
        return Success(result);
    }

    // Whitelisted mobile interactions. Retries with the same clientEventId return 200 without changing anything.
    [HttpPost("events")]
    [EnableRateLimiting(RateLimitPolicies.AchievementEvents)]
    public async Task<ActionResult<ApiResponse<RecordAchievementEventResponse>>> RecordEvent(RecordAchievementEventRequest request, CancellationToken cancellationToken)
    {
        var result = await achievementService.RecordEventAsync(CurrentUserId, request, cancellationToken);
        return result.Status == AchievementEventStatus.Recorded
            ? Success(result, "Olay kaydedildi.")
            : Success(result, "Bu olay daha önce işlenmiş.");
    }
}
