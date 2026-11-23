using Waterly.Api.DTOs.Goals;
using Waterly.Api.DTOs.Hydration;

namespace Waterly.Api.Interfaces;

public interface IGoalService
{
    Task<CurrentGoalResponse> GetCurrentGoalAsync(Guid userId, CancellationToken cancellationToken);
    Task<CurrentGoalResponse> UpdateCurrentGoalAsync(Guid userId, UpdateGoalRequest request, CancellationToken cancellationToken);
    Task<List<GoalHistoryItemResponse>> GetGoalHistoryAsync(Guid userId, CancellationToken cancellationToken);

    // The goal that was valid on a given local day.
    Task<int> GetGoalForDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);

    // The goal of every day between from and to (inclusive), loaded with a single query.
    Task<Dictionary<DateOnly, int>> GetGoalsForRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    // Goal + consumed amount for one local day.
    Task<DailyProgressResponse> GetProgressForDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);
}
