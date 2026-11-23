using Microsoft.EntityFrameworkCore;
using Waterly.Api.Data;
using Waterly.Api.DTOs.Achievements;
using Waterly.Api.DTOs.Preferences;
using Waterly.Api.Exceptions;
using Waterly.Api.Helpers;
using Waterly.Api.Interfaces;
using Waterly.Api.Models;

namespace Waterly.Api.Services;

// Earning a reward and using it are separate:
// - AchievementService unlocks rewards (UserRewards) when a level / achievement is reached.
// - This service lets the user select an unlocked reward and stores the selection on the account,
//   so the active theme comes back on a new phone.
public class PreferenceService(AppDbContext db, IAchievementService achievementService, TimeProvider timeProvider) : IPreferenceService
{
    public async Task<PreferencesResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Read-only: users without a saved row get the defaults, nothing is written on GET.
        var preferences = await db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var catalog = await LoadCatalogAsync(userId, cancellationToken);
        return BuildResponse(preferences, catalog);
    }

    public async Task<PreferencesResponse> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        var colorScheme = request.ColorScheme!.Value;
        if (!Enum.IsDefined(colorScheme))
        {
            throw AppException.BadRequest(ErrorCodes.ValidationFailed, "colorScheme System, Light veya Dark olmalıdır.");
        }

        var catalog = await LoadCatalogAsync(userId, cancellationToken);
        var bottleThemeId = ResolveSelection(catalog, request.BottleTheme, RewardType.BottleTheme)
                            ?? throw AppException.BadRequest(ErrorCodes.ValidationFailed, "bottleTheme zorunludur.");
        var profileFrameId = ResolveSelection(catalog, request.ProfileFrame, RewardType.ProfileFrame);
        var waterAnimationId = ResolveSelection(catalog, request.WaterAnimation, RewardType.WaterAnimation);
        var badgeEffectId = ResolveSelection(catalog, request.BadgeEffect, RewardType.BadgeEffect);
        var mascotAccessoryId = ResolveSelection(catalog, request.MascotAccessory, RewardType.MascotAccessory);

        void Apply(UserPreferences target)
        {
            target.ColorScheme = colorScheme;
            target.ActiveBottleThemeId = bottleThemeId;
            target.ActiveProfileFrameId = profileFrameId;
            target.ActiveWaterAnimationId = waterAnimationId;
            target.ActiveBadgeEffectId = badgeEffectId;
            target.ActiveMascotAccessoryId = mascotAccessoryId;
            target.UpdatedAt = timeProvider.GetUtcNow();
        }

        var preferences = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (preferences is null)
        {
            preferences = new UserPreferences { Id = Guid.NewGuid(), UserId = userId };
            db.UserPreferences.Add(preferences);
        }

        Apply(preferences);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueViolation(ex))
        {
            // A parallel request created the row first; update that row instead.
            db.Entry(preferences).State = EntityState.Detached;
            preferences = await db.UserPreferences.FirstAsync(p => p.UserId == userId, cancellationToken);
            Apply(preferences);
            await db.SaveChangesAsync(cancellationToken);
        }

        // The selection is a server-side fact: it counts for SECRET_DARK_MODE and SECRET_THEMES_3
        // (each theme once, dark mode once). Never fails the request.
        var gamification = await achievementService.TryRecordPreferenceEventsAsync(userId,
            colorScheme == ColorScheme.Dark, catalog.ById[bottleThemeId].AssetKey, cancellationToken);

        // Reload ownership: the evaluation may have unlocked new rewards.
        var refreshed = await LoadCatalogAsync(userId, cancellationToken);
        var saved = await db.UserPreferences.AsNoTracking().FirstAsync(p => p.UserId == userId, cancellationToken);
        return BuildResponse(saved, refreshed, gamification);
    }

    public async Task<List<CosmeticItemResponse>> GetCosmeticsAsync(Guid userId, CosmeticsQuery query, CancellationToken cancellationToken)
    {
        var preferences = await db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var catalog = await LoadCatalogAsync(userId, cancellationToken);
        var active = ActiveIds(preferences, catalog);

        return catalog.Rewards
            .Where(r => query.Type is null || r.RewardType == query.Type)
            .OrderBy(r => r.RewardType)
            .ThenBy(r => r.RequiredLevel ?? int.MaxValue)
            .ThenBy(r => r.Id)
            .Select(r => ToItem(r, catalog, active))
            .ToList();
    }

    // ---------------------------------------------------------------------------

    private sealed class Catalog(List<Reward> rewards, Dictionary<int, DateTimeOffset> owned)
    {
        public List<Reward> Rewards { get; } = rewards;
        public Dictionary<int, Reward> ById { get; } = rewards.ToDictionary(r => r.Id);
        public Dictionary<int, DateTimeOffset> Owned { get; } = owned;

        public bool IsUnlocked(Reward reward) => RewardCatalog.IsStarter(reward) || Owned.ContainsKey(reward.Id);
    }

    private async Task<Catalog> LoadCatalogAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rewards = await db.Rewards.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var owned = await db.UserRewards
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .ToDictionaryAsync(ur => ur.RewardId, ur => ur.UnlockedAt, cancellationToken);

        return new Catalog(rewards, owned);
    }

    // Returns the reward id for a selected code, or null when nothing is selected.
    private static int? ResolveSelection(Catalog catalog, string? code, RewardType expectedType)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();
        var reward = catalog.Rewards.FirstOrDefault(r => r.Code == normalized)
                     ?? throw AppException.NotFound(ErrorCodes.RewardNotFound, $"Ödül bulunamadı: {normalized}.");

        if (reward.RewardType != expectedType)
        {
            throw AppException.BadRequest(ErrorCodes.InvalidReward, $"{reward.Code} bu alan için kullanılamaz; {expectedType} türünde bir ödül seçilmelidir.");
        }

        if (!catalog.IsUnlocked(reward))
        {
            throw new AppException(StatusCodes.Status403Forbidden, ErrorCodes.RewardLocked,
                $"{reward.Name} henüz açılmadı. Kullanmak için önce kazanmalısınız.");
        }

        return reward.Id;
    }

    private static HashSet<int> ActiveIds(UserPreferences? preferences, Catalog catalog)
    {
        var defaultTheme = catalog.Rewards.First(r => r.Code == RewardCatalog.DefaultBottleThemeCode).Id;
        var ids = new HashSet<int> { preferences?.ActiveBottleThemeId ?? defaultTheme };
        foreach (var id in new[]
                 {
                     preferences?.ActiveProfileFrameId, preferences?.ActiveWaterAnimationId,
                     preferences?.ActiveBadgeEffectId, preferences?.ActiveMascotAccessoryId
                 })
        {
            if (id is { } value)
            {
                ids.Add(value);
            }
        }

        return ids;
    }

    private static PreferencesResponse BuildResponse(UserPreferences? preferences, Catalog catalog,
        AchievementEvaluationResponse? gamification = null)
    {
        var active = ActiveIds(preferences, catalog);
        CosmeticItemResponse? Item(int? id) => id is { } value ? ToItem(catalog.ById[value], catalog, active) : null;

        var bottleThemeId = preferences?.ActiveBottleThemeId
                            ?? catalog.Rewards.First(r => r.Code == RewardCatalog.DefaultBottleThemeCode).Id;

        return new PreferencesResponse
        {
            ColorScheme = preferences?.ColorScheme ?? ColorScheme.System,
            BottleTheme = Item(bottleThemeId)!,
            ProfileFrame = Item(preferences?.ActiveProfileFrameId),
            WaterAnimation = Item(preferences?.ActiveWaterAnimationId),
            BadgeEffect = Item(preferences?.ActiveBadgeEffectId),
            MascotAccessory = Item(preferences?.ActiveMascotAccessoryId),
            UpdatedAt = preferences?.UpdatedAt,
            Gamification = gamification
        };
    }

    private static CosmeticItemResponse ToItem(Reward reward, Catalog catalog, IReadOnlySet<int> active) => new()
    {
        Id = reward.Id,
        Code = reward.Code,
        Name = reward.Name,
        Description = reward.Description,
        RewardType = reward.RewardType,
        AssetKey = reward.AssetKey,
        RequiredLevel = reward.RequiredLevel,
        RequiredAchievementCode = reward.RequiredAchievementCode,
        IsStarter = RewardCatalog.IsStarter(reward),
        IsUnlocked = catalog.IsUnlocked(reward),
        UnlockedAt = catalog.Owned.TryGetValue(reward.Id, out var unlockedAt) ? unlockedAt : null,
        IsActive = active.Contains(reward.Id)
    };
}
