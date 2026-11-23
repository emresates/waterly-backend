namespace Waterly.Api.DTOs.Health;

public enum RecommendationStatus
{
    // No activity data for the day. The app works normally without health data.
    NoData,
    // Not enough earlier days to know what is "normal" for this user.
    InsufficientHistory,
    Normal,
    ElevatedActivity
}

public class RecommendationResponse
{
    public DateOnly Date { get; init; }
    public RecommendationStatus Status { get; init; }

    // Only a suggestion. The goal is never changed automatically.
    public bool ShouldReviewGoal { get; init; }
    public int CurrentGoalMl { get; init; }

    public int? Steps { get; init; }
    public int? BaselineSteps { get; init; }
    public int? ExerciseMinutes { get; init; }
    public int? BaselineExerciseMinutes { get; init; }

    // Human readable explanation of why this result was produced.
    public List<string> Reasons { get; init; } = [];
    public string Message { get; init; } = string.Empty;
    public string Disclaimer { get; init; } =
        "Bu bilgi tıbbi tavsiye değildir. Su ihtiyacı kişiden kişiye değişir; sağlık durumunuzla ilgili sorular için bir sağlık uzmanına danışın.";
}
