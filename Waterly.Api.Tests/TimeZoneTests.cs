using Waterly.Api.DTOs.Users;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class TimeZoneTests(DatabaseFixture database)
{
    [Fact]
    public async Task Istanbul_MidnightBoundary_SplitsDaysByLocalTime()
    {
        // Istanbul is UTC+3 all year. Local midnight of 2026-11-23 is 2026-11-22 21:00 UTC.
        await using var context = new ServiceTestContext(database, new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
        var userId = await context.RegisterUserAsync("Europe/Istanbul");

        var beforeMidnight = await context.AddIntakeAsync(userId, 300, new DateTimeOffset(2026, 11, 22, 20, 59, 59, TimeSpan.Zero));
        var atMidnight = await context.AddIntakeAsync(userId, 500, new DateTimeOffset(2026, 11, 22, 21, 0, 0, TimeSpan.Zero));
        var sentWithOffset = await context.AddIntakeAsync(userId, 200, new DateTimeOffset(2026, 11, 23, 14, 30, 0, TimeSpan.FromHours(3)));

        Assert.Equal(new DateOnly(2026, 11, 22), beforeMidnight.Intake.LocalDate);
        Assert.Equal(new DateOnly(2026, 11, 23), atMidnight.Intake.LocalDate);
        Assert.Equal(new DateOnly(2026, 11, 23), sentWithOffset.Intake.LocalDate);
        Assert.Equal(TimeSpan.Zero, sentWithOffset.Intake.ConsumedAt.Offset); // stored as UTC

        var today = await context.Hydration.GetTodayAsync(userId, CancellationToken.None);
        Assert.Equal(700, today.Progress.ConsumedMl);
    }

    [Fact]
    public async Task NewYork_DaylightSavingDay_UsesCorrectDayBoundaries()
    {
        // On 2026-03-08 New York switches from EST (UTC-5) to EDT (UTC-4): that local day is only 23 hours long.
        await using var context = new ServiceTestContext(database, new DateTimeOffset(2026, 3, 9, 15, 0, 0, TimeSpan.Zero));
        var userId = await context.RegisterUserAsync("America/New_York");

        var lastMinuteOfMarch7 = await context.AddIntakeAsync(userId, 100, new DateTimeOffset(2026, 3, 8, 4, 59, 0, TimeSpan.Zero));
        var firstMinuteOfMarch8 = await context.AddIntakeAsync(userId, 200, new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero));
        var lastMinuteOfMarch8 = await context.AddIntakeAsync(userId, 300, new DateTimeOffset(2026, 3, 9, 3, 59, 0, TimeSpan.Zero));
        var firstMinuteOfMarch9 = await context.AddIntakeAsync(userId, 400, new DateTimeOffset(2026, 3, 9, 4, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 3, 7), lastMinuteOfMarch7.Intake.LocalDate);
        Assert.Equal(new DateOnly(2026, 3, 8), firstMinuteOfMarch8.Intake.LocalDate);
        Assert.Equal(new DateOnly(2026, 3, 8), lastMinuteOfMarch8.Intake.LocalDate);
        Assert.Equal(new DateOnly(2026, 3, 9), firstMinuteOfMarch9.Intake.LocalDate);
        Assert.Equal("23:59", lastMinuteOfMarch8.Intake.LocalTime);

        var march8 = await context.Goals.GetProgressForDateAsync(userId, new DateOnly(2026, 3, 8), CancellationToken.None);
        Assert.Equal(500, march8.ConsumedMl);
    }

    [Fact]
    public async Task TravellingAndChangingTimeZone_DoesNotMoveOldIntakes()
    {
        await using var context = new ServiceTestContext(database, new DateTimeOffset(2026, 11, 23, 16, 0, 0, TimeSpan.Zero));
        var userId = await context.RegisterUserAsync("Europe/Istanbul");

        // 16:00 UTC is 19:00 in Istanbul (23rd) but 01:00 on the 24th in Tokyo.
        var inIstanbul = await context.AddIntakeAsync(userId, 250);
        var inTokyo = await context.AddIntakeAsync(userId, 250, timeZone: "Asia/Tokyo");
        Assert.Equal(new DateOnly(2026, 11, 23), inIstanbul.Intake.LocalDate);
        Assert.Equal(new DateOnly(2026, 11, 24), inTokyo.Intake.LocalDate);

        await context.Users.UpdateCurrentUserAsync(userId, new UpdateUserRequest { Name = "Traveller", TimeZone = "America/Los_Angeles" }, CancellationToken.None);

        var stored = await context.Db.WaterIntakes.FindAsync(inIstanbul.Intake.Id);
        Assert.Equal(new DateOnly(2026, 11, 23), stored!.LocalDate);
    }
}
