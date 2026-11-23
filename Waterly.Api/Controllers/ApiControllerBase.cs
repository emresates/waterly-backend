using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Exceptions;

namespace Waterly.Api.Controllers;

// Small shared base for all controllers: reading the current user id and wrapping results in ApiResponse.
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    // The user id comes only from the signed JWT ("sub" claim), never from the request body or URL.
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : throw AppException.Unauthorized(ErrorCodes.Unauthorized, "Geçersiz kullanıcı oturumu.");

    protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = "İşlem başarılı.") =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected ActionResult<ApiResponse<T>> CreatedResponse<T>(T data, string message) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Ok(data, message, StatusCodes.Status201Created));
}
