using System.Globalization;
using Waterly.Api.Models;
using Waterly.Api.Services;

namespace Waterly.Api.Helpers;

// One row per local day, aggregated in PostgreSQL from the user's eligible water intakes.
// Property names match the column aliases of the SQL query in AchievementService.
public class DailyIntakeAggregate
{
    public DateOnly Date { get; set; }
    public int TotalMl { get; set; }
    public int IntakeCount { get; set; }

    // Local hour (0-23) of the day's first habit-sized intake, or -1 if there is none.
    public int FirstHabitHour { get; set; }
    public bool Noon { get; set; }
    public bool Evening { get; set; }
    public bool SlotMorning { get; set; }
    public bool SlotAfternoon { get; set; }
    public bool SlotEvening { get; set; }
    public bool FromReminder { get; set; }
}

public sealed record GoalSnapshot(int TargetMl, DateOnly EffectiveFrom, DateTimeOffset CreatedAt);

public sealed record AchievementEventSnapshot(string EventType, DateOnly LocalDate, string? EventValue, string? EventMetadata);

public sealed record ReminderSnapshot(
    bool Enabled, int IntervalMinutes, TimeOnly StartTime, TimeOnly EndTime,
    int DefaultGlassMl, bool SoundEnabled, bool VibrationEnabled, DateTimeOffset UpdatedAt);

public sealed class AchievementMetricsInput
{
    public DateOnly Today { get; init; }
    public DateOnly RegistrationDate { get; init; }
    public DateTimeOffset UserCreatedAt { get; init; }

    // Ordered by EffectiveFrom, then CreatedAt (same order GoalService uses).
    public IReadOnlyList<GoalSnapshot> Goals { get; init; } = [];
    public IReadOnlyList<DailyIntakeAggregate> Days { get; init; } = [];
    public int DistinctIntakeTimeZones { get; init; }

    // Local days on which intakes were *recorded* (server CreatedAt), not the day they were consumed.
    public IReadOnlyCollection<DateOnly> IntakeRecordedDates { get; init; } = [];
    public IReadOnlyList<AchievementEventSnapshot> Events { get; init; } = [];
    public ReminderSnapshot? Reminder { get; init; }
    public bool HealthConnected { get; init; }
}

public sealed class AchievementMetrics
{
    private readonly Dictionary<AchievementMetric, long> _values = new();

    // Shown as progress for locked streak achievements. Unlocking uses the longest streak.
    public long CurrentStreak { get; set; }

    // Latest active local day (session, app open or recorded water). Stored on UserProgress.
    public DateOnly? LastActiveDate { get; set; }

    public long this[AchievementMetric metric]
    {
        get => _values.GetValueOrDefault(metric);
        set => _values[metric] = value;
    }
}

// Turns stored data into achievement metric values. Pure and deterministic: no database, no clock.
public static class AchievementMetricsCalculator
{
    // Time windows (local wall clock, inclusive hours). Nothing after 22:00 is rewarded.
    private const int MorningFromHour = 7, MorningToHour = 9;      // 07:00-09:59
    private const int ReturnGapDays = 14;                          // full inactive days before a "return"

    public static AchievementMetrics Calculate(AchievementMetricsInput input)
    {
        var metrics = new AchievementMetrics
        {
            // Every evaluation runs inside an authenticated session (register, login, refresh or an API call).
            [AchievementMetric.SessionStarted] = 1
        };

        CalculateWaterMetrics(input, metrics);
        CalculateActivityMetrics(input, metrics);
        CalculateSettingsMetrics(input, metrics);
        CalculateEventMetrics(input, metrics);

        return metrics;
    }

    public static int GoalFor(IReadOnlyList<GoalSnapshot> goals, DateOnly day)
    {
        if (goals.Count == 0)
        {
            return GoalService.DefaultTargetMl;
        }

        // Same rule as GoalService: the newest goal whose EffectiveFrom <= day; days before the first goal use the first goal.
        GoalSnapshot? match = null;
        foreach (var goal in goals)
        {
            if (goal.EffectiveFrom <= day)
            {
                match = goal;
            }
        }

        return (match ?? goals[0]).TargetMl;
    }

