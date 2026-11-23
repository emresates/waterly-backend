using Waterly.Api.DTOs.Hydration;

namespace Waterly.Api.Helpers;

public static class ProgressCalculator
{
    public static DailyProgressResponse Calculate(DateOnly date, int goalMl, int consumedMl, int intakeCount)
    {
        var percentage = goalMl > 0 ? Math.Round(consumedMl * 100m / goalMl, 1) : 0m;

        return new DailyProgressResponse
        {
            Date = date,
            DailyGoalMl = goalMl,
            ConsumedMl = consumedMl,
            // The goal can be exceeded (percentage > 100) but the remaining amount never goes below zero.
            RemainingMl = Math.Max(0, goalMl - consumedMl),
            ProgressPercentage = percentage,
            GoalCompleted = consumedMl >= goalMl,
            IntakeCount = intakeCount
        };
    }
}
