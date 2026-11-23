using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Hydration;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;

namespace Waterly.Api.Services;

public class HydrationService(
    AppDbContext db,
    IGoalService goalService,
    IUserService userService,
    IAchievementService achievementService,
    TimeProvider timeProvider) : IHydrationService
{
    public const int MinAmountMl = 10;
    public const int MaxAmountMl = 5000;

    private const int MaxDaysBack = 365;
    private const int MaxHistoryRangeDays = 366;
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

    public async Task<TodayResponse> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        var today = DateTimeHelper.GetToday(timeProvider, timeZone);

        var intakes = await db.WaterIntakes
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.LocalDate == today)
            .OrderByDescending(i => i.ConsumedAt)
            .ToListAsync(cancellationToken);

        return new TodayResponse
        {
            TimeZone = timeZone,
            Progress = await goalService.GetProgressForDateAsync(userId, today, cancellationToken),
            Intakes = intakes.Select(ToResponse).ToList()
        };
    }

    public async Task<IntakeResultResponse> AddIntakeAsync(Guid userId, AddIntakeRequest request, CancellationToken cancellationToken)
    {
        var userTimeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        var (intake, alreadyProcessed) = await SaveIntakeAsync(userId, userTimeZone, request, cancellationToken);
        var progress = await goalService.GetProgressForDateAsync(userId, intake.LocalDate, cancellationToken);

        // The water is already saved. The achievement check is idempotent and never throws, so a retry of the same
        // clientEventId cannot add XP twice, and it still completes anything a previous failed check missed.
        var gamification = await achievementService.TryEvaluateAchievementsAsync(userId, cancellationToken);

        return new IntakeResultResponse
        {
            Intake = ToResponse(intake),
            Progress = progress,
            AlreadyProcessed = alreadyProcessed,
            Gamification = gamification
        };
    }

    public async Task<SyncIntakesResponse> SyncIntakesAsync(Guid userId, SyncIntakesRequest request, CancellationToken cancellationToken)
    {
        var userTimeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        var results = new List<SyncIntakeItemResult>();

        // Every item is saved on its own. One invalid item does not block the others,
        // and the app can see exactly which events were stored.
        foreach (var item in request.Intakes)
        {
            try
            {
                var (intake, alreadyProcessed) = await SaveIntakeAsync(userId, userTimeZone, item, cancellationToken);
                results.Add(new SyncIntakeItemResult
                {
                    ClientEventId = intake.ClientEventId,
                    Status = alreadyProcessed ? SyncItemStatus.AlreadyProcessed : SyncItemStatus.Created,
                    Intake = ToResponse(intake)
                });
            }
            catch (AppException ex)
            {
                results.Add(new SyncIntakeItemResult
                {
                    ClientEventId = item.ClientEventId,
                    Status = SyncItemStatus.Failed,
                    ErrorCode = ex.ErrorCode,
                    Message = ex.Message
                });
            }
        }

        var today = DateTimeHelper.GetToday(timeProvider, userTimeZone);
        var todayProgress = await goalService.GetProgressForDateAsync(userId, today, cancellationToken);

        // One evaluation for the whole batch instead of one per item.
        var gamification = await achievementService.TryEvaluateAchievementsAsync(userId, cancellationToken);

        return new SyncIntakesResponse
        {
            CreatedCount = results.Count(r => r.Status == SyncItemStatus.Created),
            AlreadyProcessedCount = results.Count(r => r.Status == SyncItemStatus.AlreadyProcessed),
            FailedCount = results.Count(r => r.Status == SyncItemStatus.Failed),
            Results = results,
            Today = todayProgress,
            Gamification = gamification
        };
    }

    public async Task<PagedResponse<IntakeResponse>> GetIntakesAsync(Guid userId, DateRangeQuery query, CancellationToken cancellationToken)
    {
        var timeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        var today = DateTimeHelper.GetToday(timeProvider, timeZone);
        var from = query.From ?? query.To ?? today;
        var to = query.To ?? query.From ?? today;
        EnsureValidRange(from, to);

        var (page, pageSize) = PagingHelper.Normalize(query.Page, query.PageSize);

        var intakesInRange = db.WaterIntakes
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.LocalDate >= from && i.LocalDate <= to);

        var totalCount = await intakesInRange.CountAsync(cancellationToken);
        var intakes = await intakesInRange
            .OrderByDescending(i => i.ConsumedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<IntakeResponse>(intakes.Select(ToResponse).ToList(), page, pageSize, totalCount);
    }

    public async Task<IntakeResultResponse> UpdateIntakeAsync(Guid userId, Guid intakeId, UpdateIntakeRequest request, CancellationToken cancellationToken)
    {
        var intake = await FindOwnIntakeAsync(userId, intakeId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        EnsureValidAmount(request.AmountMl);
        var consumedAt = EnsureValidConsumedAt(request.ConsumedAt!.Value, now);
        var timeZone = ResolveTimeZone(request.TimeZone, intake.TimeZone);

        intake.AmountMl = request.AmountMl;
        intake.ConsumedAt = consumedAt;
        intake.TimeZone = timeZone;
        intake.LocalDate = DateTimeHelper.ToLocalDate(consumedAt, timeZone);
        intake.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        var progress = await goalService.GetProgressForDateAsync(userId, intake.LocalDate, cancellationToken);

        // Progress is recalculated from the stored data. Already earned badges are never taken back.
        var gamification = await achievementService.TryEvaluateAchievementsAsync(userId, cancellationToken);

        return new IntakeResultResponse
        {
            Intake = ToResponse(intake),
            Progress = progress,
            AlreadyProcessed = false,
            Gamification = gamification
        };
    }

    public async Task<DailyProgressResponse> DeleteIntakeAsync(Guid userId, Guid intakeId, CancellationToken cancellationToken)
    {
        var intake = await FindOwnIntakeAsync(userId, intakeId, cancellationToken);

        db.WaterIntakes.Remove(intake);
        await db.SaveChangesAsync(cancellationToken);
        var progress = await goalService.GetProgressForDateAsync(userId, intake.LocalDate, cancellationToken);

        // Earned badges stay. Deleting and re-adding water cannot earn XP again because every achievement is one-time.
        await achievementService.TryEvaluateAchievementsAsync(userId, cancellationToken);

        return progress;
    }

    public async Task<PagedResponse<DailyProgressResponse>> GetHistoryAsync(Guid userId, DateRangeQuery query, CancellationToken cancellationToken)
    {
        var timeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        var today = DateTimeHelper.GetToday(timeProvider, timeZone);
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-29);
        EnsureValidRange(from, to);

        // Do not list empty days from before the user started using the app.
        var trackingStart = await GetTrackingStartDateAsync(userId, cancellationToken);
        if (from < trackingStart)
        {
            from = trackingStart;
        }

        var (page, pageSize) = PagingHelper.Normalize(query.Page, query.PageSize);
        if (from > to)
        {
            return new PagedResponse<DailyProgressResponse>([], page, pageSize, 0);
        }

        var days = await GetDailyProgressRangeAsync(userId, from, to, cancellationToken);
        var pageItems = days
            .OrderByDescending(d => d.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResponse<DailyProgressResponse>(pageItems, page, pageSize, days.Count);
    }

    public async Task<List<DailyProgressResponse>> GetDailyProgressRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var totals = await db.WaterIntakes
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.LocalDate >= from && i.LocalDate <= to)
            .GroupBy(i => i.LocalDate)
            .Select(g => new { Date = g.Key, ConsumedMl = g.Sum(i => i.AmountMl), IntakeCount = g.Count() })
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        var goals = await goalService.GetGoalsForRangeAsync(userId, from, to, cancellationToken);

        var days = new List<DailyProgressResponse>();
        foreach (var day in DateTimeHelper.EachDay(from, to))
        {
            totals.TryGetValue(day, out var total);
            days.Add(ProgressCalculator.Calculate(day, goals[day], total?.ConsumedMl ?? 0, total?.IntakeCount ?? 0));
        }

        return days;
    }

    public async Task<DateOnly> GetTrackingStartDateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
                       .AsNoTracking()
                       .Where(u => u.Id == userId)
                       .Select(u => new { u.CreatedAt, u.TimeZone })
                       .FirstOrDefaultAsync(cancellationToken)
                   ?? throw AppException.NotFound(ErrorCodes.UserNotFound, "Kullanıcı bulunamadı.");

        var registeredOn = DateTimeHelper.ToLocalDate(user.CreatedAt, user.TimeZone);
        var firstIntakeDate = await db.WaterIntakes
            .Where(i => i.UserId == userId)
            .MinAsync(i => (DateOnly?)i.LocalDate, cancellationToken);

        return firstIntakeDate < registeredOn ? firstIntakeDate.Value : registeredOn;
    }

    // Idempotent save: the same (user, clientEventId) is stored at most once.
    private async Task<(WaterIntake Intake, bool AlreadyProcessed)> SaveIntakeAsync(
        Guid userId, string userTimeZone, AddIntakeRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientEventId is not { } clientEventId || clientEventId == Guid.Empty)
        {
            throw AppException.BadRequest(ErrorCodes.ValidationFailed, "Geçerli bir clientEventId (UUID) gönderilmelidir.");
        }

        var existing = await FindByClientEventIdAsync(userId, clientEventId, cancellationToken);
        if (existing is not null)
        {
            return (existing, true);
        }

        var now = timeProvider.GetUtcNow();
        EnsureValidAmount(request.AmountMl);
        var consumedAt = EnsureValidConsumedAt(request.ConsumedAt ?? now, now);
        var timeZone = ResolveTimeZone(request.TimeZone, userTimeZone);
        var source = request.Source ?? IntakeSource.Manual;
        if (!Enum.IsDefined(source))
        {
            throw AppException.BadRequest(ErrorCodes.ValidationFailed, "Geçersiz kayıt kaynağı. Manual veya ReminderNotification olmalıdır.");
        }

        var intake = new WaterIntake
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientEventId = clientEventId,
            Source = source,
            AmountMl = request.AmountMl,
            ConsumedAt = consumedAt,
            TimeZone = timeZone,
            LocalDate = DateTimeHelper.ToLocalDate(consumedAt, timeZone),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WaterIntakes.Add(intake);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return (intake, false);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueViolation(ex))
        {
            // The same event arrived twice at the same moment (e.g. a retry while the first request was running).
            // The unique index (UserId, ClientEventId) let only one of them in; return that one.
            db.Entry(intake).State = EntityState.Detached;
            var winner = await FindByClientEventIdAsync(userId, clientEventId, cancellationToken);
            return (winner!, true);
        }
    }

    private Task<WaterIntake?> FindByClientEventIdAsync(Guid userId, Guid clientEventId, CancellationToken cancellationToken) =>
        db.WaterIntakes
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.ClientEventId == clientEventId, cancellationToken);

    // Filtering by UserId is what stops a user from reading or changing someone else's intake by guessing an id.
    // Another user's intake looks exactly like a missing one (404), so ids cannot be probed.
    private async Task<WaterIntake> FindOwnIntakeAsync(Guid userId, Guid intakeId, CancellationToken cancellationToken) =>
        await db.WaterIntakes.FirstOrDefaultAsync(i => i.Id == intakeId && i.UserId == userId, cancellationToken)
        ?? throw AppException.NotFound(ErrorCodes.IntakeNotFound, "Su tüketim kaydı bulunamadı.");

    private static void EnsureValidAmount(int amountMl)
    {
        if (amountMl < MinAmountMl || amountMl > MaxAmountMl)
        {
            throw AppException.BadRequest(ErrorCodes.InvalidWaterAmount,
                $"Su miktarı {MinAmountMl} ile {MaxAmountMl} ml arasında olmalıdır.");
        }
    }

    private static DateTimeOffset EnsureValidConsumedAt(DateTimeOffset consumedAt, DateTimeOffset now)
    {
        if (consumedAt > now + AllowedClockSkew)
        {
            throw AppException.BadRequest(ErrorCodes.InvalidConsumedAt, "Gelecek bir zamana su tüketimi eklenemez.");
        }

        if (consumedAt < now.AddDays(-MaxDaysBack))
        {
            throw AppException.BadRequest(ErrorCodes.InvalidConsumedAt, $"En fazla {MaxDaysBack} gün öncesine kayıt eklenebilir.");
        }

        // PostgreSQL "timestamp with time zone" columns are always written in UTC.
        return consumedAt.ToUniversalTime();
    }

    private static string ResolveTimeZone(string? requestedTimeZone, string fallbackTimeZone)
    {
        var timeZone = string.IsNullOrWhiteSpace(requestedTimeZone) ? fallbackTimeZone : requestedTimeZone.Trim();
        DateTimeHelper.EnsureValidTimeZone(timeZone);
        return timeZone;
    }

    private static void EnsureValidRange(DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            throw AppException.BadRequest(ErrorCodes.InvalidDateRange, "Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        }

        if (to.DayNumber - from.DayNumber >= MaxHistoryRangeDays)
        {
            throw AppException.BadRequest(ErrorCodes.InvalidDateRange, $"Tarih aralığı en fazla {MaxHistoryRangeDays} gün olabilir.");
        }
    }

    private static IntakeResponse ToResponse(WaterIntake intake) => new()
    {
        Id = intake.Id,
        ClientEventId = intake.ClientEventId,
        AmountMl = intake.AmountMl,
        ConsumedAt = intake.ConsumedAt,
        LocalDate = intake.LocalDate,
        LocalTime = DateTimeHelper.ToLocalTime(intake.ConsumedAt, intake.TimeZone).ToString("HH:mm"),
        TimeZone = intake.TimeZone,
        Source = intake.Source,
        CreatedAt = intake.CreatedAt,
        UpdatedAt = intake.UpdatedAt
    };
}
