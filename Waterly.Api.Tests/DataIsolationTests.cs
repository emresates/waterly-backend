using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Exceptions;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class DataIsolationTests(DatabaseFixture database) : IDisposable
{
    private readonly WaterlyApiFactory _factory = new(database);

    [Fact]
    public async Task UserCannotReadChangeOrDeleteAnotherUsersIntake()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync();
        var bob = await _factory.CreateAuthenticatedClientAsync();

        var created = await alice.PostAsJsonAsync("/api/hydration/intakes", new { amountMl = 500, clientEventId = Guid.NewGuid() });
        var aliceIntakeId = (await WaterlyApiFactory.ReadAsync<IntakeResultResponse>(created)).Data!.Intake.Id;

        var update = await bob.PutAsJsonAsync($"/api/hydration/intakes/{aliceIntakeId}", new { amountMl = 1, consumedAt = DateTimeOffset.UtcNow });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(ErrorCodes.IntakeNotFound, (await WaterlyApiFactory.ReadAsync<object>(update)).ErrorCode);

        var delete = await bob.DeleteAsync($"/api/hydration/intakes/{aliceIntakeId}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var bobList = await WaterlyApiFactory.ReadAsync<PagedResponse<IntakeResponse>>(await bob.GetAsync("/api/hydration/intakes"));
        Assert.Empty(bobList.Data!.Items);

        var bobToday = await WaterlyApiFactory.ReadAsync<TodayResponse>(await bob.GetAsync("/api/hydration/today"));
        Assert.Equal(0, bobToday.Data!.Progress.ConsumedMl);

        var aliceToday = await WaterlyApiFactory.ReadAsync<TodayResponse>(await alice.GetAsync("/api/hydration/today"));
        Assert.Equal(500, aliceToday.Data!.Progress.ConsumedMl);
    }

    [Fact]
    public async Task SameClientEventId_FromDifferentUsers_CreatesSeparateRecords()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync();
        var bob = await _factory.CreateAuthenticatedClientAsync();
        var sharedEventId = Guid.NewGuid();

        var aliceResponse = await alice.PostAsJsonAsync("/api/hydration/intakes", new { amountMl = 250, clientEventId = sharedEventId });
        var bobResponse = await bob.PostAsJsonAsync("/api/hydration/intakes", new { amountMl = 330, clientEventId = sharedEventId });

        Assert.Equal(HttpStatusCode.Created, aliceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bobResponse.StatusCode);
        Assert.Equal(330, (await WaterlyApiFactory.ReadAsync<IntakeResultResponse>(bobResponse)).Data!.Intake.AmountMl);
    }

    [Fact]
    public async Task ProtectedEndpoints_RequireAToken()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/hydration/today");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthorized, (await WaterlyApiFactory.ReadAsync<object>(response)).ErrorCode);
    }

    [Fact]
    public async Task ParallelRetriesOfTheSameNotificationAction_CreateOnlyOneRecord()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var clientEventId = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/hydration/intakes", new { amountMl = 250, clientEventId })));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);

        await using var db = database.CreateDbContext();
        Assert.Equal(1, await db.WaterIntakes.CountAsync(i => i.ClientEventId == clientEventId));
    }

    [Fact]
    public async Task DeletingTheAccount_RemovesAllPersonalData()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var me = await WaterlyApiFactory.ReadAsync<Waterly.Api.DTOs.Users.UserResponse>(await client.GetAsync("/api/users/me"));
        var userId = me.Data!.Id;
        await client.PostAsJsonAsync("/api/hydration/intakes", new { amountMl = 250, clientEventId = Guid.NewGuid() });

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me") { Content = JsonContent.Create(new { password = "Passw0rd!" }) };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = database.CreateDbContext();
        Assert.False(await db.Users.AnyAsync(u => u.Id == userId));
        Assert.False(await db.WaterIntakes.AnyAsync(i => i.UserId == userId));
        Assert.False(await db.HydrationGoals.AnyAsync(g => g.UserId == userId));
        Assert.False(await db.ReminderSettings.AnyAsync(r => r.UserId == userId));
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == userId));
    }

    public void Dispose() => _factory.Dispose();
}
