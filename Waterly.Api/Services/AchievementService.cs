using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Achievements;
using Waterly.Api.DTOs.Responses;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;

namespace Waterly.Api.Services;

// How achievements work:
// 1. Every metric (total water, streak, balanced days, ...) is recalculated from stored data with PostgreSQL aggregates.
//    Nothing is incremented, so deleting / re-adding water or repeating a request can never add progress twice.
// 2. EvaluateAchievementsAsync locks the user's UserProgress row (SELECT ... FOR UPDATE), so evaluations of the same
//    user run one after another. Missing achievements are inserted (unique index on UserId + AchievementId as a final guard).
// 3. TotalXp is recalculated as SUM(XpReward) of the unlocked achievements, then the level and level rewards follow.
// 4. Earned achievements and rewards are never taken back.
//
// This service only depends on the DbContext, so the other services can call it without circular dependencies.
public partial class AchievementService(AppDbContext db, TimeProvider timeProvider, ILogger<AchievementService> logger) : IAchievementService
{
    public const int GlassEquivalentMl = 250;

    // Water counts for achievements only if it was recorded at most this long after it was consumed.
    // Offline sync keeps working; back-dating a whole week in one go does not build a streak.
    public const int EligibleBackdateHours = 72;

    // Time-of-day habits (morning / noon / evening / balanced days) only count intakes of at least this size.
    public const int HabitMinimumAmountMl = 100;

    private const int DefaultRecentCount = 10;
    private const int MaxRecentCount = 50;

    private const string HiddenName = "Gizli Başarım";
    private const string HiddenDescription = "Bu başarım gizli. Uygulamayı keşfetmeye devam et!";
    private const string HiddenIconKey = "secret_locked";

    [GeneratedRegex("^[a-z0-9_-]{1,32}$")]
    private static partial Regex ThemeKeyRegex();

    // =====================================================================================
    // Queries
    // =====================================================================================

    public async Task<PagedResponse<AchievementResponse>> GetAchievementsAsync(Guid userId, AchievementQuery query, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(userId, cancellationToken);
        var (page, pageSize) = PagingHelper.Normalize(query.Page, query.PageSize);

        var filtered = state.Achievements
            .Where(a => query.Category is null || a.Category == query.Category)
            .Where(a => query.Rarity is null || a.Rarity == query.Rarity)
            .Where(a => query.Unlocked is null || state.Unlocked.ContainsKey(a.Id) == query.Unlocked)
            .ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => ToResponse(a, state))
            .ToList();

