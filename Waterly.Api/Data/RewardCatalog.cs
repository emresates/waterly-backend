using Waterly.Api.Models;

namespace Waterly.Api.Data;

// Cosmetic rewards, seeded into the database by EF Core migrations (HasData).
// Id and Code are permanent. AssetKey points to a file bundled in the mobile app; no files are stored on the server.
public static class RewardCatalog
{
    public static readonly IReadOnlyList<Reward> All =
    [
        // Level rewards
        Level(1, "BOTTLE_SKY_BLUE", "Gökyüzü Mavisi Şişe", "Açık mavi şişe teması.", RewardType.BottleTheme, 5),
        Level(2, "FRAME_AQUA", "Aqua Çerçeve", "Profil fotoğrafın için aqua çerçeve.", RewardType.ProfileFrame, 10),
        Level(3, "ANIMATION_BUBBLES", "Baloncuklar", "Su eklerken baloncuk animasyonu.", RewardType.WaterAnimation, 15),
        Level(4, "BOTTLE_FROST", "Buzlu Şişe", "Buz kristalli şişe teması.", RewardType.BottleTheme, 20),
        Level(5, "FRAME_GLOW", "Parıltılı Çerçeve", "Hafifçe parlayan profil çerçevesi.", RewardType.ProfileFrame, 30),
        Level(6, "WATER_SPARKLE", "Işıltılı Su", "Suya ışıltı efekti.", RewardType.WaterAnimation, 40),
        Level(7, "BOTTLE_GOLD", "Altın Şişe", "Altın şişe teması.", RewardType.BottleTheme, 50),
        Level(8, "FRAME_AURORA", "Aurora Çerçeve", "Kuzey ışıkları renklerinde profil çerçevesi.", RewardType.ProfileFrame, 75),
        Level(9, "BOTTLE_MYTHIC", "Efsanevi Şişe", "Yalnızca 100. seviyede açılan efsanevi şişe teması.", RewardType.BottleTheme, 100),

        // Achievement rewards
        ForAchievement(10, "MASCOT_SUNGLASSES", "Damla Gözlüğü", "Maskot için güneş gözlüğü.", RewardType.MascotAccessory, "STREAK_7"),
        ForAchievement(11, "BADGE_SHINE", "Rozet Parıltısı", "Rozetlerine parıltı efekti.", RewardType.BadgeEffect, "STREAK_30"),
        ForAchievement(12, "MASCOT_CROWN", "Damla Tacı", "Maskot için taç.", RewardType.MascotAccessory, "STREAK_100"),

        // Starter bottle themes: level 1, so every user owns them from the first evaluation (registration).
        Level(13, DefaultBottleThemeCode, "Klasik Şişe", "Waterly'nin klasik şişe teması.", RewardType.BottleTheme, 1),
        Level(14, "BOTTLE_OCEAN", "Okyanus Şişe", "Derin mavi okyanus teması.", RewardType.BottleTheme, 1),
        Level(15, "BOTTLE_MINT", "Nane Şişe", "Ferah nane yeşili tema.", RewardType.BottleTheme, 1)
    ];

    // Used when the user has never picked a theme.
    public const string DefaultBottleThemeCode = "BOTTLE_CLASSIC";

    // Starter rewards are free for everyone, even before the first evaluation has stored them in UserRewards.
    public static bool IsStarter(Reward reward) => reward.RequiredLevel is <= 1 && reward.RequiredAchievementCode is null;

    private static Reward Level(int id, string code, string name, string description, RewardType type, int requiredLevel) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        Description = description,
        RewardType = type,
        AssetKey = code.ToLowerInvariant(),
        RequiredLevel = requiredLevel
    };

    private static Reward ForAchievement(int id, string code, string name, string description, RewardType type, string achievementCode) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        Description = description,
        RewardType = type,
        AssetKey = code.ToLowerInvariant(),
        RequiredAchievementCode = achievementCode
    };
}
