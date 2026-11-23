using Waterly.Api.DTOs.Reminders;

namespace Waterly.Api.Interfaces;

public interface IReminderService
{
    Task<ReminderSettingsResponse> GetSettingsAsync(Guid userId, CancellationToken cancellationToken);
    Task<ReminderSettingsResponse> UpdateSettingsAsync(Guid userId, UpdateReminderSettingsRequest request, CancellationToken cancellationToken);
}
