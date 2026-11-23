using Microsoft.AspNetCore.Mvc;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.DTOs.Users;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Controllers;

[Route("api/users")]
public class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> GetMe(CancellationToken cancellationToken)
    {
        var user = await userService.GetCurrentUserAsync(CurrentUserId, cancellationToken);
        return Success(user);
    }

    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> UpdateMe(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.UpdateCurrentUserAsync(CurrentUserId, request, cancellationToken);
        return Success(user, "Profil güncellendi.");
    }

    [HttpDelete("me")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteMe(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await userService.DeleteCurrentUserAsync(CurrentUserId, request, cancellationToken);
        return Success<object?>(null, "Hesabınız ve tüm verileriniz silindi.");
    }
}
