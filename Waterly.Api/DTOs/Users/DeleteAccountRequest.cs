using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Users;

// Deleting an account is irreversible, so the password is asked again.
public class DeleteAccountRequest
{
    [Required(ErrorMessage = "Hesabı silmek için şifre zorunludur.")]
    public string Password { get; set; } = string.Empty;
}
