using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Waterly.Api.DTOs.Auth;
using Waterly.Api.Exceptions;
using Waterly.Api.Tests.Infrastructure;

namespace Waterly.Api.Tests;

[Collection(DatabaseCollection.Name)]
public class AuthTests(DatabaseFixture database) : IDisposable
{
    private readonly WaterlyApiFactory _factory = new(database);

    [Fact]
    public async Task UsedRefreshToken_CannotBeUsedAgain_AndReuseRevokesTheWholeSession()
    {
        var client = _factory.CreateClient();
        var registered = await _factory.RegisterAsync(client);

        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = registered.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var rotated = (await WaterlyApiFactory.ReadAsync<AuthResponse>(firstRefresh)).Data!;
        Assert.NotEqual(registered.RefreshToken, rotated.RefreshToken);

        // Replaying the old token fails...
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = registered.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(ErrorCodes.InvalidToken, (await WaterlyApiFactory.ReadAsync<object>(replay)).ErrorCode);

        // ...and because a replay looks like theft, the newest token is revoked too.
        var afterReplay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var client = _factory.CreateClient();
        var registered = await _factory.RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.AccessToken);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = registered.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = registered.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Login_ChecksPassword_AndEmailIsUnique()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@Example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { name = "Ayşe", email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { name = "Ayşe", email = email.ToUpperInvariant(), password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ErrorCodes.EmailAlreadyExists, (await WaterlyApiFactory.ReadAsync<object>(duplicate)).ErrorCode);

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass1" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, (await WaterlyApiFactory.ReadAsync<object>(wrongPassword)).ErrorCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task InvalidRegistration_ReturnsValidationErrorsInStandardFormat()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { name = "A", email = "not-an-email", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await WaterlyApiFactory.ReadAsync<object>(response);
        Assert.False(body.Success);
        Assert.Equal(ErrorCodes.ValidationFailed, body.ErrorCode);
        Assert.Contains("email", body.Errors!.Keys);
        Assert.Contains("password", body.Errors!.Keys);
    }

    public void Dispose() => _factory.Dispose();
}
