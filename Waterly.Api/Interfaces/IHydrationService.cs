using Waterly.Api.DTOs.Hydration;
using Waterly.Api.DTOs.Responses;

namespace Waterly.Api.Interfaces;

public interface IHydrationService
{
    Task<TodayResponse> GetTodayAsync(Guid userId, CancellationToken cancellationToken);
    Task<IntakeResultResponse> AddIntakeAsync(Guid userId, AddIntakeRequest request, CancellationToken cancellationToken);
    Task<SyncIntakesResponse> SyncIntakesAsync(Guid userId, SyncIntakesRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<IntakeResponse>> GetIntakesAsync(Guid userId, DateRangeQuery query, CancellationToken cancellationToken);
    Task<IntakeResultResponse> UpdateIntakeAsync(Guid userId, Guid intakeId, UpdateIntakeRequest request, CancellationToken cancellationToken);
    Task<DailyProgressResponse> DeleteIntakeAsync(Guid userId, Guid intakeId, CancellationToken cancellationToken);
    Task<PagedResponse<DailyProgressResponse>> GetHistoryAsync(Guid userId, DateRangeQuery query, CancellationToken cancellationToken);

    // One row per day (including days without water) between from and to, oldest first.
    Task<List<DailyProgressResponse>> GetDailyProgressRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    // The first day the user has data for: registration day, or an earlier back-dated intake.
    Task<DateOnly> GetTrackingStartDateAsync(Guid userId, CancellationToken cancellationToken);
}
