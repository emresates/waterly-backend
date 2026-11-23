using Waterly.Api.Models;

namespace Waterly.Api.Interfaces;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
    string GenerateRefreshToken();
    string HashToken(string token);
}
