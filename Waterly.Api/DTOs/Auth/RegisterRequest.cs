using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "İsim zorunludur.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "İsim 2-100 karakter olmalıdır.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Şifre en az bir harf ve bir rakam içermelidir.")]
    public string Password { get; set; } = string.Empty;

    // Optional. Defaults to Europe/Istanbul.
    [StringLength(64)]
    public string? TimeZone { get; set; }
}
