using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Waterly.Api.DTOs.Achievements;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.DTOs.Users;
using Waterly.Api.Exceptions;
using Waterly.Api.Models;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

// HTTP level: real middleware, JWT, validation, rate limiting policies and JSON format.
[Collection(DatabaseCollection.Name)]
public class AchievementApiTests(DatabaseFixture database) : IDisposable
{
    private readonly WaterlyApiFactory _factory = new(database);

    [Fact]
    public async Task OldClientsWithoutSource_StillWork_AndGetGamificationInTheResponse()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/hydration/intakes", new { amountMl = 250, clientEventId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await WaterlyApiFactory.ReadAsync<IntakeResultResponse>(response)).Data!;
        Assert.Equal(IntakeSource.Manual, body.Intake.Source);
        Assert.Contains(body.Gamification!.NewlyUnlocked, a => a.Code == "FIRST_DROP");

        var fromReminder = await client.PostAsJsonAsync("/api/hydration/intakes",
            new { amountMl = 250, clientEventId = Guid.NewGuid(), source = "ReminderNotification" });
        Assert.Equal(IntakeSource.ReminderNotification, (await WaterlyApiFactory.ReadAsync<IntakeResultResponse>(fromReminder)).Data!.Intake.Source);
    }

    [Fact]
    public async Task ParallelRequests_DoNotCreateDoubleXp()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var me = (await WaterlyApiFactory.ReadAsync<UserResponse>(await client.GetAsync("/api/users/me"))).Data!;
        var repeatedId = Guid.NewGuid();

        var requests = Enumerable.Range(0, 10)
            .Select(i => client.PostAsJsonAsync("/api/hydration/intakes",
                new { amountMl = 250, clientEventId = i < 5 ? repeatedId : Guid.NewGuid() }))
            .Concat(Enumerable.Range(0, 4).Select(_ => client.PostAsync("/api/achievements/me/evaluate", null)));
        var responses = await Task.WhenAll(requests);
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));

        await using var db = database.CreateDbContext();
        var achievementIds = await db.UserAchievements.Where(ua => ua.UserId == me.Id).Select(ua => ua.AchievementId).ToListAsync();
        Assert.Equal(achievementIds.Count, achievementIds.Distinct().Count());

        var earnedXp = await db.UserAchievements.Where(ua => ua.UserId == me.Id).SumAsync(ua => ua.Achievement.XpReward);
        var progress = await db.UserProgress.SingleAsync(p => p.UserId == me.Id);
        Assert.Equal(earnedXp, progress.TotalXp);

        // 6 distinct intakes x 250 ml = 1500 ml -> 6 glasses
        Assert.Contains("GLASSES_5", await db.UserAchievements.Where(ua => ua.UserId == me.Id).Select(ua => ua.Achievement.Code).ToListAsync());
    }

    [Fact]
    public async Task CatalogEndpoint_HidesLockedSecrets_AndSupportsFilters()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var all = (await WaterlyApiFactory.ReadAsync<PagedResponse<AchievementResponse>>(await client.GetAsync("/api/achievements"))).Data!;
        Assert.Equal(76, all.TotalCount);
        Assert.Equal(10, all.Items.Count(a => a.Code is null));

        var raw = await client.GetStringAsync("/api/achievements?category=Secret");
        Assert.DoesNotContain("SECRET_", raw);
        Assert.DoesNotContain("Karanlık", raw);
        Assert.Contains("\"category\":\"Secret\"", raw);   // enums are strings, like the rest of the API

        var rare = (await WaterlyApiFactory.ReadAsync<PagedResponse<AchievementResponse>>(
            await client.GetAsync("/api/achievements?rarity=Rare&unlocked=false&pageSize=5"))).Data!;
        Assert.Equal(5, rare.Items.Count);
        Assert.All(rare.Items, a => Assert.Equal(AchievementRarity.Rare, a.Rarity));

        var unlocked = (await WaterlyApiFactory.ReadAsync<PagedResponse<AchievementResponse>>(
            await client.GetAsync("/api/achievements?unlocked=true"))).Data!;
        Assert.Equal("WELCOME", Assert.Single(unlocked.Items).Code);
    }

    [Fact]
    public async Task EventsEndpoint_IsIdempotent_AndUnlocksSecretsAndTheSummaryFollows()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var eventId = Guid.NewGuid();

        var first = await client.PostAsJsonAsync("/api/achievements/events", new { clientEventId = eventId, eventType = "DARK_THEME_ENABLED" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var recorded = (await WaterlyApiFactory.ReadAsync<RecordAchievementEventResponse>(first)).Data!;
        Assert.Equal(AchievementEventStatus.Recorded, recorded.Status);
        Assert.Equal(["SECRET_DARK_MODE", "SECRET_FIRST"], recorded.Gamification!.NewlyUnlocked.Select(a => a.Code).Order());

        var retry = await client.PostAsJsonAsync("/api/achievements/events", new { clientEventId = eventId, eventType = "DARK_THEME_ENABLED" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(AchievementEventStatus.AlreadyProcessed, (await WaterlyApiFactory.ReadAsync<RecordAchievementEventResponse>(retry)).Data!.Status);

        var invalid = await client.PostAsJsonAsync("/api/achievements/events", new { clientEventId = Guid.NewGuid(), eventType = "SESSION_STARTED" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ErrorCodes.InvalidAchievementEvent, (await WaterlyApiFactory.ReadAsync<object>(invalid)).ErrorCode);

        // XP / achievement codes in the body are simply not part of the contract and are ignored.
        var sneaky = await client.PostAsJsonAsync("/api/achievements/events",
            new { clientEventId = Guid.NewGuid(), eventType = "APP_OPENED", xp = 100000, achievementCode = "STREAK_365" });
        Assert.Equal(HttpStatusCode.OK, sneaky.StatusCode);

        var summary = (await WaterlyApiFactory.ReadAsync<AchievementSummaryResponse>(await client.GetAsync("/api/achievements/me/summary"))).Data!;
        Assert.Equal(76, summary.TotalAchievements);
        Assert.Equal(3, summary.UnlockedAchievements);                // WELCOME, SECRET_DARK_MODE, SECRET_FIRST
        Assert.Equal(73, summary.LockedAchievements);
        Assert.Equal(3.95m, summary.CompletionPercentage);
        Assert.Equal(160, summary.TotalXp);                            // 10 + 50 + 100
        Assert.Equal(6, summary.Level);
        Assert.Equal(1, summary.CommonUnlocked);
        Assert.Equal(2, summary.RareUnlocked);
        Assert.Equal(3, summary.UnseenAchievements);

        var progress = (await WaterlyApiFactory.ReadAsync<UserLevelResponse>(await client.GetAsync("/api/achievements/me/progress"))).Data!;
        Assert.Equal(160, progress.TotalXp);
        Assert.Equal(125, progress.CurrentLevelXp);
        Assert.Equal(180, progress.NextLevelXp);
        Assert.Equal(63.6m, progress.ProgressPercentage);

        var rewards = (await WaterlyApiFactory.ReadAsync<List<RewardResponse>>(await client.GetAsync("/api/achievements/me/rewards"))).Data!;
        Assert.Equal(["BOTTLE_CLASSIC", "BOTTLE_MINT", "BOTTLE_OCEAN", "BOTTLE_SKY_BLUE"], rewards.Select(r => r.Code).Order());
    }

    [Fact]
    public async Task UnseenAndSeen_WorkForTheCurrentUserOnly()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync();
        var bob = await _factory.CreateAuthenticatedClientAsync();
        await alice.PostAsJsonAsync("/api/hydration/intakes", new { amountMl = 250, clientEventId = Guid.NewGuid() });

        var aliceUnseen = (await WaterlyApiFactory.ReadAsync<List<AchievementResponse>>(await alice.GetAsync("/api/achievements/me/unseen"))).Data!;
        Assert.Equal(4, aliceUnseen.Count);

        var bobMarks = await bob.PostAsJsonAsync("/api/achievements/me/seen", new { achievementIds = aliceUnseen.Select(a => a.Id) });
        Assert.Equal(1, (await WaterlyApiFactory.ReadAsync<MarkAchievementsSeenResponse>(bobMarks)).Data!.MarkedCount);   // only his WELCOME

        var aliceMarks = await alice.PostAsJsonAsync("/api/achievements/me/seen", new { achievementIds = aliceUnseen.Take(2).Select(a => a.Id) });
        Assert.Equal(2, (await WaterlyApiFactory.ReadAsync<MarkAchievementsSeenResponse>(aliceMarks)).Data!.MarkedCount);
        Assert.Equal(2, (await WaterlyApiFactory.ReadAsync<List<AchievementResponse>>(await alice.GetAsync("/api/achievements/me/unseen"))).Data!.Count);

        var empty = await alice.PostAsJsonAsync("/api/achievements/me/seen", new { });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var recent = (await WaterlyApiFactory.ReadAsync<List<AchievementResponse>>(await alice.GetAsync("/api/achievements/me/recent?count=2"))).Data!;
        Assert.Equal(2, recent.Count);
        var mine = (await WaterlyApiFactory.ReadAsync<List<AchievementResponse>>(await alice.GetAsync("/api/achievements/me"))).Data!;
        Assert.Equal(4, mine.Count);
        Assert.All(mine, a => Assert.NotNull(a.UnlockedAt));
    }

    [Fact]
    public async Task AchievementEndpoints_RequireAToken()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/achievements")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/achievements/events",
            new { clientEventId = Guid.NewGuid(), eventType = "APP_OPENED" })).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
