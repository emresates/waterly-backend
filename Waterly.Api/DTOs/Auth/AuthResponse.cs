using Waterly.Api.DTOs.Users;

namespace Waterly.Api.DTOs.Auth;

public class AuthResponse
{
    public string TokenType { get; init; } = "Bearer";
    public string AccessToken { get; init; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public DateTimeOffset RefreshTokenExpiresAt { get; init; }
    public UserResponse User { get; init; } = null!;
}
