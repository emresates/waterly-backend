using Microsoft.EntityFrameworkCore;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class HydrationTests(DatabaseFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero); // 13:00 in Istanbul

    [Fact]
    public async Task AddingWater_IncreasesTheDailyTotal()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        await context.AddIntakeAsync(userId, 250);
        var result = await context.AddIntakeAsync(userId, 500);

        Assert.False(result.AlreadyProcessed);
        Assert.Equal(750, result.Progress.ConsumedMl);
        Assert.Equal(2, result.Progress.IntakeCount);

        var today = await context.Hydration.GetTodayAsync(userId, CancellationToken.None);
        Assert.Equal(750, today.Progress.ConsumedMl);
        Assert.Equal(2, today.Intakes.Count);
    }

    [Fact]
    public async Task SameClientEventId_IsStoredOnlyOnce()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        var request = new AddIntakeRequest { AmountMl = 250, ClientEventId = Guid.NewGuid() };

        var first = await context.Hydration.AddIntakeAsync(userId, request, CancellationToken.None);
        var second = await context.Hydration.AddIntakeAsync(userId, request, CancellationToken.None);

        Assert.False(first.AlreadyProcessed);
        Assert.True(second.AlreadyProcessed);
        Assert.Equal(first.Intake.Id, second.Intake.Id);
        Assert.Equal(250, second.Progress.ConsumedMl);
        Assert.Equal(1, await context.Db.WaterIntakes.CountAsync(i => i.UserId == userId));
    }

    [Fact]
    public async Task OfflineSync_SkipsDuplicatesAndReportsInvalidItems()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        var alreadySent = Guid.NewGuid();
        await context.Hydration.AddIntakeAsync(userId, new AddIntakeRequest { AmountMl = 200, ClientEventId = alreadySent }, CancellationToken.None);

        var result = await context.Hydration.SyncIntakesAsync(userId, new SyncIntakesRequest
        {
            Intakes =
            [
                new AddIntakeRequest { AmountMl = 200, ClientEventId = alreadySent },
                new AddIntakeRequest { AmountMl = 330, ClientEventId = Guid.NewGuid(), ConsumedAt = Now.AddHours(-2) },
                new AddIntakeRequest { AmountMl = 0, ClientEventId = Guid.NewGuid() }
            ]
        }, CancellationToken.None);

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.AlreadyProcessedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(ErrorCodes.InvalidWaterAmount, result.Results[2].ErrorCode);
        Assert.Equal(530, result.Today.ConsumedMl);
    }

    [Theory]
    [InlineData(2000, 0, 0, 2000, false)]
    [InlineData(2000, 750, 37.5, 1250, false)]
    [InlineData(2000, 2000, 100, 0, true)]
    [InlineData(2000, 2500, 125, 0, true)]   // goal can be exceeded, remaining never goes negative
    [InlineData(3000, 1000, 33.3, 2000, false)]
    public void ProgressPercentage_IsCalculatedCorrectly(int goal, int consumed, double expectedPercentage, int expectedRemaining, bool expectedCompleted)
    {
        var progress = ProgressCalculator.Calculate(new DateOnly(2026, 11, 23), goal, consumed, 1);

        Assert.Equal((decimal)expectedPercentage, progress.ProgressPercentage);
        Assert.Equal(expectedRemaining, progress.RemainingMl);
        Assert.Equal(expectedCompleted, progress.GoalCompleted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(5001)]
    public async Task InvalidAmounts_AreRejected(int amountMl)
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() => context.AddIntakeAsync(userId, amountMl));
        Assert.Equal(ErrorCodes.InvalidWaterAmount, exception.ErrorCode);
    }

    [Fact]
    public async Task FutureIntakes_AreRejected_ButPastDaysAreAllowed()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() => context.AddIntakeAsync(userId, 250, Now.AddHours(1)));
        Assert.Equal(ErrorCodes.InvalidConsumedAt, exception.ErrorCode);

        var pastDay = await context.AddIntakeAsync(userId, 250, Now.AddDays(-3));
        Assert.Equal(new DateOnly(2026, 11, 20), pastDay.Intake.LocalDate);
    }

    [Fact]
    public async Task EditingAnIntake_MovesItToTheNewDay()
    {
        await using var context = new ServiceTestContext(database, Now);
        var userId = await context.RegisterUserAsync();
        var added = await context.AddIntakeAsync(userId, 250);

        var updated = await context.Hydration.UpdateIntakeAsync(userId, added.Intake.Id,
            new UpdateIntakeRequest { AmountMl = 400, ConsumedAt = Now.AddDays(-1) }, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 22), updated.Intake.LocalDate);
        Assert.Equal(400, updated.Progress.ConsumedMl);
        var today = await context.Goals.GetProgressForDateAsync(userId, new DateOnly(2026, 11, 23), CancellationToken.None);
        Assert.Equal(0, today.ConsumedMl);
    }
}
