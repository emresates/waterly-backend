using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Reminders;

public class UpdateReminderSettingsRequest
{
    public bool Enabled { get; set; }

    // 60 = every hour, 120 = every 2 hours, 180 = every 3 hours, or any custom value.
    [Range(15, 720, ErrorMessage = "Hatırlatma aralığı 15-720 dakika arasında olmalıdır.")]
    public int IntervalMinutes { get; set; }

    [Required(ErrorMessage = "Başlangıç saati zorunludur.")]
    [RegularExpression(TimeFormat, ErrorMessage = "Saat HH:mm formatında olmalıdır, örneğin 09:00.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bitiş saati zorunludur.")]
    [RegularExpression(TimeFormat, ErrorMessage = "Saat HH:mm formatında olmalıdır, örneğin 23:00.")]
    public string EndTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Saat dilimi zorunludur.")]
    [StringLength(64)]
    public string TimeZone { get; set; } = string.Empty;

    [Range(50, 2000, ErrorMessage = "Varsayılan bardak 50-2000 ml arasında olmalıdır.")]
    public int DefaultGlassMl { get; set; }

    public bool SoundEnabled { get; set; }
    public bool VibrationEnabled { get; set; }

    private const string TimeFormat = @"^([01]\d|2[0-3]):[0-5]\d$";
}
