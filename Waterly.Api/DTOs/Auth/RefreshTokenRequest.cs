using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Auth;

// Used by both /api/auth/refresh and /api/auth/logout.
public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token zorunludur.")]
    [StringLength(200)]
    public string RefreshToken { get; set; } = string.Empty;
}
