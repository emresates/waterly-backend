using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.DTOs.Statistics;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;

namespace Waterly.Api.Services;

// Statistics are calculated directly from the database on every request. No cache is needed at this scale.
public class StatisticsService(
    AppDbContext db,
    IHydrationService hydrationService,
    IGoalService goalService,
    IUserService userService,
    TimeProvider timeProvider) : IStatisticsService
{
    public async Task<DailyStatisticsResponse> GetDailyAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken)
    {
        var day = date ?? await GetTodayAsync(userId, cancellationToken);

        var intakes = await db.WaterIntakes
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.LocalDate == day)
            .Select(i => new { i.AmountMl, i.ConsumedAt, i.TimeZone })
            .ToListAsync(cancellationToken);

        var hourly = Enumerable.Range(0, 24)
            .Select(hour => new HourlyConsumption { Hour = hour })
            .ToList();

        foreach (var intake in intakes)
        {
            var localHour = DateTimeHelper.ToLocalTime(intake.ConsumedAt, intake.TimeZone).Hour;
            hourly[localHour] = new HourlyConsumption
            {
                Hour = localHour,
                AmountMl = hourly[localHour].AmountMl + intake.AmountMl,
                IntakeCount = hourly[localHour].IntakeCount + 1
            };
        }

        return new DailyStatisticsResponse
        {
            Progress = await goalService.GetProgressForDateAsync(userId, day, cancellationToken),
            HourlyDistribution = hourly
        };
    }

    public async Task<PeriodStatisticsResponse> GetWeeklyAsync(Guid userId, DateOnly? endDate, CancellationToken cancellationToken)
    {
        var end = endDate ?? await GetTodayAsync(userId, cancellationToken);
        return await BuildPeriodStatisticsAsync(userId, end.AddDays(-6), end, cancellationToken);
    }

    public async Task<PeriodStatisticsResponse> GetMonthlyAsync(Guid userId, MonthlyStatisticsQuery query, CancellationToken cancellationToken)
    {
        if (query.Year.HasValue != query.Month.HasValue)
        {
            throw AppException.BadRequest(ErrorCodes.ValidationFailed, "Takvim ayı için year ve month birlikte gönderilmelidir.");
        }

        if (query is { Year: { } year, Month: { } month })
        {
            var firstDay = new DateOnly(year, month, 1);
            return await BuildPeriodStatisticsAsync(userId, firstDay, firstDay.AddMonths(1).AddDays(-1), cancellationToken);
        }

        var today = await GetTodayAsync(userId, cancellationToken);
        return await BuildPeriodStatisticsAsync(userId, today.AddDays(-29), today, cancellationToken);
    }

    public async Task<StreakResponse> GetStreakAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = await GetTodayAsync(userId, cancellationToken);
        var start = await hydrationService.GetTrackingStartDateAsync(userId, cancellationToken);
        if (start > today)
        {
            start = today;
        }

        // Oldest first, one entry per day, days without water included (they break a streak).
        var days = await hydrationService.GetDailyProgressRangeAsync(userId, start, today, cancellationToken);

        var longestStreak = 0;
        var runningStreak = 0;
        DateOnly? lastCompletedDate = null;
        foreach (var day in days)
        {
            if (day.GoalCompleted)
            {
                runningStreak++;
                longestStreak = Math.Max(longestStreak, runningStreak);
                lastCompletedDate = day.Date;
            }
            else
            {
                runningStreak = 0;
            }
        }

        // Today is still in progress: if the goal is not reached yet, the streak continues from yesterday
        // instead of dropping to zero. It only breaks once a whole day passes without reaching the goal.
        var todayCompleted = days[^1].GoalCompleted;
        var currentStreak = 0;
        var index = todayCompleted ? days.Count - 1 : days.Count - 2;
        while (index >= 0 && days[index].GoalCompleted)
        {
            currentStreak++;
            index--;
        }

        return new StreakResponse
        {
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            TodayCompleted = todayCompleted,
            LastCompletedDate = lastCompletedDate
        };
    }

    private async Task<PeriodStatisticsResponse> BuildPeriodStatisticsAsync(Guid userId, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var today = await GetTodayAsync(userId, cancellationToken);
        var trackingStart = await hydrationService.GetTrackingStartDateAsync(userId, cancellationToken);
        var days = await hydrationService.GetDailyProgressRangeAsync(userId, start, end, cancellationToken);

        // Future days and days before the user started would pull the averages down, so they are not counted.
        // They are still returned in "Days" so a chart always has a full week or month.
        var trackedDays = days.Where(d => d.Date <= today && d.Date >= trackingStart).ToList();

        var total = trackedDays.Sum(d => d.ConsumedMl);
        var completed = trackedDays.Count(d => d.GoalCompleted);

        return new PeriodStatisticsResponse
        {
            StartDate = start,
            EndDate = end,
            Days = days,
            TrackedDays = trackedDays.Count,
            TotalConsumedMl = total,
            AverageDailyMl = trackedDays.Count == 0 ? 0 : (int)Math.Round(total / (double)trackedDays.Count),
            CompletedDays = completed,
            SuccessRate = trackedDays.Count == 0 ? 0 : Math.Round(completed * 100m / trackedDays.Count, 1),
            HighestDay = trackedDays.MaxBy(d => d.ConsumedMl),
            LowestDay = trackedDays.MinBy(d => d.ConsumedMl)
        };
    }

    private async Task<DateOnly> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        return DateTimeHelper.GetToday(timeProvider, timeZone);
    }
}
