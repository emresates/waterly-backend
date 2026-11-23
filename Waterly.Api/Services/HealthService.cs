using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Health;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;

namespace Waterly.Api.Services;

// Health data is optional. The phone reads Apple HealthKit / Android Health Connect with the user's
// permission and uploads only a daily summary. The backend never talks to those SDKs.
public class HealthService(
    AppDbContext db,
    IGoalService goalService,
    IUserService userService,
    TimeProvider timeProvider) : IHealthService
{
    private const int MaxDaysBack = 365;
    private const int MaxQueryRangeDays = 92;

    // Recommendation rules. The user is compared with their OWN recent average, not with a fixed step count,
    // because there is no single step number that means "you need X ml more water".
    private const int BaselineDays = 14;
    private const int MinimumBaselineDays = 3;
    private const double StepIncreaseRatio = 1.3;
    private const int MinimumExtraSteps = 3000;
    private const int MinimumExtraExerciseMinutes = 30;

    public async Task<List<ActivityResponse>> SaveActivityAsync(Guid userId, SaveActivityRequest request, CancellationToken cancellationToken)
    {
        var source = request.Source!.Value;
        var today = await GetTodayAsync(userId, cancellationToken);

        // If the same date is sent twice in one request, the last value wins.
        var days = request.Days
            .GroupBy(d => d.Date!.Value)
            .Select(g => g.Last())
            .ToList();

        foreach (var day in days)
        {
            // +1 day tolerance because the phone may be in a time zone ahead of the profile's.
            if (day.Date > today.AddDays(1) || day.Date < today.AddDays(-MaxDaysBack))
            {
                throw AppException.BadRequest(ErrorCodes.InvalidDateRange, $"Geçersiz aktivite tarihi: {day.Date:yyyy-MM-dd}.");
            }
        }

        // Upsert: update the existing row for (user, date, source) or insert a new one.
        // If a parallel request inserted the same row first, the unique index rejects ours and we retry once as an update.
        try
        {
            await UpsertDaysAsync(userId, source, days, cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            await UpsertDaysAsync(userId, source, days, cancellationToken);
        }

        var dates = days.Select(d => d.Date!.Value).ToList();
        return await db.DailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Source == source && dates.Contains(a.Date))
            .OrderBy(a => a.Date)
            .Select(a => ToResponse(a))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ActivityResponse>> GetActivityAsync(Guid userId, ActivityQuery query, CancellationToken cancellationToken)
    {
        var today = await GetTodayAsync(userId, cancellationToken);
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-6);

        if (from > to || to.DayNumber - from.DayNumber >= MaxQueryRangeDays)
        {
            throw AppException.BadRequest(ErrorCodes.InvalidDateRange, $"Tarih aralığı geçersiz. En fazla {MaxQueryRangeDays} gün seçilebilir.");
        }

        return await db.DailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.Source)
            .Select(a => ToResponse(a))
            .ToListAsync(cancellationToken);
    }

    public async Task<RecommendationResponse> GetRecommendationAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken)
    {
        var day = date ?? await GetTodayAsync(userId, cancellationToken);
        var goalMl = await goalService.GetGoalForDateAsync(userId, day, cancellationToken);

        var activities = await db.DailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date >= day.AddDays(-BaselineDays) && a.Date <= day)
            .ToListAsync(cancellationToken);

        // A user might send both Apple Health and manual data. Taking the highest value per day avoids counting twice.
        var dailyTotals = activities
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => (Steps: g.Max(a => a.Steps), ExerciseMinutes: g.Max(a => a.ExerciseMinutes)));

        if (!dailyTotals.TryGetValue(day, out var current))
        {
            return new RecommendationResponse
            {
                Date = day,
                Status = RecommendationStatus.NoData,
                CurrentGoalMl = goalMl,
                Message = "Bu gün için aktivite verisi yok. Uygulama sağlık verisi olmadan da normal şekilde çalışır."
            };
        }

        var baseline = dailyTotals.Where(d => d.Key < day).Select(d => d.Value).ToList();
        if (baseline.Count < MinimumBaselineDays)
        {
            return new RecommendationResponse
            {
                Date = day,
                Status = RecommendationStatus.InsufficientHistory,
                CurrentGoalMl = goalMl,
                Steps = current.Steps,
                ExerciseMinutes = current.ExerciseMinutes,
                Reasons = [$"Karşılaştırma için en az {MinimumBaselineDays} günlük geçmiş aktivite verisi gerekiyor (şu an {baseline.Count} gün var)."],
                Message = "Kişisel aktivite ortalamanızı öğrenmek için biraz daha veriye ihtiyaç var."
            };
        }

        var baselineSteps = (int)Math.Round(baseline.Average(b => b.Steps));
        var baselineExercise = (int)Math.Round(baseline.Average(b => b.ExerciseMinutes));
        var reasons = new List<string>();

        if (current.Steps >= baselineSteps * StepIncreaseRatio && current.Steps - baselineSteps >= MinimumExtraSteps)
        {
            reasons.Add($"Adım sayınız ({current.Steps}) son {baseline.Count} günlük ortalamanızdan ({baselineSteps}) belirgin şekilde yüksek.");
        }

        if (current.ExerciseMinutes - baselineExercise >= MinimumExtraExerciseMinutes)
        {
            reasons.Add($"Egzersiz süreniz ({current.ExerciseMinutes} dk) ortalamanızdan ({baselineExercise} dk) en az {MinimumExtraExerciseMinutes} dakika fazla.");
        }

        var isElevated = reasons.Count > 0;
        if (!isElevated)
        {
            reasons.Add("Aktiviteniz kişisel ortalamanıza yakın.");
        }

        return new RecommendationResponse
        {
            Date = day,
            Status = isElevated ? RecommendationStatus.ElevatedActivity : RecommendationStatus.Normal,
            ShouldReviewGoal = isElevated,
            CurrentGoalMl = goalMl,
            Steps = current.Steps,
            BaselineSteps = baselineSteps,
            ExerciseMinutes = current.ExerciseMinutes,
            BaselineExerciseMinutes = baselineExercise,
            Reasons = reasons,
            Message = isElevated
                ? "Bugün her zamankinden daha aktif görünüyorsunuz. Aktif günlerde su ihtiyacı artabilir; isterseniz günlük hedefinizi gözden geçirebilirsiniz. Hedefiniz otomatik olarak değiştirilmedi."
                : "Bugünkü aktiviteniz olağan seviyenizde görünüyor."
        };
    }

    private async Task UpsertDaysAsync(Guid userId, ActivitySource source, List<ActivityDayRequest> days, CancellationToken cancellationToken)
    {
        var dates = days.Select(d => d.Date!.Value).ToList();
        var existing = await db.DailyActivities
            .Where(a => a.UserId == userId && a.Source == source && dates.Contains(a.Date))
            .ToDictionaryAsync(a => a.Date, cancellationToken);

        var now = timeProvider.GetUtcNow();
        foreach (var day in days)
        {
            var date = day.Date!.Value;
            if (!existing.TryGetValue(date, out var activity))
            {
                activity = new DailyActivity { Id = Guid.NewGuid(), UserId = userId, Date = date, Source = source };
                db.DailyActivities.Add(activity);
            }

            activity.Steps = day.Steps;
            activity.ExerciseMinutes = day.ExerciseMinutes;
            activity.ActiveCalories = day.ActiveCalories;
            activity.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<DateOnly> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZone = await userService.GetTimeZoneAsync(userId, cancellationToken);
        return DateTimeHelper.GetToday(timeProvider, timeZone);
    }

    private static ActivityResponse ToResponse(DailyActivity activity) => new()
    {
        Date = activity.Date,
        Steps = activity.Steps,
        ExerciseMinutes = activity.ExerciseMinutes,
        ActiveCalories = activity.ActiveCalories,
        Source = activity.Source,
        UpdatedAt = activity.UpdatedAt
    };
}
