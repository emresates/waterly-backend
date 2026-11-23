using System.ComponentModel.DataAnnotations;
using Waterly.Api.Models;

namespace Waterly.Api.DTOs.Health;

public class ActivityDayRequest
{
    [Required(ErrorMessage = "Tarih zorunludur.")]
    public DateOnly? Date { get; set; }

    [Range(0, 200000, ErrorMessage = "Adım sayısı 0-200000 arasında olmalıdır.")]
    public int Steps { get; set; }

    [Range(0, 1440, ErrorMessage = "Egzersiz süresi 0-1440 dakika arasında olmalıdır.")]
    public int ExerciseMinutes { get; set; }

    [Range(0, 20000, ErrorMessage = "Aktif kalori 0-20000 arasında olmalıdır.")]
    public decimal? ActiveCalories { get; set; }
}

// The phone reads HealthKit / Health Connect and uploads daily summaries (e.g. the last 7 days).
public class SaveActivityRequest
{
    [Required(ErrorMessage = "Veri kaynağı zorunludur.")]
    public ActivitySource? Source { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "En az bir gün gönderilmelidir.")]
    [MaxLength(31, ErrorMessage = "Tek seferde en fazla 31 gün gönderilebilir.")]
    public List<ActivityDayRequest> Days { get; set; } = [];
}