    public static string IsoWeekKey(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(dateTime)}-W{ISOWeek.GetWeekOfYear(dateTime):D2}";
    }

    private static void CalculateWaterMetrics(AchievementMetricsInput input, AchievementMetrics metrics)
    {
        long cappedTotalMl = 0, intakeCount = 0;
        decimal bestPercentage = 0;
        int morningDays = 0, noonDays = 0, eveningDays = 0, balancedDays = 0, reminderDays = 0, registrationDayMl = 0;
        var completedDates = new List<DateOnly>();

        foreach (var day in input.Days.OrderBy(d => d.Date))
        {
            var goalMl = GoalFor(input.Goals, day.Date);

            // Anti-farming: a day counts at most up to that day's goal. Real consumption data is not changed.
            var countedMl = Math.Min(day.TotalMl, goalMl);
            cappedTotalMl += countedMl;
            intakeCount += day.IntakeCount;

            if (goalMl > 0)
            {
                bestPercentage = Math.Max(bestPercentage, Math.Min(100m, day.TotalMl * 100m / goalMl));
            }

            if (day.TotalMl >= goalMl)
            {
                completedDates.Add(day.Date);
            }

            if (day.Date == input.RegistrationDate)
            {
                registrationDayMl = countedMl;
            }

            if (day.FirstHabitHour is >= MorningFromHour and <= MorningToHour) morningDays++;
            if (day.Noon) noonDays++;
            if (day.Evening) eveningDays++;
            if (day.SlotMorning && day.SlotAfternoon && day.SlotEvening) balancedDays++;
            if (day.FromReminder) reminderDays++;
        }

        metrics[AchievementMetric.IntakeCount] = intakeCount;
        metrics[AchievementMetric.RegistrationDayMl] = registrationDayMl;
        metrics[AchievementMetric.BestDailyGoalPercentage] = (long)Math.Floor(bestPercentage);
        metrics[AchievementMetric.GoalCompletedDays] = completedDates.Count;
        metrics[AchievementMetric.CappedTotalMl] = cappedTotalMl;
        metrics[AchievementMetric.GlassEquivalents] = cappedTotalMl / AchievementService.GlassEquivalentMl;
        metrics[AchievementMetric.MorningStartDays] = morningDays;
        metrics[AchievementMetric.NoonDays] = noonDays;
        metrics[AchievementMetric.EveningDays] = eveningDays;
        metrics[AchievementMetric.BalancedDays] = balancedDays;
        metrics[AchievementMetric.ReminderActionDays] = reminderDays;
        metrics[AchievementMetric.TimeZonesUsed] = input.DistinctIntakeTimeZones;

        CalculateStreaks(completedDates, input.Today, metrics);
    }

    // Same rules as StatisticsService.GetStreakAsync: an unfinished today does not break the current streak.
    // Unlocking uses the longest streak ever, so reaching 30 days at once also unlocks 2, 3, 7, 10, 14 and 21.
    private static void CalculateStreaks(List<DateOnly> completedDates, DateOnly today, AchievementMetrics metrics)
    {
        int longest = 0, running = 0;
        var secondChance = false;
        DateOnly? previous = null;

        foreach (var date in completedDates)
        {
            if (previous is { } prev && date.DayNumber - prev.DayNumber == 1)
            {
                running++;
            }
            else
            {
                // A gap of at least one full day without the goal, followed by a completed day.
                if (previous is { } earlier && date.DayNumber - earlier.DayNumber >= 2)
                {
                    secondChance = true;
                }

                running = 1;
            }

            longest = Math.Max(longest, running);
            previous = date;
        }

        var completed = completedDates.ToHashSet();
        var cursor = completed.Contains(today) ? today : today.AddDays(-1);
        var current = 0;
        while (completed.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        metrics[AchievementMetric.StreakDays] = longest;
        metrics[AchievementMetric.SecondChance] = secondChance ? 1 : 0;
        metrics.CurrentStreak = current;
    }

    // An active day is a day with a successful session, an app open, or a water intake recorded on that day.
    // Opening the app 100 times on one day is still one active day.
    private static void CalculateActivityMetrics(AchievementMetricsInput input, AchievementMetrics metrics)
    {
        var activeDays = new SortedSet<DateOnly>(input.IntakeRecordedDates);
        foreach (var e in input.Events)
        {
            if (e.EventType is AchievementEventTypes.SessionStarted or AchievementEventTypes.AppOpened)
            {
                activeDays.Add(e.LocalDate);
            }
        }

        int longestGap = 0, bestComeback = 0, comebackRun = 0;
        DateOnly? previous = null;
        foreach (var day in activeDays)
        {
            if (previous is { } prev)
            {
                // Full calendar days with no activity between the two active days.
                var gap = day.DayNumber - prev.DayNumber - 1;
                longestGap = Math.Max(longestGap, gap);

                if (gap >= ReturnGapDays)
                {
                    comebackRun = 1; // the return day itself
                }
                else if (comebackRun > 0)
                {
                    comebackRun++;
                }

                bestComeback = Math.Max(bestComeback, comebackRun);
            }

            previous = day;
        }

        metrics[AchievementMetric.ActiveDays] = activeDays.Count;
        metrics.LastActiveDate = activeDays.Count > 0 ? activeDays.Max : null;
        metrics[AchievementMetric.LongestInactiveGapDays] = longestGap;
        metrics[AchievementMetric.ComebackActiveDays] = bestComeback;
    }

    private static void CalculateSettingsMetrics(AchievementMetricsInput input, AchievementMetrics metrics)
    {
        // A goal is "customised" when some goal row has a different target than the one before it.
        var goalChanged = false;
        GoalSnapshot? previousGoal = null;
        foreach (var goal in input.Goals.OrderBy(g => g.CreatedAt))
        {
            if (previousGoal is not null && goal.TargetMl != previousGoal.TargetMl)
            {
                goalChanged = true;
                break;
            }

            previousGoal = goal;
        }

        metrics[AchievementMetric.GoalCustomized] = goalChanged ? 1 : 0;
        metrics[AchievementMetric.HealthConnected] = input.HealthConnected ? 1 : 0;

        if (input.Reminder is { } reminder)
        {
            var defaults = ReminderService.CreateDefaultSettings(Guid.Empty, DateTimeHelper.DefaultTimeZone, input.UserCreatedAt);

            // Registration writes UpdatedAt == user CreatedAt; any later save means the user stored their own settings.
            metrics[AchievementMetric.ReminderSettingsSaved] = reminder.UpdatedAt > input.UserCreatedAt ? 1 : 0;

            var differsFromDefault = reminder.Enabled != defaults.Enabled
                                     || reminder.IntervalMinutes != defaults.IntervalMinutes
                                     || reminder.StartTime != defaults.StartTime
                                     || reminder.EndTime != defaults.EndTime
                                     || reminder.SoundEnabled != defaults.SoundEnabled
                                     || reminder.VibrationEnabled != defaults.VibrationEnabled;
            metrics[AchievementMetric.ReminderCustomized] = differsFromDefault ? 1 : 0;
            metrics[AchievementMetric.GlassSizeCustomized] = reminder.DefaultGlassMl != defaults.DefaultGlassMl ? 1 : 0;
        }
    }

    private static void CalculateEventMetrics(AchievementMetricsInput input, AchievementMetrics metrics)
    {
        var byType = input.Events.ToLookup(e => e.EventType);

        long Any(string type) => byType[type].Any() ? 1 : 0;

        metrics[AchievementMetric.OnboardingCompleted] = Any(AchievementEventTypes.OnboardingCompleted);
        metrics[AchievementMetric.StatisticsViewed] = Any(AchievementEventTypes.StatisticsOpened);
        metrics[AchievementMetric.HistoryViewed] = Any(AchievementEventTypes.HistoryOpened);
        metrics[AchievementMetric.MonthlyReportViewed] = Any(AchievementEventTypes.MonthlyReportOpened);
        metrics[AchievementMetric.CalendarDayViewed] = Any(AchievementEventTypes.CalendarDayViewed);
        metrics[AchievementMetric.HealthRecommendationViewed] = Any(AchievementEventTypes.HealthRecommendationViewed);
        metrics[AchievementMetric.DarkThemeEnabled] = Any(AchievementEventTypes.DarkThemeEnabled);
        metrics[AchievementMetric.BottleEasterEggDiscovered] = Any(AchievementEventTypes.BottleEasterEggDiscovered);

        // Distinct ISO calendar weeks (server-derived metadata), not the number of times the report was opened.
        metrics[AchievementMetric.WeeklyReportWeeks] = byType[AchievementEventTypes.WeeklyReportOpened]
            .Select(e => e.EventMetadata ?? IsoWeekKey(e.LocalDate))
            .Distinct()
            .Count();

        // Distinct themes; using the same theme again never counts twice.
        metrics[AchievementMetric.BottleThemesUsed] = byType[AchievementEventTypes.BottleThemeUsed]
            .Select(e => e.EventValue)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .Count();
    }
}
