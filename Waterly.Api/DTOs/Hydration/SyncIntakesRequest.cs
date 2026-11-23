using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Hydration;

// Lets the app upload everything it queued while offline in one request.
public class SyncIntakesRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "En az bir kayıt gönderilmelidir.")]
    [MaxLength(100, ErrorMessage = "Tek seferde en fazla 100 kayıt gönderilebilir.")]
    public List<AddIntakeRequest> Intakes { get; set; } = [];
}
