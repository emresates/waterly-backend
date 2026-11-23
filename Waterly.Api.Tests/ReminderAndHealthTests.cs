using Waterly.Api.DTOs.Health;
using Waterly.Api.Models;
using Waterly.Api.Services;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class ReminderAndHealthTests(DatabaseFixture database)
{
    [Fact]
    public void ReminderTimes_AreBuiltFromTheWindowAndInterval()
    {
        var settings = ReminderService.CreateDefaultSettings(Guid.NewGuid(), "Europe/Istanbul", DateTimeOffset.UtcNow);
        settings.StartTime = new TimeOnly(9, 0);
        settings.EndTime = new TimeOnly(23, 0);
        settings.IntervalMinutes = 120;

        Assert.Equal(["09:00", "11:00", "13:00", "15:00", "17:00", "19:00", "21:00", "23:00"], ReminderService.BuildDailyReminderTimes(settings));

        settings.StartTime = new TimeOnly(22, 0);
        settings.EndTime = new TimeOnly(1, 0);
        settings.IntervalMinutes = 60;
        Assert.Equal(["22:00", "23:00", "00:00", "01:00"], ReminderService.BuildDailyReminderTimes(settings));

        settings.Enabled = false;
        Assert.Empty(ReminderService.BuildDailyReminderTimes(settings));
    }

    [Fact]
    public async Task Recommendation_DetectsHigherActivity_WithoutChangingTheGoal()
    {
        var now = new DateTimeOffset(2026, 11, 23, 15, 0, 0, TimeSpan.Zero);
        await using var context = new ServiceTestContext(database, now);
        var userId = await context.RegisterUserAsync();

        var noData = await context.Health.GetRecommendationAsync(userId, null, CancellationToken.None);
        Assert.Equal(RecommendationStatus.NoData, noData.Status);

        await context.Health.SaveActivityAsync(userId, new SaveActivityRequest
        {
            Source = ActivitySource.AppleHealth,
            Days =
            [
                new ActivityDayRequest { Date = new DateOnly(2026, 11, 20), Steps = 5000, ExerciseMinutes = 10 },
                new ActivityDayRequest { Date = new DateOnly(2026, 11, 21), Steps = 6000, ExerciseMinutes = 15 },
                new ActivityDayRequest { Date = new DateOnly(2026, 11, 22), Steps = 5500, ExerciseMinutes = 5 },
                new ActivityDayRequest { Date = new DateOnly(2026, 11, 23), Steps = 7000, ExerciseMinutes = 10 }
            ]
        }, CancellationToken.None);

        var normal = await context.Health.GetRecommendationAsync(userId, null, CancellationToken.None);
        Assert.Equal(RecommendationStatus.Normal, normal.Status);
        Assert.False(normal.ShouldReviewGoal);

        // Re-sending the same day updates the existing row instead of creating a new one.
        var saved = await context.Health.SaveActivityAsync(userId, new SaveActivityRequest
        {
            Source = ActivitySource.AppleHealth,
            Days = [new ActivityDayRequest { Date = new DateOnly(2026, 11, 23), Steps = 14000, ExerciseMinutes = 60 }]
        }, CancellationToken.None);
        Assert.Single(saved);
        Assert.Equal(4, context.Db.DailyActivities.Count(a => a.UserId == userId));

        var elevated = await context.Health.GetRecommendationAsync(userId, null, CancellationToken.None);
        Assert.Equal(RecommendationStatus.ElevatedActivity, elevated.Status);
        Assert.True(elevated.ShouldReviewGoal);
        Assert.Equal(2, elevated.Reasons.Count);
        Assert.Equal(2000, await context.Goals.GetGoalForDateAsync(userId, new DateOnly(2026, 11, 23), CancellationToken.None));
    }
}
