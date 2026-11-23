using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Auth;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;
using Waterly.Api.Settings;

namespace Waterly.Api.Services;

public class AuthService(
    AppDbContext db,
    ITokenService tokenService,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtSettings> jwtOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    // Used when the e-mail does not exist, so a failed login takes the same time either way.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString());

    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var timeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? DateTimeHelper.DefaultTimeZone : request.TimeZone.Trim();
        DateTimeHelper.EnsureValidTimeZone(timeZone);

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw EmailAlreadyExists();
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = email,
            TimeZone = timeZone,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        // Every new user starts with a default goal (valid from their local "today") and default reminders.
        user.HydrationGoals.Add(new HydrationGoal
        {
            Id = Guid.NewGuid(),
            TargetMl = GoalService.DefaultTargetMl,
            EffectiveFrom = DateTimeHelper.ToLocalDate(now, timeZone),
            CreatedAt = now
        });
        user.ReminderSettings = ReminderService.CreateDefaultSettings(user.Id, timeZone, now);

        db.Users.Add(user);
        var refreshToken = AddNewRefreshToken(user.Id, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueViolation(ex))
        {
            // Two registrations with the same e-mail arrived at the same moment.
            throw EmailAlreadyExists();
        }

        return BuildAuthResponse(user, refreshToken.PlainToken, refreshToken.Entity.ExpiresAt);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new User(), DummyPasswordHash, request.Password);
            throw InvalidCredentials();
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw InvalidCredentials();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        var now = timeProvider.GetUtcNow();
        await DeleteExpiredRefreshTokensAsync(user.Id, now, cancellationToken);

        var refreshToken = AddNewRefreshToken(user.Id, now);
        await db.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, refreshToken.PlainToken, refreshToken.Entity.ExpiresAt);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var storedToken = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            throw InvalidToken();
        }

        var now = timeProvider.GetUtcNow();

        if (storedToken.RevokedAt is not null)
        {
            if (storedToken.ReplacedByTokenId is not null)
            {
                // An already rotated token was used again: it was probably stolen.
                // Log the user out from every device to be safe.
                logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all sessions.", storedToken.UserId);
                await RevokeAllRefreshTokensAsync(storedToken.UserId, now, cancellationToken);
            }

            throw InvalidToken();
        }

        if (storedToken.ExpiresAt <= now)
        {
            throw InvalidToken();
        }

        // Rotation: the old token is revoked and a brand new one is issued, in a single transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var newRefreshToken = AddNewRefreshToken(storedToken.UserId, now);
        await db.SaveChangesAsync(cancellationToken);

        // "WHERE RevokedAt IS NULL" makes this safe when two requests try to rotate the same token at once:
        // only one of them can update the row, the other gets 0 and fails.
        var revokedCount = await db.RefreshTokens
            .Where(t => t.Id == storedToken.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenId, newRefreshToken.Entity.Id), cancellationToken);

        if (revokedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw InvalidToken();
        }

        await transaction.CommitAsync(cancellationToken);

        return BuildAuthResponse(storedToken.User, newRefreshToken.PlainToken, newRefreshToken.Entity.ExpiresAt);
    }

    public async Task LogoutAsync(Guid userId, RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        // Logout is idempotent: an unknown or already revoked token is simply ignored.
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    private (RefreshToken Entity, string PlainToken) AddNewRefreshToken(Guid userId, DateTimeOffset now)
    {
        var plainToken = tokenService.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenService.HashToken(plainToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenDays)
        };

        db.RefreshTokens.Add(entity);
        return (entity, plainToken);
    }

    private Task RevokeAllRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);

    // Keeps the table small without needing a background job.
    private Task DeleteExpiredRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt < now)
            .ExecuteDeleteAsync(cancellationToken);

    private AuthResponse BuildAuthResponse(User user, string refreshToken, DateTimeOffset refreshTokenExpiresAt)
    {
        var accessToken = tokenService.CreateAccessToken(user);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            User = UserService.ToResponse(user)
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static AppException EmailAlreadyExists() =>
        AppException.Conflict(ErrorCodes.EmailAlreadyExists, "Bu e-posta adresiyle kayıtlı bir hesap zaten var.");

    private static AppException InvalidCredentials() =>
        AppException.Unauthorized(ErrorCodes.InvalidCredentials, "E-posta veya şifre hatalı.");

    private static AppException InvalidToken() =>
        AppException.Unauthorized(ErrorCodes.InvalidToken, "Oturum geçersiz veya süresi dolmuş. Lütfen tekrar giriş yapın.");
}
