using Waterly.Api.DTOs.Preferences;

namespace Waterly.Api.Interfaces;

public interface IPreferenceService
{
    // Appearance settings stored on the account (dark mode, active bottle theme and other cosmetics).
    Task<PreferencesResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    // Only earned (or starter) cosmetics of the right type can be selected.
    Task<PreferencesResponse> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken);

    // Every cosmetic reward with locked / unlocked / active status.
    Task<List<CosmeticItemResponse>> GetCosmeticsAsync(Guid userId, CosmeticsQuery query, CancellationToken cancellationToken);
}
