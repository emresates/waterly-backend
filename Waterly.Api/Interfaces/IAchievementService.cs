using Waterly.Api.DTOs.Achievements;
using Waterly.Api.DTOs.Responses;

namespace Waterly.Api.Interfaces;

public interface IAchievementService
{
    // Full catalog with the user's progress. Locked secret achievements are returned as placeholders.
    Task<PagedResponse<AchievementResponse>> GetAchievementsAsync(Guid userId, AchievementQuery query, CancellationToken cancellationToken);

    // Unlocked achievements, newest first.
    Task<List<AchievementResponse>> GetUserAchievementsAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<AchievementResponse>> GetRecentAchievementsAsync(Guid userId, int count, CancellationToken cancellationToken);
    Task<List<AchievementResponse>> GetUnseenAchievementsAsync(Guid userId, CancellationToken cancellationToken);
    Task<MarkAchievementsSeenResponse> MarkAchievementsSeenAsync(Guid userId, MarkAchievementsSeenRequest request, CancellationToken cancellationToken);

    Task<AchievementSummaryResponse> GetSummaryAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserLevelResponse> GetUserProgressAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<RewardResponse>> GetUserRewardsAsync(Guid userId, CancellationToken cancellationToken);

    // Stores a whitelisted mobile event (idempotent by clientEventId) and re-evaluates achievements.
    Task<RecordAchievementEventResponse> RecordEventAsync(Guid userId, RecordAchievementEventRequest request, CancellationToken cancellationToken);

    // Recalculates everything from stored data and unlocks what is missing. Idempotent and safe to run in parallel.
    // Also repairs UserProgress if TotalXp / Level ever got out of sync.
    Task<AchievementEvaluationResponse> EvaluateAchievementsAsync(Guid userId, CancellationToken cancellationToken);

    // Same as EvaluateAchievementsAsync, but never throws: used after the main action (e.g. saving water) already succeeded.
    // Returns null if the check failed; the next evaluation will catch up.
    Task<AchievementEvaluationResponse?> TryEvaluateAchievementsAsync(Guid userId, CancellationToken cancellationToken);

    // Records a successful session (once per local day) and evaluates achievements. Never throws.
    Task TryRecordSessionAsync(Guid userId, bool alwaysEvaluate, CancellationToken cancellationToken);
}
