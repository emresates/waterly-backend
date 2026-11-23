using Waterly.Api.DTOs.Goals;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class GoalHistoryTests(DatabaseFixture database)
{
    private static readonly DateTimeOffset Today = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ChangingTheGoal_DoesNotAffectPastDays()
    {
        await using var context = new ServiceTestContext(database, Today.AddDays(-3));
        var userId = await context.RegisterUserAsync();

        context.Clock.Now = Today;
        await context.AddIntakeAsync(userId, 2000, Today.AddDays(-2));
        await context.AddIntakeAsync(userId, 2000);

        var updated = await context.Goals.UpdateCurrentGoalAsync(userId, new UpdateGoalRequest { TargetMl = 3000 }, CancellationToken.None);
        Assert.Equal(3000, updated.TargetMl);
        Assert.Equal(new DateOnly(2026, 11, 23), updated.EffectiveFrom);

        var pastDay = await context.Goals.GetProgressForDateAsync(userId, new DateOnly(2026, 11, 21), CancellationToken.None);
        Assert.Equal(2000, pastDay.DailyGoalMl);
        Assert.True(pastDay.GoalCompleted);

        var today = await context.Goals.GetProgressForDateAsync(userId, new DateOnly(2026, 11, 23), CancellationToken.None);
        Assert.Equal(3000, today.DailyGoalMl);
        Assert.False(today.GoalCompleted);
        Assert.Equal(66.7m, today.ProgressPercentage);

        var weekly = await context.Statistics.GetWeeklyAsync(userId, null, CancellationToken.None);
        Assert.Equal(2000, weekly.Days.Single(d => d.Date == new DateOnly(2026, 11, 21)).DailyGoalMl);
        Assert.Equal(3000, weekly.Days.Single(d => d.Date == new DateOnly(2026, 11, 23)).DailyGoalMl);
        Assert.Equal(1, weekly.CompletedDays);

        var history = await context.Goals.GetGoalHistoryAsync(userId, CancellationToken.None);
        Assert.Equal([3000, 2000], history.Select(h => h.TargetMl));
    }

    [Fact]
    public async Task ChangingTheGoalTwiceOnTheSameDay_UsesTheLatestValue()
    {
        await using var context = new ServiceTestContext(database, Today);
        var userId = await context.RegisterUserAsync();

        await context.Goals.UpdateCurrentGoalAsync(userId, new UpdateGoalRequest { TargetMl = 2500 }, CancellationToken.None);
        context.Clock.Now = Today.AddMinutes(5);
        var current = await context.Goals.UpdateCurrentGoalAsync(userId, new UpdateGoalRequest { TargetMl = 1800 }, CancellationToken.None);

        Assert.Equal(1800, current.TargetMl);
        Assert.Equal(1800, await context.Goals.GetGoalForDateAsync(userId, new DateOnly(2026, 11, 23), CancellationToken.None));
    }
}
