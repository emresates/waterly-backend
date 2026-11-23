using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Goals;

public class UpdateGoalRequest
{
    [Range(500, 10000, ErrorMessage = "Günlük hedef 500 ile 10000 ml arasında olmalıdır.")]
    public int TargetMl { get; set; }
}
