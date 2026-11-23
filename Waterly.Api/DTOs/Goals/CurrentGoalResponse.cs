using Waterly.Api.DTOs.Hydration;

namespace Waterly.Api.DTOs.Goals;

public class CurrentGoalResponse
{
    public int TargetMl { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DailyProgressResponse Today { get; init; } = null!;
    public string Note { get; init; } =
        "Varsayılan hedef genel bir başlangıç değeridir; herkes için tıbbi olarak doğru kabul edilmemelidir. Hedefinizi ihtiyacınıza göre değiştirebilirsiniz.";
}
