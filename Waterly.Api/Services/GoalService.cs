using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Goals;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;

namespace Waterly.Api.Services;

// How goal history works:
// Each change inserts a new HydrationGoal row with EffectiveFrom = the user's local "today".
// The goal of any day D is the newest row whose EffectiveFrom <= D.
// So changing the goal today never changes the result of yesterday.
public class GoalService(AppDbContext db, IUserService userService, IAchievementService achievementService, TimeProvider timeProvider) : IGoalService
{
    // A general starting point only, not a medical recommendation. Users can change it at any time.
    public const int DefaultTargetMl = 2000;

    public async Task<CurrentGoalResponse> GetCurrentGoalAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = await GetTodayAsync(userId, cancellationToken);
        var goal = await FindGoalForDateAsync(userId, today, cancellationToken) ?? throw GoalNotFound();

        return new CurrentGoalResponse
        {
            TargetMl = goal.TargetMl,
            EffectiveFrom = goal.EffectiveFrom,
            Today = await GetProgressForDateAsync(userId, today, cancellationToken)
        };
    }

    public async Task<CurrentGoalResponse> UpdateCurrentGoalAsync(Guid userId, UpdateGoalRequest request, CancellationToken cancellationToken)
    {
        var today = await GetTodayAsync(userId, cancellationToken);

        db.HydrationGoals.Add(new HydrationGoal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetMl = request.TargetMl,
            EffectiveFrom = today,
            CreatedAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);

        // "Kendi Yolum" and, with a lower goal, today's goal achievements. Past days keep their own goal.
        await achievementService.TryEvaluateAchievementsAsync(userId, cancellationToken);

        return await GetCurrentGoalAsync(userId, cancellationToken);
    }

    public async Task<List<GoalHistoryItemResponse>> GetGoalHistoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.HydrationGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.EffectiveFrom)
            .ThenByDescending(g => g.CreatedAt)
            .Select(g => new GoalHistoryItemResponse
            {
                Id = g.Id,
                TargetMl = g.TargetMl,
                EffectiveFrom = g.EffectiveFrom,
                CreatedAt = g.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetGoalForDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var goal = await FindGoalForDateAsync(userId, date, cancellationToken) ?? throw GoalNotFound();
        return goal.TargetMl;
    }

    public async Task<Dictionary<DateOnly, int>> GetGoalsForRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // A user only has a handful of goal rows, so loading them all and matching in memory is simple and fast.
        var goals = await db.HydrationGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.EffectiveFrom)
            .ThenBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        if (goals.Count == 0)
        {
            throw GoalNotFound();
        }

        var result = new Dictionary<DateOnly, int>();
        foreach (var day in DateTimeHelper.EachDay(from, to))
        {
            // Days before the first goal (e.g. back-dated intakes) use the first goal.
            var goal = goals.LastOrDefault(g => g.EffectiveFrom <= day) ?? goals[0];
            result[day] = goal.TargetMl;
        }

        return result;
    }

    public async Task<DailyProgressResponse> GetProgressForDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var goalMl = await GetGoalForDateAsync(userId, date, cancellationToken);

        var amounts = await db.WaterIntakes
            .Where(i => i.UserId == userId && i.LocalDate == date)
            .Select(i => i.AmountMl)
            .ToListAsync(cancellationToken);

        return ProgressCalculator.Calculate(date, goalMl, amounts.Sum(), amounts.Count);
    }

    private async Task<HydrationGoal?> FindGoalForDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var goal = await db.HydrationGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.EffectiveFrom <= date)
            .OrderByDescending(g => g.EffectiveFrom)
            .ThenByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Fallback for days before the first goal existed.
        return goal ?? await db.HydrationGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.EffectiveFrom)
            .ThenBy(g => g.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DateOnly> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        return DateTimeHelper.GetToday(timeProvider, timeZone);
    }

    private static AppException GoalNotFound() =>
        AppException.NotFound(ErrorCodes.GoalNotFound, "Günlük hedef bulunamadı.");
}