        return new PagedResponse<AchievementResponse>(items, page, pageSize, filtered.Count);
    }

    public Task<List<AchievementResponse>> GetUserAchievementsAsync(Guid userId, CancellationToken cancellationToken) =>
        QueryUnlockedAsync(db.UserAchievements.Where(ua => ua.UserId == userId).OrderByDescending(ua => ua.UnlockedAt).ThenBy(ua => ua.AchievementId),
            cancellationToken);

    public Task<List<AchievementResponse>> GetRecentAchievementsAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        var take = count <= 0 ? DefaultRecentCount : Math.Min(count, MaxRecentCount);
        return QueryUnlockedAsync(db.UserAchievements.Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.UnlockedAt).ThenBy(ua => ua.AchievementId).Take(take), cancellationToken);
    }

    public Task<List<AchievementResponse>> GetUnseenAchievementsAsync(Guid userId, CancellationToken cancellationToken) =>
        QueryUnlockedAsync(db.UserAchievements.Where(ua => ua.UserId == userId && ua.SeenAt == null)
            .OrderBy(ua => ua.UnlockedAt).ThenBy(ua => ua.AchievementId), cancellationToken);

    public async Task<MarkAchievementsSeenResponse> MarkAchievementsSeenAsync(Guid userId, MarkAchievementsSeenRequest request, CancellationToken cancellationToken)
    {
        if (!request.All && request.AchievementIds.Count == 0)
        {
            throw AppException.BadRequest(ErrorCodes.ValidationFailed, "achievementIds listesi veya \"all\": true gönderilmelidir.");
        }

        // Filtering by UserId means a user can only ever mark their own unlocked achievements.
        var unseen = db.UserAchievements.Where(ua => ua.UserId == userId && ua.SeenAt == null);
        if (!request.All)
        {
            var ids = request.AchievementIds.Distinct().ToList();
            unseen = unseen.Where(ua => ids.Contains(ua.AchievementId));
        }

        var seenAt = (DateTimeOffset?)timeProvider.GetUtcNow();
        var marked = await unseen.ExecuteUpdateAsync(setters => setters.SetProperty(ua => ua.SeenAt, seenAt), cancellationToken);

        return new MarkAchievementsSeenResponse { MarkedCount = marked };
    }

    public async Task<AchievementSummaryResponse> GetSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var total = await db.Achievements.CountAsync(a => a.IsActive, cancellationToken);
        var unlocked = await db.UserAchievements
            .AsNoTracking()
            .Where(ua => ua.UserId == userId && ua.Achievement.IsActive)
            .Select(ua => new { ua.Achievement.Rarity, ua.SeenAt })
            .ToListAsync(cancellationToken);
        var level = await GetUserProgressAsync(userId, cancellationToken);
        var lastActiveDate = await db.UserProgress
            .Where(p => p.UserId == userId)
            .Select(p => p.LastActiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        int CountRarity(AchievementRarity rarity) => unlocked.Count(u => u.Rarity == rarity);

        return new AchievementSummaryResponse
        {
            TotalAchievements = total,
            UnlockedAchievements = unlocked.Count,
            LockedAchievements = total - unlocked.Count,
            CompletionPercentage = total == 0 ? 0 : Math.Round(unlocked.Count * 100m / total, 2),
            TotalXp = level.TotalXp,
            Level = level.Level,
            LevelProgressPercentage = level.ProgressPercentage,
            CommonUnlocked = CountRarity(AchievementRarity.Common),
            RareUnlocked = CountRarity(AchievementRarity.Rare),
            EpicUnlocked = CountRarity(AchievementRarity.Epic),
            LegendaryUnlocked = CountRarity(AchievementRarity.Legendary),
            MythicUnlocked = CountRarity(AchievementRarity.Mythic),
            UnseenAchievements = unlocked.Count(u => u.SeenAt is null),
            LastActiveDate = lastActiveDate
        };
    }

    public async Task<UserLevelResponse> GetUserProgressAsync(Guid userId, CancellationToken cancellationToken)
    {
        var totalXp = await db.UserProgress
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.TotalXp)
            .FirstOrDefaultAsync(cancellationToken);

        return LevelCalculator.Build(totalXp ?? 0);
    }

    public async Task<List<RewardResponse>> GetUserRewardsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rewards = await db.UserRewards
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .OrderBy(ur => ur.UnlockedAt)
            .ThenBy(ur => ur.RewardId)
            .Select(ur => new { ur.Reward, ur.UnlockedAt })
            .ToListAsync(cancellationToken);

        return rewards.Select(r => ToRewardResponse(r.Reward, r.UnlockedAt)).ToList();
    }

    // =====================================================================================
    // Mobile events
    // =====================================================================================

    public async Task<RecordAchievementEventResponse> RecordEventAsync(Guid userId, RecordAchievementEventRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientEventId is not { } clientEventId || clientEventId == Guid.Empty)
        {
            throw AppException.BadRequest(ErrorCodes.ValidationFailed, "Geçerli bir clientEventId (UUID) gönderilmelidir.");
        }

        var eventType = request.EventType.Trim().ToUpperInvariant();
        if (!AchievementEventTypes.ClientAllowed.Contains(eventType))
        {
            throw InvalidEvent("Desteklenmeyen olay tipi.");
        }

        // Idempotency: a retry of the same phone event returns success without doing anything.
        var existing = await FindEventAsync(userId, clientEventId, cancellationToken);
        if (existing is not null)
        {
            return EventResult(clientEventId, existing.EventType, AchievementEventStatus.AlreadyProcessed);
        }

        // The local day comes from the server clock + the profile time zone, never from the device.
        var timeZone = await GetTimeZoneAsync(userId, cancellationToken);
        var today = DateTimeHelper.GetToday(timeProvider, timeZone);
        var (value, metadata, dedupeKey) = BuildEventDetails(eventType, request.EventValue, today);

        // A theme only counts as "used" if the user really owns it (starter theme or earned reward).
        if (eventType == AchievementEventTypes.BottleThemeUsed && !await OwnsBottleThemeAsync(userId, value!, cancellationToken))
        {
            throw InvalidEvent("BOTTLE_THEME_USED yalnızca kullanıcının sahip olduğu bir şişe temasının assetKey değeriyle gönderilebilir.");
        }

        var stored = await TryInsertEventAsync(new UserAchievementEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientEventId = clientEventId,
            EventType = eventType,
            EventValue = value,
            EventMetadata = metadata,
            DedupeKey = dedupeKey,
            LocalDate = today,
            CreatedAt = timeProvider.GetUtcNow()
        }, cancellationToken);

        if (!stored)
        {
            // Either a parallel retry with the same clientEventId won, or this kind of event was already counted for the period.
            var winner = await FindEventAsync(userId, clientEventId, cancellationToken);
            return EventResult(clientEventId, eventType,
                winner is not null ? AchievementEventStatus.AlreadyProcessed : AchievementEventStatus.AlreadyCounted);
        }

        var gamification = await TryEvaluateAchievementsAsync(userId, cancellationToken);
        return EventResult(clientEventId, eventType, AchievementEventStatus.Recorded, gamification);
    }

    public async Task TryRecordSessionAsync(Guid userId, bool alwaysEvaluate, CancellationToken cancellationToken)
    {
        try
        {
            var timeZone = await GetTimeZoneAsync(userId, cancellationToken);
            var today = DateTimeHelper.GetToday(timeProvider, timeZone);
            var dedupeKey = DayKey(today);

            var recorded = false;
            var alreadyToday = await db.UserAchievementEvents.AnyAsync(e => e.UserId == userId
                && e.EventType == AchievementEventTypes.SessionStarted && e.DedupeKey == dedupeKey, cancellationToken);

            if (!alreadyToday)
            {
                recorded = await TryInsertEventAsync(new UserAchievementEvent
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ClientEventId = Guid.NewGuid(),
                    EventType = AchievementEventTypes.SessionStarted,
                    DedupeKey = dedupeKey,
                    LocalDate = today,
                    CreatedAt = timeProvider.GetUtcNow()
                }, cancellationToken);
            }

            if (recorded || alwaysEvaluate)
            {
                await EvaluateAchievementsAsync(userId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failed achievement check must never break login. The next evaluation catches up.
            logger.LogWarning(ex, "Recording the session for achievements failed for user {UserId}.", userId);
            db.ChangeTracker.Clear();
        }
    }

    // =====================================================================================
    // Evaluation
    // =====================================================================================

    public async Task<AchievementEvaluationResponse?> TryEvaluateAchievementsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await EvaluateAchievementsAsync(userId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The main action (e.g. saving water) already succeeded and must not look failed to the client.
            logger.LogWarning(ex, "Achievement evaluation failed for user {UserId}. It will be retried on the next evaluation.", userId);
            db.ChangeTracker.Clear();
            return null;
        }
    }

    public async Task<AchievementEvaluationResponse> EvaluateAchievementsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = timeProvider.GetUtcNow();

        // From here on, other evaluations of the same user wait until this transaction ends.
        await LockUserProgressAsync(userId, now, cancellationToken);
        var progress = await db.UserProgress.AsNoTracking().FirstAsync(p => p.UserId == userId, cancellationToken);
        var previousLevel = progress.Level;

        // Metrics are read after the lock, so they include everything committed by earlier requests.
        var state = await LoadStateAsync(userId, cancellationToken);
        var unlockedIds = state.Unlocked.Keys.ToHashSet();
        var newlyUnlocked = new List<Achievement>();

        // Repeat until nothing changes, so meta achievements ("10 achievements", "first secret") see the new ones.
        // The loop always ends: every pass either unlocks something new or stops, and the catalog is finite.
        // An achievement is only checked while it is locked, so it can never count itself (no SECRET_FIRST loop).
        bool changed;
        do
        {
            changed = false;
            foreach (var achievement in state.Achievements)
            {
                if (!unlockedIds.Contains(achievement.Id)
                    && GetUnlockValue(achievement, state, unlockedIds) >= achievement.TargetValue)
                {
                    unlockedIds.Add(achievement.Id);
                    newlyUnlocked.Add(achievement);
                    changed = true;
                }
            }
        } while (changed);

        foreach (var achievement in newlyUnlocked)
        {
            var row = new UserAchievement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AchievementId = achievement.Id,
                UnlockedAt = now
            };
            db.UserAchievements.Add(row);
            state.Unlocked[achievement.Id] = row;
        }

        await db.SaveChangesAsync(cancellationToken);

        // XP is the sum of what was earned, so it can never be counted twice and repairs itself if it ever drifted.
        var totalXp = await db.UserAchievements
            .Where(ua => ua.UserId == userId)
            .SumAsync(ua => ua.Achievement.XpReward, cancellationToken);
        var level = LevelCalculator.LevelFor(totalXp);

        var lastActiveDate = state.Metrics.LastActiveDate;
        if (progress.TotalXp != totalXp || progress.Level != level || progress.LastActiveDate != lastActiveDate)
        {
            await db.UserProgress
                .Where(p => p.UserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.TotalXp, totalXp)
                    .SetProperty(p => p.Level, level)
                    .SetProperty(p => p.LastActiveDate, lastActiveDate)
                    .SetProperty(p => p.UpdatedAt, now), cancellationToken);
        }

        var newRewards = await UnlockRewardsAsync(userId, level, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new AchievementEvaluationResponse
        {
            NewlyUnlocked = newlyUnlocked.Select(a => ToResponse(a, state)).ToList(),
            NewRewards = newRewards,
            Progress = LevelCalculator.Build(totalXp),
            PreviousLevel = previousLevel,
            LeveledUp = level > previousLevel
        };
    }

    // Makes sure the user's UserProgress row exists, then locks it until the transaction ends.
    private async Task LockUserProgressAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "UserProgress" ("Id", "UserId", "TotalXp", "Level", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {userId}, 0, 1, {now})
            ON CONFLICT ("UserId") DO NOTHING
            """, cancellationToken);

        await db.Database.ExecuteSqlAsync($"""
            SELECT 1 FROM "UserProgress" WHERE "UserId" = {userId} FOR UPDATE
            """, cancellationToken);
    }

    private async Task<List<RewardResponse>> UnlockRewardsAsync(Guid userId, int level, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unlockedCodes = (await db.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.Achievement.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var owned = (await db.UserRewards
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RewardId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var rewards = await db.Rewards.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var earned = rewards
            .Where(r => !owned.Contains(r.Id) && IsRewardEarned(r, level, unlockedCodes))
            .ToList();

        foreach (var reward in earned)
        {
            db.UserRewards.Add(new UserReward { Id = Guid.NewGuid(), UserId = userId, RewardId = reward.Id, UnlockedAt = now });
        }

        return earned.Select(r => ToRewardResponse(r, now)).ToList();
    }

    // Every requirement that is set must be met.
    private static bool IsRewardEarned(Reward reward, int level, IReadOnlySet<string> unlockedCodes)
    {
        if (reward.RequiredLevel is null && reward.RequiredAchievementCode is null)
        {
            return false;
        }

        var levelMet = reward.RequiredLevel is not { } requiredLevel || level >= requiredLevel;
        var achievementMet = reward.RequiredAchievementCode is not { } code || unlockedCodes.Contains(code);
        return levelMet && achievementMet;
    }

    // =====================================================================================
    // Metrics (read-only)
    // =====================================================================================

    private sealed class EvaluationState(List<Achievement> achievements, Dictionary<int, UserAchievement> unlocked, AchievementMetrics metrics)
    {
        public List<Achievement> Achievements { get; } = achievements;
        public Dictionary<int, UserAchievement> Unlocked { get; } = unlocked;
        public AchievementMetrics Metrics { get; } = metrics;
        public HashSet<int> SecretIds { get; } = achievements.Where(a => a.IsSecret).Select(a => a.Id).ToHashSet();
    }

    private async Task<EvaluationState> LoadStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var achievements = await db.Achievements
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(cancellationToken);

        var unlocked = await db.UserAchievements
            .AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .ToDictionaryAsync(ua => ua.AchievementId, cancellationToken);

        var metrics = await ComputeMetricsAsync(userId, cancellationToken);
        return new EvaluationState(achievements, unlocked, metrics);
    }

    // A handful of small queries. Water is aggregated per day inside PostgreSQL; individual intakes are never loaded.
    private async Task<AchievementMetrics> ComputeMetricsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
                       .AsNoTracking()
                       .Where(u => u.Id == userId)
                       .Select(u => new { u.CreatedAt, u.TimeZone })
                       .FirstOrDefaultAsync(cancellationToken)
                   ?? throw AppException.NotFound(ErrorCodes.UserNotFound, "Kullanıcı bulunamadı.");

        var goals = await db.HydrationGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.EffectiveFrom)
            .ThenBy(g => g.CreatedAt)
            .Select(g => new GoalSnapshot(g.TargetMl, g.EffectiveFrom, g.CreatedAt))
            .ToListAsync(cancellationToken);

        const int minAmount = HabitMinimumAmountMl;
        const int backdateHours = EligibleBackdateHours;

        // Local hours use the time zone stored on each intake, exactly like the daily statistics.
        // Windows: morning start 07-09, noon 12-13, evening 18-20; balanced day slots 06-11, 12-17, 18-21.
        var days = await db.Database.SqlQuery<DailyIntakeAggregate>($"""
            SELECT x."LocalDate" AS "Date",
                   SUM(x."AmountMl")::int AS "TotalMl",
                   COUNT(*)::int AS "IntakeCount",
                   COALESCE(EXTRACT(HOUR FROM MIN(x.local_time) FILTER (WHERE x."AmountMl" >= {minAmount}))::int, -1) AS "FirstHabitHour",
                   COALESCE(bool_or(x.local_hour BETWEEN 12 AND 13) FILTER (WHERE x."AmountMl" >= {minAmount}), FALSE) AS "Noon",
                   COALESCE(bool_or(x.local_hour BETWEEN 18 AND 20) FILTER (WHERE x."AmountMl" >= {minAmount}), FALSE) AS "Evening",
                   COALESCE(bool_or(x.local_hour BETWEEN 6 AND 11) FILTER (WHERE x."AmountMl" >= {minAmount}), FALSE) AS "SlotMorning",
                   COALESCE(bool_or(x.local_hour BETWEEN 12 AND 17) FILTER (WHERE x."AmountMl" >= {minAmount}), FALSE) AS "SlotAfternoon",
                   COALESCE(bool_or(x.local_hour BETWEEN 18 AND 21) FILTER (WHERE x."AmountMl" >= {minAmount}), FALSE) AS "SlotEvening",
                   bool_or(x."Source" = 'ReminderNotification') AS "FromReminder"
            FROM (
                SELECT i."LocalDate", i."AmountMl", i."Source",
                       (i."ConsumedAt" AT TIME ZONE i."TimeZone") AS local_time,
                       EXTRACT(HOUR FROM (i."ConsumedAt" AT TIME ZONE i."TimeZone"))::int AS local_hour
                FROM "WaterIntakes" AS i
                WHERE i."UserId" = {userId}
                  AND i."CreatedAt" - i."ConsumedAt" <= make_interval(hours => {backdateHours})
            ) AS x
            GROUP BY x."LocalDate"
            ORDER BY x."LocalDate"
            """).ToListAsync(cancellationToken);

        var timeZoneCounts = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(DISTINCT i."TimeZone")::int AS "Value"
            FROM "WaterIntakes" AS i
            WHERE i."UserId" = {userId}
              AND i."CreatedAt" - i."ConsumedAt" <= make_interval(hours => {backdateHours})
            """).ToListAsync(cancellationToken);

        // The day an intake was *recorded* (server time) proves the user was active that day.
        var recordedDates = await db.Database.SqlQuery<DateOnly>($"""
            SELECT DISTINCT (i."CreatedAt" AT TIME ZONE i."TimeZone")::date AS "Value"
            FROM "WaterIntakes" AS i
            WHERE i."UserId" = {userId}
            """).ToListAsync(cancellationToken);

        var events = await db.UserAchievementEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => new AchievementEventSnapshot(e.EventType, e.LocalDate, e.EventValue, e.EventMetadata))
            .ToListAsync(cancellationToken);

        var reminder = await db.ReminderSettings
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => new ReminderSnapshot(r.Enabled, r.IntervalMinutes, r.StartTime, r.EndTime,
                r.DefaultGlassMl, r.SoundEnabled, r.VibrationEnabled, r.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        // Only real data from Apple Health / Health Connect counts, not manually typed values or empty days.
        var healthConnected = await db.DailyActivities.AnyAsync(a => a.UserId == userId
            && a.Source != ActivitySource.Manual
            && (a.Steps > 0 || a.ExerciseMinutes > 0), cancellationToken);

        return AchievementMetricsCalculator.Calculate(new AchievementMetricsInput
        {
            Today = DateTimeHelper.GetToday(timeProvider, user.TimeZone),
            RegistrationDate = DateTimeHelper.ToLocalDate(user.CreatedAt, user.TimeZone),
            UserCreatedAt = user.CreatedAt,
            Goals = goals,
            Days = days,
            DistinctIntakeTimeZones = timeZoneCounts.FirstOrDefault(),
            IntakeRecordedDates = recordedDates,
            Events = events,
            Reminder = reminder,
            HealthConnected = healthConnected
        });
    }

    private static long GetUnlockValue(Achievement achievement, EvaluationState state, IReadOnlySet<int> unlockedIds) =>
        achievement.MetricType switch
        {
            // Meta achievements never count themselves.
            AchievementMetric.AchievementsUnlocked => unlockedIds.Count(id => id != achievement.Id),
            AchievementMetric.SecretAchievementsUnlocked => unlockedIds.Count(id => id != achievement.Id && state.SecretIds.Contains(id)),
            _ => state.Metrics[achievement.MetricType]
        };

    // Locked streak achievements show the current streak; unlocking uses the longest streak.
    private static long GetDisplayValue(Achievement achievement, EvaluationState state) =>
        achievement.MetricType == AchievementMetric.StreakDays
            ? state.Metrics.CurrentStreak
            : GetUnlockValue(achievement, state, state.Unlocked.Keys.ToHashSet());

    // =====================================================================================
    // Event helpers
    // =====================================================================================

    // Decides what is stored for an event and how often it can count (the dedupe key).
    private static (string? Value, string? Metadata, string DedupeKey) BuildEventDetails(string eventType, string? rawValue, DateOnly today)
    {
        var value = string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();

        switch (eventType)
        {
            case AchievementEventTypes.BottleThemeUsed:
            {
                var theme = value?.ToLowerInvariant();
                if (theme is null || !ThemeKeyRegex().IsMatch(theme))
                {
                    throw InvalidEvent("BOTTLE_THEME_USED için eventValue geçerli bir tema anahtarı olmalıdır (a-z, 0-9, _ veya -, en fazla 32 karakter).");
                }

                return (theme, null, $"theme:{theme}");
            }

            case AchievementEventTypes.CalendarDayViewed:
            {
                if (value is null)
                {
                    return (null, null, DayKey(today));
                }

                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var viewedDay)
                    || viewedDay >= today)
                {
                    throw InvalidEvent("CALENDAR_DAY_VIEWED için eventValue bugünden önceki bir gün olmalıdır (yyyy-MM-dd).");
                }

                return (viewedDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null, DayKey(today));
            }
        }

        if (value is not null)
        {
            throw InvalidEvent($"{eventType} olayı eventValue kabul etmez.");
        }

        return eventType switch
        {
            // Counted once per ISO calendar week.
            AchievementEventTypes.WeeklyReportOpened => (null, AchievementMetricsCalculator.IsoWeekKey(today),
                $"week:{AchievementMetricsCalculator.IsoWeekKey(today)}"),

            // Only the first time matters.
            AchievementEventTypes.OnboardingCompleted or AchievementEventTypes.DarkThemeEnabled
                or AchievementEventTypes.BottleEasterEggDiscovered => (null, null, "once"),

            // Screens and app opens: once per local day.
            _ => (null, null, DayKey(today))
        };
    }

    private async Task<bool> OwnsBottleThemeAsync(Guid userId, string assetKey, CancellationToken cancellationToken)
    {
        var theme = await db.Rewards
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.AssetKey == assetKey && r.RewardType == RewardType.BottleTheme, cancellationToken);

        return theme is not null
               && (RewardCatalog.IsStarter(theme)
                   || await db.UserRewards.AnyAsync(ur => ur.UserId == userId && ur.RewardId == theme.Id, cancellationToken));
    }

    // Called by PreferenceService after the user saved their appearance settings on the server.
    // These are server-side facts (not client claims), recorded with the same once / per-theme rules as mobile events.
    public async Task<AchievementEvaluationResponse?> TryRecordPreferenceEventsAsync(Guid userId, bool darkModeEnabled,
        string? selectedBottleThemeAssetKey, CancellationToken cancellationToken)
    {
        try
        {
            var timeZone = await GetTimeZoneAsync(userId, cancellationToken);
            var today = DateTimeHelper.GetToday(timeProvider, timeZone);

            if (darkModeEnabled)
            {
                await RecordServerEventAsync(userId, AchievementEventTypes.DarkThemeEnabled, null, today, cancellationToken);
            }

            if (selectedBottleThemeAssetKey is not null)
            {
                await RecordServerEventAsync(userId, AchievementEventTypes.BottleThemeUsed, selectedBottleThemeAssetKey, today, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Recording preference events failed for user {UserId}.", userId);
            db.ChangeTracker.Clear();
        }

        return await TryEvaluateAchievementsAsync(userId, cancellationToken);
    }

    private async Task RecordServerEventAsync(Guid userId, string eventType, string? rawValue, DateOnly today, CancellationToken cancellationToken)
    {
        var (value, metadata, dedupeKey) = BuildEventDetails(eventType, rawValue, today);
        var alreadyCounted = await db.UserAchievementEvents
            .AnyAsync(e => e.UserId == userId && e.EventType == eventType && e.DedupeKey == dedupeKey, cancellationToken);
        if (alreadyCounted)
        {
            return;
        }

        await TryInsertEventAsync(new UserAchievementEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientEventId = Guid.NewGuid(),
            EventType = eventType,
            EventValue = value,
            EventMetadata = metadata,
            DedupeKey = dedupeKey,
            LocalDate = today,
            CreatedAt = timeProvider.GetUtcNow()
        }, cancellationToken);
    }

    // Returns false when a unique index (same clientEventId, or same event in the same period) rejected the row.
    private async Task<bool> TryInsertEventAsync(UserAchievementEvent achievementEvent, CancellationToken cancellationToken)
    {
        db.UserAchievementEvents.Add(achievementEvent);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueViolation(ex))
        {
            db.Entry(achievementEvent).State = EntityState.Detached;
            return false;
        }
    }

    private Task<UserAchievementEvent?> FindEventAsync(Guid userId, Guid clientEventId, CancellationToken cancellationToken) =>
        db.UserAchievementEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.ClientEventId == clientEventId, cancellationToken);

    private async Task<string> GetTimeZoneAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.Where(u => u.Id == userId).Select(u => u.TimeZone).FirstOrDefaultAsync(cancellationToken)
        ?? throw AppException.NotFound(ErrorCodes.UserNotFound, "Kullanıcı bulunamadı.");

    private static string DayKey(DateOnly day) => $"day:{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    private static RecordAchievementEventResponse EventResult(Guid clientEventId, string eventType, AchievementEventStatus status,
        AchievementEvaluationResponse? gamification = null) => new()
    {
        ClientEventId = clientEventId,
        EventType = eventType,
        Status = status,
        Gamification = gamification
    };

    private static AppException InvalidEvent(string message) =>
        AppException.BadRequest(ErrorCodes.InvalidAchievementEvent, message);

    // =====================================================================================
    // Mapping
    // =====================================================================================

    private async Task<List<AchievementResponse>> QueryUnlockedAsync(IQueryable<UserAchievement> query, CancellationToken cancellationToken)
    {
        var rows = await query
            .AsNoTracking()
            .Select(ua => new { ua.Achievement, ua.UnlockedAt, ua.SeenAt })
            .ToListAsync(cancellationToken);

        return rows.Select(r => ToUnlockedResponse(r.Achievement, r.UnlockedAt, r.SeenAt)).ToList();
    }

    private static AchievementResponse ToResponse(Achievement achievement, EvaluationState state)
    {
        if (state.Unlocked.TryGetValue(achievement.Id, out var unlocked))
        {
            return ToUnlockedResponse(achievement, unlocked.UnlockedAt, unlocked.SeenAt);
        }

        // Locked secret: do not leak the code, name, description, icon or what has to be done.
        if (achievement.IsSecret)
        {
            return new AchievementResponse
            {
                Id = achievement.Id,
                Code = null,
                Name = HiddenName,
                Description = HiddenDescription,
                Category = achievement.Category,
                Rarity = achievement.Rarity,
                XpReward = achievement.XpReward,
                IconKey = HiddenIconKey,
                IsSecret = true,
                IsUnlocked = false
            };
        }

        var current = Math.Clamp(GetDisplayValue(achievement, state), 0, achievement.TargetValue);
        return new AchievementResponse
        {
            Id = achievement.Id,
            Code = achievement.Code,
            Name = achievement.Name,
            Description = achievement.Description,
            Category = achievement.Category,
            Rarity = achievement.Rarity,
            XpReward = achievement.XpReward,
            IconKey = achievement.IconKey,
            IsSecret = false,
            IsUnlocked = false,
            CurrentValue = current,
            TargetValue = achievement.TargetValue,
            ProgressPercentage = Math.Round(current * 100m / achievement.TargetValue, 1)
        };
    }

    private static AchievementResponse ToUnlockedResponse(Achievement achievement, DateTimeOffset unlockedAt, DateTimeOffset? seenAt) => new()
    {
        Id = achievement.Id,
        Code = achievement.Code,
        Name = achievement.Name,
        Description = achievement.Description,
        Category = achievement.Category,
        Rarity = achievement.Rarity,
        XpReward = achievement.XpReward,
        IconKey = achievement.IconKey,
        IsSecret = achievement.IsSecret,
        IsUnlocked = true,
        CurrentValue = achievement.TargetValue,
        TargetValue = achievement.TargetValue,
        ProgressPercentage = 100m,
        UnlockedAt = unlockedAt,
        SeenAt = seenAt
    };

    private static RewardResponse ToRewardResponse(Reward reward, DateTimeOffset unlockedAt) => new()
    {
        Id = reward.Id,
        Code = reward.Code,
        Name = reward.Name,
        Description = reward.Description,
        RewardType = reward.RewardType,
        AssetKey = reward.AssetKey,
        RequiredLevel = reward.RequiredLevel,
        RequiredAchievementCode = reward.RequiredAchievementCode,
        UnlockedAt = unlockedAt
    };
}
