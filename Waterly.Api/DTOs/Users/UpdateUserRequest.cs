using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Users;

public class UpdateUserRequest
{
    [Required(ErrorMessage = "İsim zorunludur.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "İsim 2-100 karakter olmalıdır.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Saat dilimi zorunludur.")]
    [StringLength(64)]
    public string TimeZone { get; set; } = string.Empty;
}
