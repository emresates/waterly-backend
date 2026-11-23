using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class StreakTests(DatabaseFixture database)
{
    // "Today" for these tests is 2026-11-23 in Istanbul (UTC+3).
    private static readonly DateTimeOffset Today = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset NoonDaysAgo(int days) => new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero).AddDays(-days);

    [Fact]
    public async Task UnfinishedToday_DoesNotResetTheStreak()
    {
        await using var context = new ServiceTestContext(database, Today.AddDays(-10));
        var userId = await context.RegisterUserAsync();
        context.Clock.Now = Today;

        for (var daysAgo = 5; daysAgo >= 1; daysAgo--)
        {
            await context.AddIntakeAsync(userId, 2000, NoonDaysAgo(daysAgo));
        }
        await context.AddIntakeAsync(userId, 500);

        var streak = await context.Statistics.GetStreakAsync(userId, CancellationToken.None);
        Assert.Equal(5, streak.CurrentStreak);
        Assert.False(streak.TodayCompleted);

        await context.AddIntakeAsync(userId, 1500);

        streak = await context.Statistics.GetStreakAsync(userId, CancellationToken.None);
        Assert.Equal(6, streak.CurrentStreak);
        Assert.Equal(6, streak.LongestStreak);
        Assert.True(streak.TodayCompleted);
    }

    [Fact]
    public async Task MissedDay_BreaksTheCurrentStreak_ButLongestIsKept()
    {
        await using var context = new ServiceTestContext(database, Today.AddDays(-10));
        var userId = await context.RegisterUserAsync();
        context.Clock.Now = Today;

        foreach (var daysAgo in new[] { 9, 8, 7, 2, 1 })
        {
            await context.AddIntakeAsync(userId, 2100, NoonDaysAgo(daysAgo));
        }
        await context.AddIntakeAsync(userId, 1000, NoonDaysAgo(6)); // not enough, breaks the first run

        var streak = await context.Statistics.GetStreakAsync(userId, CancellationToken.None);
        Assert.Equal(2, streak.CurrentStreak);
        Assert.Equal(3, streak.LongestStreak);
        Assert.Equal(new DateOnly(2026, 11, 22), streak.LastCompletedDate);
    }

    [Fact]
    public async Task MissingYesterday_MeansCurrentStreakIsZero()
    {
        await using var context = new ServiceTestContext(database, Today.AddDays(-5));
        var userId = await context.RegisterUserAsync();
        context.Clock.Now = Today;

        await context.AddIntakeAsync(userId, 2000, NoonDaysAgo(3));
        await context.AddIntakeAsync(userId, 2000, NoonDaysAgo(2));

        var streak = await context.Statistics.GetStreakAsync(userId, CancellationToken.None);
        Assert.Equal(0, streak.CurrentStreak);
        Assert.Equal(2, streak.LongestStreak);
    }
}
