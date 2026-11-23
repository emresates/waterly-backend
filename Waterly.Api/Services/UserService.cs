using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Users;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;

namespace Waterly.Api.Services;

public class UserService(AppDbContext db, IPasswordHasher<User> passwordHasher, TimeProvider timeProvider) : IUserService
{
    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw UserNotFound();

        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateCurrentUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var timeZone = request.TimeZone.Trim();
        DateTimeHelper.EnsureValidTimeZone(timeZone);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw UserNotFound();

        // Changing the time zone only affects new intakes. Old intakes keep the local day they were saved with.
        user.Name = request.Name.Trim();
        user.TimeZone = timeZone;
        user.UpdatedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task DeleteCurrentUserAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw UserNotFound();

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw AppException.Unauthorized(ErrorCodes.InvalidCredentials, "Şifre hatalı.");
        }

        // The database foreign keys use ON DELETE CASCADE, so intakes, goals, reminders,
        // activity data and refresh tokens of this user are deleted in the same statement.
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<string> GetTimeZoneAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Users
                   .Where(u => u.Id == userId)
                   .Select(u => u.TimeZone)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw UserNotFound();
    }

    public static UserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        TimeZone = user.TimeZone,
        CreatedAt = user.CreatedAt
    };

    private static AppException UserNotFound() =>
        AppException.NotFound(ErrorCodes.UserNotFound, "Kullanıcı bulunamadı.");
}
