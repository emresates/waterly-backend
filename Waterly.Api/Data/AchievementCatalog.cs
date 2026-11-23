using Waterly.Api.Models;
using static Waterly.Api.Models.AchievementCategory;
using static Waterly.Api.Models.AchievementMetric;
using static Waterly.Api.Models.AchievementRarity;

namespace Waterly.Api.Data;

// The single source of truth for all achievements. It is seeded into the database by EF Core migrations (HasData).
//
// Rules:
// - Id and Code are permanent. Never change or reuse them; add new achievements with new ids at the end.
// - To retire an achievement set IsActive = false instead of deleting it, so users keep what they earned.
// - Name / Description are the Turkish defaults. Translations can be added later keyed by Code.
public static class AchievementCatalog
{
    public static readonly IReadOnlyList<Achievement> All = Build();

    private static List<Achievement> Build()
    {
        var items = new List<Achievement>();

        void Add(string code, string name, string description, AchievementCategory category,
            AchievementRarity rarity, AchievementMetric metric, int target, int xp, bool isSecret = false)
        {
            var id = items.Count + 1;
            items.Add(new Achievement
            {
                Id = id,
                Code = code,
                Name = name,
                Description = description,
                Category = category,
                Rarity = rarity,
                MetricType = metric,
                TargetValue = target,
                XpReward = xp,
                IconKey = code.ToLowerInvariant(),
                IsSecret = isSecret,
                IsActive = true,
                SortOrder = id
            });
        }

        // ---------------- GettingStarted (10) ----------------
        Add("WELCOME", "Su Dünyasına Hoş Geldin", "Waterly'de ilk oturumunu başlat.", GettingStarted, Common, SessionStarted, 1, 10);
        Add("FIRST_DROP", "İlk Damla", "İlk su kaydını ekle.", GettingStarted, Common, IntakeCount, 1, 10);
        Add("FIRST_GLASS", "İlk Bardak", "Kayıt olduğun gün en az 250 ml su iç.", GettingStarted, Common, RegistrationDayMl, 250, 15);
        Add("FIRST_DAY_500", "Güzel Başlangıç", "Kayıt olduğun gün en az 500 ml su iç.", GettingStarted, Common, RegistrationDayMl, 500, 20);
        Add("FIRST_HALF_GOAL", "Hedefe İlk Adım", "Herhangi bir gün günlük hedefinin %50'sine ulaş.", GettingStarted, Common, BestDailyGoalPercentage, 50, 20);
        Add("FIRST_GOAL", "İlk Zafer", "Günlük hedefini ilk kez tamamla.", GettingStarted, Rare, GoalCompletedDays, 1, 50);
        Add("GOAL_CUSTOMIZED", "Kendi Yolum", "Günlük hedefini ilk kez değiştir.", GettingStarted, Common, GoalCustomized, 1, 15);
        Add("REMINDER_CONFIGURED", "Zamanı Geldi", "Hatırlatma ayarlarını ilk kez kaydet.", GettingStarted, Common, ReminderSettingsSaved, 1, 20);
        Add("ONBOARDING_COMPLETED", "Hazırım!", "Uygulama tanıtımını tamamla.", GettingStarted, Common, OnboardingCompleted, 1, 25);
        Add("ACTIVE_DAYS_7", "İlk Haftam", "Uygulamayı 7 farklı gün aktif olarak kullan.", GettingStarted, Rare, ActiveDays, 7, 40);

        // ---------------- Streak (14) ----------------
        Add("STREAK_2", "İstikrarın Başlangıcı", "Günlük hedefini 2 gün üst üste tamamla.", Streak, Common, StreakDays, 2, 30);
        Add("STREAK_3", "Üç Damla", "Günlük hedefini 3 gün üst üste tamamla.", Streak, Common, StreakDays, 3, 50);
        Add("STREAK_7", "Haftanın Yıldızı", "Günlük hedefini 7 gün üst üste tamamla.", Streak, Rare, StreakDays, 7, 150);
        Add("STREAK_10", "10/10", "Günlük hedefini 10 gün üst üste tamamla.", Streak, Rare, StreakDays, 10, 200);
        Add("STREAK_14", "İki Haftalık Kahraman", "Günlük hedefini 14 gün üst üste tamamla.", Streak, Rare, StreakDays, 14, 300);
        Add("STREAK_21", "Su Ustası", "Günlük hedefini 21 gün üst üste tamamla.", Streak, Epic, StreakDays, 21, 400);
        Add("STREAK_30", "Altın Ay", "Günlük hedefini 30 gün üst üste tamamla.", Streak, Epic, StreakDays, 30, 750);
        Add("STREAK_50", "Durdurulamaz", "Günlük hedefini 50 gün üst üste tamamla.", Streak, Legendary, StreakDays, 50, 1000);
        Add("STREAK_100", "Yüz Günlük Efsane", "Günlük hedefini 100 gün üst üste tamamla.", Streak, Legendary, StreakDays, 100, 2500);
        Add("STREAK_365", "Bir Yıllık Yolculuk", "Günlük hedefini 365 gün üst üste tamamla.", Streak, Mythic, StreakDays, 365, 10000);
        Add("SECOND_CHANCE", "İkinci Şans", "Hedefini tamamladığın günlerden sonra en az bir tam gün ara verip hedefini yeniden tamamla.", Streak, Common, SecondChance, 1, 30);
        Add("GOAL_DAYS_30", "İstikrarlı Yolculuk", "Toplam 30 gün günlük hedefini tamamla.", Streak, Rare, GoalCompletedDays, 30, 400);
        Add("GOAL_DAYS_100", "Su Maratoncusu", "Toplam 100 gün günlük hedefini tamamla.", Streak, Epic, GoalCompletedDays, 100, 1500);
        Add("GOAL_DAYS_365", "Ömürlük Alışkanlık", "Toplam 365 gün günlük hedefini tamamla.", Streak, Legendary, GoalCompletedDays, 365, 5000);

        // ---------------- WaterVolume (12) ----------------
        // Each day counts at most up to that day's goal, so drinking extra does not speed these up.
        Add("TOTAL_ML_1000", "Bir Litre Mutluluk", "Toplam 1 litre su kaydet.", WaterVolume, Common, CappedTotalMl, 1_000, 20);
        Add("TOTAL_ML_5000", "Küçük Dere", "Toplam 5 litre su kaydet.", WaterVolume, Common, CappedTotalMl, 5_000, 40);
        Add("TOTAL_ML_10000", "Akan Su", "Toplam 10 litre su kaydet.", WaterVolume, Common, CappedTotalMl, 10_000, 75);
        Add("TOTAL_ML_25000", "Mavi Yolculuk", "Toplam 25 litre su kaydet.", WaterVolume, Rare, CappedTotalMl, 25_000, 150);
        Add("TOTAL_ML_50000", "Küçük Göl", "Toplam 50 litre su kaydet.", WaterVolume, Rare, CappedTotalMl, 50_000, 250);
        Add("TOTAL_ML_100000", "Su Deposu", "Toplam 100 litre su kaydet.", WaterVolume, Rare, CappedTotalMl, 100_000, 400);
        Add("TOTAL_ML_250000", "Nehir Yolcusu", "Toplam 250 litre su kaydet.", WaterVolume, Epic, CappedTotalMl, 250_000, 750);
        Add("TOTAL_ML_500000", "Mavi Nehir", "Toplam 500 litre su kaydet.", WaterVolume, Epic, CappedTotalMl, 500_000, 1250);
        Add("TOTAL_ML_1000000", "Bin Litrelik Efsane", "Toplam 1.000 litre su kaydet.", WaterVolume, Legendary, CappedTotalMl, 1_000_000, 2500);
        Add("TOTAL_ML_2500000", "Okyanus Kaşifi", "Toplam 2.500 litre su kaydet.", WaterVolume, Legendary, CappedTotalMl, 2_500_000, 5000);
        Add("TOTAL_ML_5000000", "Su Gezegeni", "Toplam 5.000 litre su kaydet.", WaterVolume, Mythic, CappedTotalMl, 5_000_000, 10000);
        Add("TOTAL_ML_10000000", "Sonsuz Mavi", "Toplam 10.000 litre su kaydet.", WaterVolume, Mythic, CappedTotalMl, 10_000_000, 20000);

        // ---------------- GlassCollection (10) ----------------
        // One glass equivalent = 250 ml of the same capped total (not the number of records).
        Add("GLASSES_1", "Bardakla Tanıştık", "1 bardak (250 ml) eşdeğeri su kaydet.", GlassCollection, Common, GlassEquivalents, 1, 10);
        Add("GLASSES_5", "Beşlik Çak", "5 bardak eşdeğeri su kaydet.", GlassCollection, Common, GlassEquivalents, 5, 25);
        Add("GLASSES_10", "İlk Onluk", "10 bardak eşdeğeri su kaydet.", GlassCollection, Common, GlassEquivalents, 10, 40);
        Add("GLASSES_25", "Bardak Koleksiyoncusu", "25 bardak eşdeğeri su kaydet.", GlassCollection, Common, GlassEquivalents, 25, 75);
        Add("GLASSES_50", "Ellilik Kulüp", "50 bardak eşdeğeri su kaydet.", GlassCollection, Rare, GlassEquivalents, 50, 125);
        Add("GLASSES_100", "Bardak Ustası", "100 bardak eşdeğeri su kaydet.", GlassCollection, Rare, GlassEquivalents, 100, 250);
        Add("GLASSES_250", "Damla Biriktiricisi", "250 bardak eşdeğeri su kaydet.", GlassCollection, Rare, GlassEquivalents, 250, 500);
        Add("GLASSES_500", "Mavi Koleksiyon", "500 bardak eşdeğeri su kaydet.", GlassCollection, Epic, GlassEquivalents, 500, 800);
        Add("GLASSES_1000", "Bin Bardak", "1.000 bardak eşdeğeri su kaydet.", GlassCollection, Epic, GlassEquivalents, 1_000, 1500);
        Add("GLASSES_5000", "Sonsuz Bardak", "5.000 bardak eşdeğeri su kaydet.", GlassCollection, Legendary, GlassEquivalents, 5_000, 5000);

        // ---------------- DailyHabits (10) ----------------
        Add("MORNING_FIRST", "Günaydın Damlası", "Günün ilk su kaydını 07:00-09:59 arasında yap.", DailyHabits, Common, MorningStartDays, 1, 20);
        Add("MORNING_DAYS_7", "Sabah Rutini", "7 farklı gün, günün ilk su kaydını 07:00-09:59 arasında yap.", DailyHabits, Common, MorningStartDays, 7, 75);
        Add("NOON_DAYS_5", "Öğle Molası", "5 farklı gün 12:00-13:59 arasında su iç.", DailyHabits, Common, NoonDays, 5, 50);
        Add("EVENING_DAYS_7", "Akşamın Damlası", "7 farklı gün 18:00-20:59 arasında su iç.", DailyHabits, Common, EveningDays, 7, 75);
        Add("BALANCED_DAY_FIRST", "Dengeli Gün", "Bir gün içinde sabah (06-12), öğleden sonra (12-18) ve akşam (18-22) su iç.", DailyHabits, Common, BalancedDays, 1, 40);
        Add("BALANCED_DAYS_5", "Rutin Oluşturucu", "5 dengeli gün tamamla.", DailyHabits, Rare, BalancedDays, 5, 100);
        Add("BALANCED_DAYS_14", "Planlı Yaşam", "14 dengeli gün tamamla.", DailyHabits, Rare, BalancedDays, 14, 250);
        Add("REMINDER_ACTION_FIRST", "Hatırladım!", "Bir hatırlatma bildiriminden ilk kez su kaydı ekle.", DailyHabits, Common, ReminderActionDays, 1, 25);
        Add("REMINDER_ACTION_DAYS_10", "Bildirim Dostu", "10 farklı gün hatırlatma bildiriminden su kaydı ekle.", DailyHabits, Rare, ReminderActionDays, 10, 100);
        Add("REMINDER_ACTION_DAYS_30", "Akıllı Alışkanlık", "30 farklı gün hatırlatma bildiriminden su kaydı ekle.", DailyHabits, Epic, ReminderActionDays, 30, 350);

        // ---------------- Discovery (10) ----------------
        Add("STATS_FIRST_VIEW", "Meraklı Damla", "İstatistik ekranını ilk kez aç.", Discovery, Common, StatisticsViewed, 1, 10);
        Add("HISTORY_FIRST_VIEW", "Geçmişe Yolculuk", "Geçmiş ekranını ilk kez aç.", Discovery, Common, HistoryViewed, 1, 15);
        Add("WEEKLY_REPORT_WEEKS_5", "Veri Dedektifi", "5 farklı takvim haftasında haftalık raporu aç.", Discovery, Rare, WeeklyReportWeeks, 5, 50);
        Add("MONTHLY_REPORT_FIRST", "Aylık Rapor", "Aylık raporu ilk kez aç.", Discovery, Common, MonthlyReportViewed, 1, 25);
        Add("CALENDAR_DAY_VIEW", "Takvim Kaşifi", "Takvimden geçmiş bir günü incele.", Discovery, Common, CalendarDayViewed, 1, 20);
        Add("REMINDER_CUSTOMIZED", "Kendi Ritmim", "Hatırlatma ayarlarını varsayılandan farklı olacak şekilde kişiselleştir.", Discovery, Common, ReminderCustomized, 1, 30);
        Add("GLASS_SIZE_CUSTOMIZED", "Kişisel Şişem", "Varsayılan bardak boyutunu değiştir.", Discovery, Common, GlassSizeCustomized, 1, 20);
        Add("HEALTH_CONNECTED", "Sağlık Bağlantısı", "Apple Health veya Health Connect'ten ilk gerçek aktivite verisini aktar.", Discovery, Rare, HealthConnected, 1, 50);
        Add("HEALTH_RECOMMENDATION_VIEW", "Aktivite Kaşifi", "Sağlık önerisi ekranını ilk kez aç.", Discovery, Common, HealthRecommendationViewed, 1, 25);
        Add("WEEKLY_REPORT_WEEKS_4", "Analiz Ustası", "4 farklı takvim haftasında haftalık raporu aç.", Discovery, Rare, WeeklyReportWeeks, 4, 100);

        // ---------------- Secret (10) ----------------
        Add("SECRET_FIRST", "Gizemli Damla", "Kendisi dışındaki ilk gizli başarımı aç.", Secret, Rare, SecretAchievementsUnlocked, 1, 100, isSecret: true);
        Add("SECRET_DARK_MODE", "Gece Gökyüzü", "Karanlık modu ilk kez etkinleştir.", Secret, Rare, DarkThemeEnabled, 1, 50, isSecret: true);
        Add("SECRET_THEMES_3", "Renklerin Dansı", "3 farklı şişe teması kullan.", Secret, Rare, BottleThemesUsed, 3, 75, isSecret: true);
        Add("SECRET_BOTTLE_TAPS_10", "Küçük Sürpriz", "Şişeye 10 kez dokunarak gizli animasyonu keşfet.", Secret, Rare, BottleEasterEggDiscovered, 1, 40, isSecret: true);
        Add("SECRET_RETURN_14_DAYS", "Hoş Geldin Geriye", "En az 14 tam gün aradan sonra uygulamaya geri dön.", Secret, Rare, LongestInactiveGapDays, 14, 75, isSecret: true);
        Add("SECRET_COMEBACK_3_DAYS", "Yeniden Başladık", "Uzun bir aradan döndükten sonra 3 farklı gün aktif ol.", Secret, Epic, ComebackActiveDays, 3, 100, isSecret: true);
        Add("SECRET_TIMEZONE_TRAVEL", "Mavi Gezgin", "Öncekinden farklı bir saat diliminde su kaydı ekle.", Secret, Epic, TimeZonesUsed, 2, 100, isSecret: true);
        Add("SECRET_ACHIEVEMENTS_10", "Koleksiyon Avcısı", "10 farklı başarım aç.", Secret, Rare, AchievementsUnlocked, 10, 200, isSecret: true);
        Add("SECRET_ACHIEVEMENTS_25", "Hazine Sandığı", "25 farklı başarım aç.", Secret, Epic, AchievementsUnlocked, 25, 500, isSecret: true);
        Add("SECRET_ACHIEVEMENTS_50", "Efsane Avcısı", "50 farklı başarım aç.", Secret, Legendary, AchievementsUnlocked, 50, 1000, isSecret: true);

        return items;
    }
}
