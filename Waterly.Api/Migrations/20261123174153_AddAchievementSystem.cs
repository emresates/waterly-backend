using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Waterly.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "WaterIntakes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MetricType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetValue = table.Column<int>(type: "integer", nullable: false),
                    XpReward = table.Column<int>(type: "integer", nullable: false),
                    IconKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsSecret = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.Id);
                    table.CheckConstraint("CK_Achievements_TargetValue", "\"TargetValue\" > 0");
                    table.CheckConstraint("CK_Achievements_XpReward", "\"XpReward\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RewardType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssetKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequiredLevel = table.Column<int>(type: "integer", nullable: true),
                    RequiredAchievementCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.Id);
                    table.CheckConstraint("CK_Rewards_RequiredLevel", "\"RequiredLevel\" IS NULL OR (\"RequiredLevel\" >= 1 AND \"RequiredLevel\" <= 100)");
                    table.CheckConstraint("CK_Rewards_Requirement", "\"RequiredLevel\" IS NOT NULL OR \"RequiredAchievementCode\" IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "UserAchievementEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EventMetadata = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DedupeKey = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievementEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievementEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalXp = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProgress", x => x.Id);
                    table.CheckConstraint("CK_UserProgress_Level", "\"Level\" >= 1 AND \"Level\" <= 100");
                    table.CheckConstraint("CK_UserProgress_TotalXp", "\"TotalXp\" >= 0");
                    table.ForeignKey(
                        name: "FK_UserProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementId = table.Column<int>(type: "integer", nullable: false),
                    UnlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Achievements_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "Achievements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardId = table.Column<int>(type: "integer", nullable: false),
                    UnlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRewards_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRewards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "Code", "Description", "IconKey", "IsActive", "IsSecret", "MetricType", "Name", "Rarity", "SortOrder", "TargetValue", "XpReward" },
                values: new object[,]
                {
                    { 1, "GettingStarted", "WELCOME", "Waterly'de ilk oturumunu başlat.", "welcome", true, false, "SessionStarted", "Su Dünyasına Hoş Geldin", "Common", 1, 1, 10 },
                    { 2, "GettingStarted", "FIRST_DROP", "İlk su kaydını ekle.", "first_drop", true, false, "IntakeCount", "İlk Damla", "Common", 2, 1, 10 },
                    { 3, "GettingStarted", "FIRST_GLASS", "Kayıt olduğun gün en az 250 ml su iç.", "first_glass", true, false, "RegistrationDayMl", "İlk Bardak", "Common", 3, 250, 15 },
                    { 4, "GettingStarted", "FIRST_DAY_500", "Kayıt olduğun gün en az 500 ml su iç.", "first_day_500", true, false, "RegistrationDayMl", "Güzel Başlangıç", "Common", 4, 500, 20 },
                    { 5, "GettingStarted", "FIRST_HALF_GOAL", "Herhangi bir gün günlük hedefinin %50'sine ulaş.", "first_half_goal", true, false, "BestDailyGoalPercentage", "Hedefe İlk Adım", "Common", 5, 50, 20 },
                    { 6, "GettingStarted", "FIRST_GOAL", "Günlük hedefini ilk kez tamamla.", "first_goal", true, false, "GoalCompletedDays", "İlk Zafer", "Rare", 6, 1, 50 },
                    { 7, "GettingStarted", "GOAL_CUSTOMIZED", "Günlük hedefini ilk kez değiştir.", "goal_customized", true, false, "GoalCustomized", "Kendi Yolum", "Common", 7, 1, 15 },
                    { 8, "GettingStarted", "REMINDER_CONFIGURED", "Hatırlatma ayarlarını ilk kez kaydet.", "reminder_configured", true, false, "ReminderSettingsSaved", "Zamanı Geldi", "Common", 8, 1, 20 },
                    { 9, "GettingStarted", "ONBOARDING_COMPLETED", "Uygulama tanıtımını tamamla.", "onboarding_completed", true, false, "OnboardingCompleted", "Hazırım!", "Common", 9, 1, 25 },
                    { 10, "GettingStarted", "ACTIVE_DAYS_7", "Uygulamayı 7 farklı gün aktif olarak kullan.", "active_days_7", true, false, "ActiveDays", "İlk Haftam", "Rare", 10, 7, 40 },
                    { 11, "Streak", "STREAK_2", "Günlük hedefini 2 gün üst üste tamamla.", "streak_2", true, false, "StreakDays", "İstikrarın Başlangıcı", "Common", 11, 2, 30 },
                    { 12, "Streak", "STREAK_3", "Günlük hedefini 3 gün üst üste tamamla.", "streak_3", true, false, "StreakDays", "Üç Damla", "Common", 12, 3, 50 },
                    { 13, "Streak", "STREAK_7", "Günlük hedefini 7 gün üst üste tamamla.", "streak_7", true, false, "StreakDays", "Haftanın Yıldızı", "Rare", 13, 7, 150 },
                    { 14, "Streak", "STREAK_10", "Günlük hedefini 10 gün üst üste tamamla.", "streak_10", true, false, "StreakDays", "10/10", "Rare", 14, 10, 200 },
                    { 15, "Streak", "STREAK_14", "Günlük hedefini 14 gün üst üste tamamla.", "streak_14", true, false, "StreakDays", "İki Haftalık Kahraman", "Rare", 15, 14, 300 },
                    { 16, "Streak", "STREAK_21", "Günlük hedefini 21 gün üst üste tamamla.", "streak_21", true, false, "StreakDays", "Su Ustası", "Epic", 16, 21, 400 },
                    { 17, "Streak", "STREAK_30", "Günlük hedefini 30 gün üst üste tamamla.", "streak_30", true, false, "StreakDays", "Altın Ay", "Epic", 17, 30, 750 },
                    { 18, "Streak", "STREAK_50", "Günlük hedefini 50 gün üst üste tamamla.", "streak_50", true, false, "StreakDays", "Durdurulamaz", "Legendary", 18, 50, 1000 },
                    { 19, "Streak", "STREAK_100", "Günlük hedefini 100 gün üst üste tamamla.", "streak_100", true, false, "StreakDays", "Yüz Günlük Efsane", "Legendary", 19, 100, 2500 },
                    { 20, "Streak", "STREAK_365", "Günlük hedefini 365 gün üst üste tamamla.", "streak_365", true, false, "StreakDays", "Bir Yıllık Yolculuk", "Mythic", 20, 365, 10000 },
                    { 21, "Streak", "SECOND_CHANCE", "Hedefini tamamladığın günlerden sonra en az bir tam gün ara verip hedefini yeniden tamamla.", "second_chance", true, false, "SecondChance", "İkinci Şans", "Common", 21, 1, 30 },
                    { 22, "Streak", "GOAL_DAYS_30", "Toplam 30 gün günlük hedefini tamamla.", "goal_days_30", true, false, "GoalCompletedDays", "İstikrarlı Yolculuk", "Rare", 22, 30, 400 },
                    { 23, "Streak", "GOAL_DAYS_100", "Toplam 100 gün günlük hedefini tamamla.", "goal_days_100", true, false, "GoalCompletedDays", "Su Maratoncusu", "Epic", 23, 100, 1500 },
                    { 24, "Streak", "GOAL_DAYS_365", "Toplam 365 gün günlük hedefini tamamla.", "goal_days_365", true, false, "GoalCompletedDays", "Ömürlük Alışkanlık", "Legendary", 24, 365, 5000 },
                    { 25, "WaterVolume", "TOTAL_ML_1000", "Toplam 1 litre su kaydet.", "total_ml_1000", true, false, "CappedTotalMl", "Bir Litre Mutluluk", "Common", 25, 1000, 20 },
                    { 26, "WaterVolume", "TOTAL_ML_5000", "Toplam 5 litre su kaydet.", "total_ml_5000", true, false, "CappedTotalMl", "Küçük Dere", "Common", 26, 5000, 40 },
                    { 27, "WaterVolume", "TOTAL_ML_10000", "Toplam 10 litre su kaydet.", "total_ml_10000", true, false, "CappedTotalMl", "Akan Su", "Common", 27, 10000, 75 },
                    { 28, "WaterVolume", "TOTAL_ML_25000", "Toplam 25 litre su kaydet.", "total_ml_25000", true, false, "CappedTotalMl", "Mavi Yolculuk", "Rare", 28, 25000, 150 },
                    { 29, "WaterVolume", "TOTAL_ML_50000", "Toplam 50 litre su kaydet.", "total_ml_50000", true, false, "CappedTotalMl", "Küçük Göl", "Rare", 29, 50000, 250 },
                    { 30, "WaterVolume", "TOTAL_ML_100000", "Toplam 100 litre su kaydet.", "total_ml_100000", true, false, "CappedTotalMl", "Su Deposu", "Rare", 30, 100000, 400 },
                    { 31, "WaterVolume", "TOTAL_ML_250000", "Toplam 250 litre su kaydet.", "total_ml_250000", true, false, "CappedTotalMl", "Nehir Yolcusu", "Epic", 31, 250000, 750 },
                    { 32, "WaterVolume", "TOTAL_ML_500000", "Toplam 500 litre su kaydet.", "total_ml_500000", true, false, "CappedTotalMl", "Mavi Nehir", "Epic", 32, 500000, 1250 },
                    { 33, "WaterVolume", "TOTAL_ML_1000000", "Toplam 1.000 litre su kaydet.", "total_ml_1000000", true, false, "CappedTotalMl", "Bin Litrelik Efsane", "Legendary", 33, 1000000, 2500 },
                    { 34, "WaterVolume", "TOTAL_ML_2500000", "Toplam 2.500 litre su kaydet.", "total_ml_2500000", true, false, "CappedTotalMl", "Okyanus Kaşifi", "Legendary", 34, 2500000, 5000 },
                    { 35, "WaterVolume", "TOTAL_ML_5000000", "Toplam 5.000 litre su kaydet.", "total_ml_5000000", true, false, "CappedTotalMl", "Su Gezegeni", "Mythic", 35, 5000000, 10000 },
                    { 36, "WaterVolume", "TOTAL_ML_10000000", "Toplam 10.000 litre su kaydet.", "total_ml_10000000", true, false, "CappedTotalMl", "Sonsuz Mavi", "Mythic", 36, 10000000, 20000 },
                    { 37, "GlassCollection", "GLASSES_1", "1 bardak (250 ml) eşdeğeri su kaydet.", "glasses_1", true, false, "GlassEquivalents", "Bardakla Tanıştık", "Common", 37, 1, 10 },
                    { 38, "GlassCollection", "GLASSES_5", "5 bardak eşdeğeri su kaydet.", "glasses_5", true, false, "GlassEquivalents", "Beşlik Çak", "Common", 38, 5, 25 },
                    { 39, "GlassCollection", "GLASSES_10", "10 bardak eşdeğeri su kaydet.", "glasses_10", true, false, "GlassEquivalents", "İlk Onluk", "Common", 39, 10, 40 },
                    { 40, "GlassCollection", "GLASSES_25", "25 bardak eşdeğeri su kaydet.", "glasses_25", true, false, "GlassEquivalents", "Bardak Koleksiyoncusu", "Common", 40, 25, 75 },
                    { 41, "GlassCollection", "GLASSES_50", "50 bardak eşdeğeri su kaydet.", "glasses_50", true, false, "GlassEquivalents", "Ellilik Kulüp", "Rare", 41, 50, 125 },
                    { 42, "GlassCollection", "GLASSES_100", "100 bardak eşdeğeri su kaydet.", "glasses_100", true, false, "GlassEquivalents", "Bardak Ustası", "Rare", 42, 100, 250 },
                    { 43, "GlassCollection", "GLASSES_250", "250 bardak eşdeğeri su kaydet.", "glasses_250", true, false, "GlassEquivalents", "Damla Biriktiricisi", "Rare", 43, 250, 500 },
                    { 44, "GlassCollection", "GLASSES_500", "500 bardak eşdeğeri su kaydet.", "glasses_500", true, false, "GlassEquivalents", "Mavi Koleksiyon", "Epic", 44, 500, 800 },
                    { 45, "GlassCollection", "GLASSES_1000", "1.000 bardak eşdeğeri su kaydet.", "glasses_1000", true, false, "GlassEquivalents", "Bin Bardak", "Epic", 45, 1000, 1500 },
                    { 46, "GlassCollection", "GLASSES_5000", "5.000 bardak eşdeğeri su kaydet.", "glasses_5000", true, false, "GlassEquivalents", "Sonsuz Bardak", "Legendary", 46, 5000, 5000 },
                    { 47, "DailyHabits", "MORNING_FIRST", "Günün ilk su kaydını 07:00-09:59 arasında yap.", "morning_first", true, false, "MorningStartDays", "Günaydın Damlası", "Common", 47, 1, 20 },
                    { 48, "DailyHabits", "MORNING_DAYS_7", "7 farklı gün, günün ilk su kaydını 07:00-09:59 arasında yap.", "morning_days_7", true, false, "MorningStartDays", "Sabah Rutini", "Common", 48, 7, 75 },
                    { 49, "DailyHabits", "NOON_DAYS_5", "5 farklı gün 12:00-13:59 arasında su iç.", "noon_days_5", true, false, "NoonDays", "Öğle Molası", "Common", 49, 5, 50 },
                    { 50, "DailyHabits", "EVENING_DAYS_7", "7 farklı gün 18:00-20:59 arasında su iç.", "evening_days_7", true, false, "EveningDays", "Akşamın Damlası", "Common", 50, 7, 75 },
                    { 51, "DailyHabits", "BALANCED_DAY_FIRST", "Bir gün içinde sabah (06-12), öğleden sonra (12-18) ve akşam (18-22) su iç.", "balanced_day_first", true, false, "BalancedDays", "Dengeli Gün", "Common", 51, 1, 40 },
                    { 52, "DailyHabits", "BALANCED_DAYS_5", "5 dengeli gün tamamla.", "balanced_days_5", true, false, "BalancedDays", "Rutin Oluşturucu", "Rare", 52, 5, 100 },
                    { 53, "DailyHabits", "BALANCED_DAYS_14", "14 dengeli gün tamamla.", "balanced_days_14", true, false, "BalancedDays", "Planlı Yaşam", "Rare", 53, 14, 250 },
                    { 54, "DailyHabits", "REMINDER_ACTION_FIRST", "Bir hatırlatma bildiriminden ilk kez su kaydı ekle.", "reminder_action_first", true, false, "ReminderActionDays", "Hatırladım!", "Common", 54, 1, 25 },
                    { 55, "DailyHabits", "REMINDER_ACTION_DAYS_10", "10 farklı gün hatırlatma bildiriminden su kaydı ekle.", "reminder_action_days_10", true, false, "ReminderActionDays", "Bildirim Dostu", "Rare", 55, 10, 100 },
                    { 56, "DailyHabits", "REMINDER_ACTION_DAYS_30", "30 farklı gün hatırlatma bildiriminden su kaydı ekle.", "reminder_action_days_30", true, false, "ReminderActionDays", "Akıllı Alışkanlık", "Epic", 56, 30, 350 },
                    { 57, "Discovery", "STATS_FIRST_VIEW", "İstatistik ekranını ilk kez aç.", "stats_first_view", true, false, "StatisticsViewed", "Meraklı Damla", "Common", 57, 1, 10 },
                    { 58, "Discovery", "HISTORY_FIRST_VIEW", "Geçmiş ekranını ilk kez aç.", "history_first_view", true, false, "HistoryViewed", "Geçmişe Yolculuk", "Common", 58, 1, 15 },
                    { 59, "Discovery", "WEEKLY_REPORT_WEEKS_5", "5 farklı takvim haftasında haftalık raporu aç.", "weekly_report_weeks_5", true, false, "WeeklyReportWeeks", "Veri Dedektifi", "Rare", 59, 5, 50 },
                    { 60, "Discovery", "MONTHLY_REPORT_FIRST", "Aylık raporu ilk kez aç.", "monthly_report_first", true, false, "MonthlyReportViewed", "Aylık Rapor", "Common", 60, 1, 25 },
                    { 61, "Discovery", "CALENDAR_DAY_VIEW", "Takvimden geçmiş bir günü incele.", "calendar_day_view", true, false, "CalendarDayViewed", "Takvim Kaşifi", "Common", 61, 1, 20 },
                    { 62, "Discovery", "REMINDER_CUSTOMIZED", "Hatırlatma ayarlarını varsayılandan farklı olacak şekilde kişiselleştir.", "reminder_customized", true, false, "ReminderCustomized", "Kendi Ritmim", "Common", 62, 1, 30 },
                    { 63, "Discovery", "GLASS_SIZE_CUSTOMIZED", "Varsayılan bardak boyutunu değiştir.", "glass_size_customized", true, false, "GlassSizeCustomized", "Kişisel Şişem", "Common", 63, 1, 20 },
                    { 64, "Discovery", "HEALTH_CONNECTED", "Apple Health veya Health Connect'ten ilk gerçek aktivite verisini aktar.", "health_connected", true, false, "HealthConnected", "Sağlık Bağlantısı", "Rare", 64, 1, 50 },
                    { 65, "Discovery", "HEALTH_RECOMMENDATION_VIEW", "Sağlık önerisi ekranını ilk kez aç.", "health_recommendation_view", true, false, "HealthRecommendationViewed", "Aktivite Kaşifi", "Common", 65, 1, 25 },
                    { 66, "Discovery", "WEEKLY_REPORT_WEEKS_4", "4 farklı takvim haftasında haftalık raporu aç.", "weekly_report_weeks_4", true, false, "WeeklyReportWeeks", "Analiz Ustası", "Rare", 66, 4, 100 },
                    { 67, "Secret", "SECRET_FIRST", "Kendisi dışındaki ilk gizli başarımı aç.", "secret_first", true, true, "SecretAchievementsUnlocked", "Gizemli Damla", "Rare", 67, 1, 100 },
                    { 68, "Secret", "SECRET_DARK_MODE", "Karanlık modu ilk kez etkinleştir.", "secret_dark_mode", true, true, "DarkThemeEnabled", "Gece Gökyüzü", "Rare", 68, 1, 50 },
                    { 69, "Secret", "SECRET_THEMES_3", "3 farklı şişe teması kullan.", "secret_themes_3", true, true, "BottleThemesUsed", "Renklerin Dansı", "Rare", 69, 3, 75 },
                    { 70, "Secret", "SECRET_BOTTLE_TAPS_10", "Şişeye 10 kez dokunarak gizli animasyonu keşfet.", "secret_bottle_taps_10", true, true, "BottleEasterEggDiscovered", "Küçük Sürpriz", "Rare", 70, 1, 40 },
                    { 71, "Secret", "SECRET_RETURN_14_DAYS", "En az 14 tam gün aradan sonra uygulamaya geri dön.", "secret_return_14_days", true, true, "LongestInactiveGapDays", "Hoş Geldin Geriye", "Rare", 71, 14, 75 },
                    { 72, "Secret", "SECRET_COMEBACK_3_DAYS", "Uzun bir aradan döndükten sonra 3 farklı gün aktif ol.", "secret_comeback_3_days", true, true, "ComebackActiveDays", "Yeniden Başladık", "Epic", 72, 3, 100 },
                    { 73, "Secret", "SECRET_TIMEZONE_TRAVEL", "Öncekinden farklı bir saat diliminde su kaydı ekle.", "secret_timezone_travel", true, true, "TimeZonesUsed", "Mavi Gezgin", "Epic", 73, 2, 100 },
                    { 74, "Secret", "SECRET_ACHIEVEMENTS_10", "10 farklı başarım aç.", "secret_achievements_10", true, true, "AchievementsUnlocked", "Koleksiyon Avcısı", "Rare", 74, 10, 200 },
                    { 75, "Secret", "SECRET_ACHIEVEMENTS_25", "25 farklı başarım aç.", "secret_achievements_25", true, true, "AchievementsUnlocked", "Hazine Sandığı", "Epic", 75, 25, 500 },
                    { 76, "Secret", "SECRET_ACHIEVEMENTS_50", "50 farklı başarım aç.", "secret_achievements_50", true, true, "AchievementsUnlocked", "Efsane Avcısı", "Legendary", 76, 50, 1000 }
                });

            migrationBuilder.InsertData(
                table: "Rewards",
                columns: new[] { "Id", "AssetKey", "Code", "Description", "Name", "RequiredAchievementCode", "RequiredLevel", "RewardType" },
                values: new object[,]
                {
                    { 1, "bottle_sky_blue", "BOTTLE_SKY_BLUE", "Açık mavi şişe teması.", "Gökyüzü Mavisi Şişe", null, 5, "BottleTheme" },
                    { 2, "frame_aqua", "FRAME_AQUA", "Profil fotoğrafın için aqua çerçeve.", "Aqua Çerçeve", null, 10, "ProfileFrame" },
                    { 3, "animation_bubbles", "ANIMATION_BUBBLES", "Su eklerken baloncuk animasyonu.", "Baloncuklar", null, 15, "WaterAnimation" },
                    { 4, "bottle_frost", "BOTTLE_FROST", "Buz kristalli şişe teması.", "Buzlu Şişe", null, 20, "BottleTheme" },
                    { 5, "frame_glow", "FRAME_GLOW", "Hafifçe parlayan profil çerçevesi.", "Parıltılı Çerçeve", null, 30, "ProfileFrame" },
                    { 6, "water_sparkle", "WATER_SPARKLE", "Suya ışıltı efekti.", "Işıltılı Su", null, 40, "WaterAnimation" },
                    { 7, "bottle_gold", "BOTTLE_GOLD", "Altın şişe teması.", "Altın Şişe", null, 50, "BottleTheme" },
                    { 8, "frame_aurora", "FRAME_AURORA", "Kuzey ışıkları renklerinde profil çerçevesi.", "Aurora Çerçeve", null, 75, "ProfileFrame" },
                    { 9, "bottle_mythic", "BOTTLE_MYTHIC", "Yalnızca 100. seviyede açılan efsanevi şişe teması.", "Efsanevi Şişe", null, 100, "BottleTheme" },
                    { 10, "mascot_sunglasses", "MASCOT_SUNGLASSES", "Maskot için güneş gözlüğü.", "Damla Gözlüğü", "STREAK_7", null, "MascotAccessory" },
                    { 11, "badge_shine", "BADGE_SHINE", "Rozetlerine parıltı efekti.", "Rozet Parıltısı", "STREAK_30", null, "BadgeEffect" },
                    { 12, "mascot_crown", "MASCOT_CROWN", "Maskot için taç.", "Damla Tacı", "STREAK_100", null, "MascotAccessory" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Code",
                table: "Achievements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_Code",
                table: "Rewards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievementEvents_UserId_ClientEventId",
                table: "UserAchievementEvents",
                columns: new[] { "UserId", "ClientEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievementEvents_UserId_EventType_DedupeKey",
                table: "UserAchievementEvents",
                columns: new[] { "UserId", "EventType", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_AchievementId",
                table: "UserAchievements",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_UnlockedAt",
                table: "UserAchievements",
                columns: new[] { "UserId", "UnlockedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProgress_UserId",
                table: "UserProgress",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRewards_RewardId",
                table: "UserRewards",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRewards_UserId_RewardId",
                table: "UserRewards",
                columns: new[] { "UserId", "RewardId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAchievementEvents");

            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "UserProgress");

            migrationBuilder.DropTable(
                name: "UserRewards");

            migrationBuilder.DropTable(
                name: "Achievements");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "WaterIntakes");
        }
    }
}
