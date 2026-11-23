using Waterly.Api.DTOs.Health;

namespace Waterly.Api.Interfaces;

public interface IHealthService
{
    Task<List<ActivityResponse>> SaveActivityAsync(Guid userId, SaveActivityRequest request, CancellationToken cancellationToken);
    Task<List<ActivityResponse>> GetActivityAsync(Guid userId, ActivityQuery query, CancellationToken cancellationToken);
    Task<RecommendationResponse> GetRecommendationAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken);
}
