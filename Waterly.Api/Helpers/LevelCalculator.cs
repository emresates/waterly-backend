using Waterly.Api.DTOs.Achievements;

namespace Waterly.Api.Helpers;

// Total XP needed to reach a level: Threshold(level) = 5 * (level - 1)^2
//   Level 1 = 0 XP, Level 2 = 5 XP, Level 10 = 405 XP, Level 50 = 12005 XP, Level 100 = 49005 XP.
// The level is capped at 100; XP keeps growing after that.
public static class LevelCalculator
{
    public const int MaxLevel = 100;
    private const int Factor = 5;

    public static int ThresholdFor(int level)
    {
        var clamped = Math.Clamp(level, 1, MaxLevel);
        return Factor * (clamped - 1) * (clamped - 1);
    }

    public static int LevelFor(int totalXp)
    {
        if (totalXp <= 0)
        {
            return 1;
        }

        // Start from the square-root estimate, then correct it with integer math so rounding can never be off by one.
        var level = Math.Clamp((int)Math.Floor(Math.Sqrt(totalXp / (double)Factor)) + 1, 1, MaxLevel);
        while (level < MaxLevel && ThresholdFor(level + 1) <= totalXp)
        {
            level++;
        }

        while (level > 1 && ThresholdFor(level) > totalXp)
        {
            level--;
        }

        return level;
    }

    public static UserLevelResponse Build(int totalXp)
    {
        totalXp = Math.Max(0, totalXp);
        var level = LevelFor(totalXp);
        var currentLevelXp = ThresholdFor(level);

        if (level >= MaxLevel)
        {
            return new UserLevelResponse
            {
                Level = MaxLevel,
                TotalXp = totalXp,
                CurrentLevelXp = currentLevelXp,
                NextLevelXp = null,
                XpIntoLevel = totalXp - currentLevelXp,
                XpToNextLevel = 0,
                ProgressPercentage = 100m,
                IsMaxLevel = true
            };
        }

        var nextLevelXp = ThresholdFor(level + 1);
        var xpIntoLevel = totalXp - currentLevelXp;

        return new UserLevelResponse
        {
            Level = level,
            TotalXp = totalXp,
            CurrentLevelXp = currentLevelXp,
            NextLevelXp = nextLevelXp,
            XpIntoLevel = xpIntoLevel,
            XpToNextLevel = nextLevelXp - totalXp,
            ProgressPercentage = Math.Round(xpIntoLevel * 100m / (nextLevelXp - currentLevelXp), 1),
            IsMaxLevel = false
        };
    }
}
