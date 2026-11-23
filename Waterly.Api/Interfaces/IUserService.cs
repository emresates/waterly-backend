using Waterly.Api.DTOs.Users;

namespace Waterly.Api.Interfaces;

public interface IUserService
{
    Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserResponse> UpdateCurrentUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task DeleteCurrentUserAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken);

    // Used by other services to know which calendar day "today" is for the user.
    Task<string> GetTimeZoneAsync(Guid userId, CancellationToken cancellationToken);
}
