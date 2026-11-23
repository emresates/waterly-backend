using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Hydration;

public class AddIntakeRequest
{
    // Range is checked in HydrationService so the app receives the specific "invalidWaterAmount" error code.
    public int AmountMl { get; set; }

    // When the water was consumed. If omitted, the server time is used.
    // Offline clients should always send the original time.
    public DateTimeOffset? ConsumedAt { get; set; }

    // A UUID created on the phone for this single action. Sending it again never creates a second record.
    [Required(ErrorMessage = "clientEventId zorunludur.")]
    public Guid? ClientEventId { get; set; }

    // Optional IANA time zone of the device at the time of drinking (useful while travelling).
    // If omitted the user's profile time zone is used.
    [StringLength(64)]
    public string? TimeZone { get; set; }
}
