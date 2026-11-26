using Waterly.Api.DTOs.Achievements;

namespace Waterly.Api.Helpers;

// Total XP needed to reach a level: Threshold(level) = round(60 * (level - 1)^1.5)
//   Level 1 = 0 XP, Level 2 = 60 XP, Level 5 = 480 XP, Level 10 = 1620 XP, Level 20 = 4969 XP,
//   Level 50 = 20580 XP, Level 100 = 59102 XP.
// Steeper at the start than the old 5 * (level - 1)^2 curve, so the easy first-day achievements no
// longer jump a new user to level 12, while level 100 stays reachable (the whole catalog is 74965 XP).
// The level is capped at 100; XP keeps growing after that.
public static class LevelCalculator
{
    public const int MaxLevel = 100;
    private const double Factor = 60;
    private const double Exponent = 1.5;

    public static int ThresholdFor(int level)
    {
        var clamped = Math.Clamp(level, 1, MaxLevel);
        return (int)Math.Round(Factor * Math.Pow(clamped - 1, Exponent), MidpointRounding.AwayFromZero);
    }

    public static int LevelFor(int totalXp)
    {
        if (totalXp <= 0)
        {
            return 1;
        }

        // Start from the inverse-formula estimate, then correct it against the exact integer thresholds so
        // floating-point rounding can never be off by one.
        var level = Math.Clamp((int)Math.Floor(Math.Pow(totalXp / Factor, 1 / Exponent)) + 1, 1, MaxLevel);
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
