using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Reminders;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;

namespace Waterly.Api.Services;

// The backend only stores reminder preferences. The phone schedules local notifications itself,
// so there is no background job or push server here.
public class ReminderService(AppDbContext db, IUserService userService, TimeProvider timeProvider) : IReminderService
{
    public async Task<ReminderSettingsResponse> GetSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await db.ReminderSettings.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken)
                       ?? await CreateMissingSettingsAsync(userId, cancellationToken);

        return ToResponse(settings);
    }

    public async Task<ReminderSettingsResponse> UpdateSettingsAsync(Guid userId, UpdateReminderSettingsRequest request, CancellationToken cancellationToken)
    {
        var timeZone = request.TimeZone.Trim();
        DateTimeHelper.EnsureValidTimeZone(timeZone);

        var startTime = ParseTime(request.StartTime);
        var endTime = ParseTime(request.EndTime);
        if (startTime == endTime)
        {
            throw AppException.BadRequest(ErrorCodes.InvalidReminderSettings, "Başlangıç ve bitiş saati aynı olamaz.");
        }

        var settings = await db.ReminderSettings.FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken)
                       ?? await CreateMissingSettingsAsync(userId, cancellationToken);

        settings.Enabled = request.Enabled;
        settings.IntervalMinutes = request.IntervalMinutes;
        settings.StartTime = startTime;
        settings.EndTime = endTime;
        settings.TimeZone = timeZone;
        settings.DefaultGlassMl = request.DefaultGlassMl;
        settings.SoundEnabled = request.SoundEnabled;
        settings.VibrationEnabled = request.VibrationEnabled;
        settings.UpdatedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(settings);
    }

    public static ReminderSettings CreateDefaultSettings(Guid userId, string timeZone, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Enabled = true,
        IntervalMinutes = 120,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(22, 0),
        TimeZone = timeZone,
        DefaultGlassMl = 250,
        SoundEnabled = true,
        VibrationEnabled = true,
        UpdatedAt = now
    };

    // Example: 09:00 - 23:00 every 120 minutes -> 09:00, 11:00, 13:00, ..., 23:00.
    // A window like 22:00 - 02:00 crosses midnight and is supported too.
    public static List<string> BuildDailyReminderTimes(ReminderSettings settings)
    {
        var times = new List<string>();
        if (!settings.Enabled)
        {
            return times;
        }

        var windowMinutes = (int)(settings.EndTime - settings.StartTime).TotalMinutes;
        if (windowMinutes <= 0)
        {
            windowMinutes += 24 * 60;
        }

        for (var offset = 0; offset <= windowMinutes; offset += settings.IntervalMinutes)
        {
            times.Add(DateTimeHelper.FormatTime(settings.StartTime.AddMinutes(offset)));
        }

        return times;
    }

    // Normally created at registration. This only repairs users that somehow have no settings row.
    private async Task<ReminderSettings> CreateMissingSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        var settings = CreateDefaultSettings(userId, timeZone, timeProvider.GetUtcNow());

        db.ReminderSettings.Add(settings);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return settings;
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueViolation(ex))
        {
            db.Entry(settings).State = EntityState.Detached;
            return await db.ReminderSettings.FirstAsync(r => r.UserId == userId, cancellationToken);
        }
    }

    private static TimeOnly ParseTime(string value) =>
        TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);

    private static ReminderSettingsResponse ToResponse(ReminderSettings settings) => new()
    {
        Enabled = settings.Enabled,
        IntervalMinutes = settings.IntervalMinutes,
        StartTime = DateTimeHelper.FormatTime(settings.StartTime),
        EndTime = DateTimeHelper.FormatTime(settings.EndTime),
        TimeZone = settings.TimeZone,
        DefaultGlassMl = settings.DefaultGlassMl,
        SoundEnabled = settings.SoundEnabled,
        VibrationEnabled = settings.VibrationEnabled,
        UpdatedAt = settings.UpdatedAt,
        DailyReminderTimes = BuildDailyReminderTimes(settings)
    };
}
