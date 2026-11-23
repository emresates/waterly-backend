using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Hydration;

public class UpdateIntakeRequest
{
    public int AmountMl { get; set; }

    [Required(ErrorMessage = "consumedAt zorunludur.")]
    public DateTimeOffset? ConsumedAt { get; set; }

    // Optional. If omitted the time zone already stored on the intake is kept.
    [StringLength(64)]
    public string? TimeZone { get; set; }
}
