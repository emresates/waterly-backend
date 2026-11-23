using Microsoft.EntityFrameworkCore;
using Waterly.Api.Models;

namespace Waterly.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WaterIntake> WaterIntakes => Set<WaterIntake>();
    public DbSet<HydrationGoal> HydrationGoals => Set<HydrationGoal>();
    public DbSet<ReminderSettings> ReminderSettings => Set<ReminderSettings>();
    public DbSet<DailyActivity> DailyActivities => Set<DailyActivity>();

    // Achievement system
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<UserReward> UserRewards => Set<UserReward>();
    public DbSet<UserAchievementEvent> UserAchievementEvents => Set<UserAchievementEvent>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureWaterIntake(modelBuilder);
        ConfigureHydrationGoal(modelBuilder);
        ConfigureReminderSettings(modelBuilder);
        ConfigureDailyActivity(modelBuilder);

        ConfigureAchievement(modelBuilder);
        ConfigureUserAchievement(modelBuilder);
        ConfigureUserProgress(modelBuilder);
        ConfigureReward(modelBuilder);
        ConfigureUserReward(modelBuilder);
        ConfigureUserAchievementEvent(modelBuilder);
        ConfigureUserPreferences(modelBuilder);
    }

    // DateTimeOffset -> "timestamp with time zone" (always UTC)
    // DateOnly       -> "date"
    // TimeOnly       -> "time without time zone"
    // These are the Npgsql defaults; they are written explicitly here to make the mapping obvious.

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.TimeZone).IsRequired().HasMaxLength(64);
            entity.Property(u => u.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(u => u.UpdatedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(u => u.Email).IsUnique();

            // Deleting a user deletes every piece of personal data that belongs to them.
            entity.HasMany(u => u.RefreshTokens).WithOne(t => t.User).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(u => u.WaterIntakes).WithOne(i => i.User).HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(u => u.HydrationGoals).WithOne(g => g.User).HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(u => u.DailyActivities).WithOne(a => a.User).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(u => u.ReminderSettings).WithOne(r => r.User).HasForeignKey<ReminderSettings>(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRefreshToken(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
            entity.Property(t => t.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(t => t.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(t => t.RevokedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);
        });
    }

    private static void ConfigureWaterIntake(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WaterIntake>(entity =>
        {
            entity.ToTable("WaterIntakes", table =>
            {
                table.HasCheckConstraint("CK_WaterIntakes_AmountMl", "\"AmountMl\" > 0 AND \"AmountMl\" <= 5000");
            });
            entity.HasKey(i => i.Id);

            entity.Property(i => i.ConsumedAt).HasColumnType("timestamp with time zone");
            entity.Property(i => i.LocalDate).HasColumnType("date");
            entity.Property(i => i.TimeZone).IsRequired().HasMaxLength(64);
            entity.Property(i => i.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(i => i.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.Property(i => i.Source).HasConversion<string>().HasMaxLength(32);

            // Idempotency: the same client event can only be stored once per user.
            entity.HasIndex(i => new { i.UserId, i.ClientEventId }).IsUnique();

            // Daily totals, history and statistics all filter by user + local day.
            entity.HasIndex(i => new { i.UserId, i.LocalDate });
            entity.HasIndex(i => new { i.UserId, i.ConsumedAt });
        });
    }

    private static void ConfigureHydrationGoal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HydrationGoal>(entity =>
        {
            entity.ToTable("HydrationGoals", table =>
            {
                table.HasCheckConstraint("CK_HydrationGoals_TargetMl", "\"TargetMl\" >= 500 AND \"TargetMl\" <= 10000");
            });
            entity.HasKey(g => g.Id);

            entity.Property(g => g.EffectiveFrom).HasColumnType("date");
            entity.Property(g => g.CreatedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(g => new { g.UserId, g.EffectiveFrom });
        });
    }

    private static void ConfigureReminderSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReminderSettings>(entity =>
        {
            entity.ToTable("ReminderSettings", table =>
            {
                table.HasCheckConstraint("CK_ReminderSettings_IntervalMinutes", "\"IntervalMinutes\" >= 15 AND \"IntervalMinutes\" <= 720");
                table.HasCheckConstraint("CK_ReminderSettings_DefaultGlassMl", "\"DefaultGlassMl\" > 0 AND \"DefaultGlassMl\" <= 5000");
            });
            entity.HasKey(r => r.Id);

            entity.Property(r => r.StartTime).HasColumnType("time without time zone");
            entity.Property(r => r.EndTime).HasColumnType("time without time zone");
            entity.Property(r => r.TimeZone).IsRequired().HasMaxLength(64);
            entity.Property(r => r.UpdatedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(r => r.UserId).IsUnique();
        });
    }

    private static void ConfigureDailyActivity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyActivity>(entity =>
        {
            entity.ToTable("DailyActivities", table =>
            {
                table.HasCheckConstraint("CK_DailyActivities_Steps", "\"Steps\" >= 0");
                table.HasCheckConstraint("CK_DailyActivities_ExerciseMinutes", "\"ExerciseMinutes\" >= 0 AND \"ExerciseMinutes\" <= 1440");
            });
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Date).HasColumnType("date");
            entity.Property(a => a.ActiveCalories).HasPrecision(8, 2);
            entity.Property(a => a.Source).HasConversion<string>().HasMaxLength(32);
            entity.Property(a => a.UpdatedAt).HasColumnType("timestamp with time zone");

            // One summary per user, per day, per data source. Re-sending updates the same row.
            entity.HasIndex(a => new { a.UserId, a.Date, a.Source }).IsUnique();
        });
    }

    // ---------------------------------------------------------------------------
    // Achievement system
    // Catalog tables (Achievements, Rewards) use fixed int ids from AchievementCatalog / RewardCatalog,
    // so re-running migrations never creates duplicates. Per-user tables cascade when the user is deleted.
    // ---------------------------------------------------------------------------

    private static void ConfigureAchievement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.ToTable("Achievements", table =>
            {
                table.HasCheckConstraint("CK_Achievements_TargetValue", "\"TargetValue\" > 0");
                table.HasCheckConstraint("CK_Achievements_XpReward", "\"XpReward\" >= 0");
            });
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedNever();

            entity.Property(a => a.Code).IsRequired().HasMaxLength(64);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Description).IsRequired().HasMaxLength(300);
            entity.Property(a => a.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(a => a.Rarity).HasConversion<string>().HasMaxLength(32);
            entity.Property(a => a.MetricType).HasConversion<string>().HasMaxLength(64);
            entity.Property(a => a.IconKey).IsRequired().HasMaxLength(64);

            entity.HasIndex(a => a.Code).IsUnique();

            entity.HasData(AchievementCatalog.All);
        });
    }

    private static void ConfigureUserAchievement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.ToTable("UserAchievements");
            entity.HasKey(ua => ua.Id);

            entity.Property(ua => ua.UnlockedAt).HasColumnType("timestamp with time zone");
            entity.Property(ua => ua.SeenAt).HasColumnType("timestamp with time zone");

            // The same achievement can be earned only once per user, even with parallel requests.
            entity.HasIndex(ua => new { ua.UserId, ua.AchievementId }).IsUnique();
            entity.HasIndex(ua => new { ua.UserId, ua.UnlockedAt });

            entity.HasOne(ua => ua.User).WithMany().HasForeignKey(ua => ua.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ua => ua.Achievement).WithMany().HasForeignKey(ua => ua.AchievementId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUserProgress(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProgress>(entity =>
        {
            entity.ToTable("UserProgress", table =>
            {
                table.HasCheckConstraint("CK_UserProgress_TotalXp", "\"TotalXp\" >= 0");
                table.HasCheckConstraint("CK_UserProgress_Level", "\"Level\" >= 1 AND \"Level\" <= 100");
            });
            entity.HasKey(p => p.Id);

            entity.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.Property(p => p.LastActiveDate).HasColumnType("date");

            entity.HasIndex(p => p.UserId).IsUnique();
            entity.HasOne(p => p.User).WithOne().HasForeignKey<UserProgress>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUserPreferences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.ToTable("UserPreferences");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.ColorScheme).HasConversion<string>().HasMaxLength(16);
            entity.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(p => p.UserId).IsUnique();
            entity.HasOne(p => p.User).WithOne().HasForeignKey<UserPreferences>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

            // Selected cosmetics must be real rewards. Ownership and the reward type are checked in PreferenceService.
            entity.HasOne<Reward>().WithMany().HasForeignKey(p => p.ActiveBottleThemeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Reward>().WithMany().HasForeignKey(p => p.ActiveProfileFrameId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Reward>().WithMany().HasForeignKey(p => p.ActiveWaterAnimationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Reward>().WithMany().HasForeignKey(p => p.ActiveBadgeEffectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Reward>().WithMany().HasForeignKey(p => p.ActiveMascotAccessoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureReward(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reward>(entity =>
        {
            entity.ToTable("Rewards", table =>
            {
                table.HasCheckConstraint("CK_Rewards_Requirement", "\"RequiredLevel\" IS NOT NULL OR \"RequiredAchievementCode\" IS NOT NULL");
                table.HasCheckConstraint("CK_Rewards_RequiredLevel", "\"RequiredLevel\" IS NULL OR (\"RequiredLevel\" >= 1 AND \"RequiredLevel\" <= 100)");
            });
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedNever();

            entity.Property(r => r.Code).IsRequired().HasMaxLength(64);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Description).IsRequired().HasMaxLength(300);
            entity.Property(r => r.RewardType).HasConversion<string>().HasMaxLength(32);
            entity.Property(r => r.AssetKey).IsRequired().HasMaxLength(64);
            entity.Property(r => r.RequiredAchievementCode).HasMaxLength(64);

            entity.HasIndex(r => r.Code).IsUnique();

            entity.HasData(RewardCatalog.All);
        });
    }

    private static void ConfigureUserReward(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserReward>(entity =>
        {
            entity.ToTable("UserRewards");
            entity.HasKey(ur => ur.Id);

            entity.Property(ur => ur.UnlockedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(ur => new { ur.UserId, ur.RewardId }).IsUnique();

            entity.HasOne(ur => ur.User).WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ur => ur.Reward).WithMany().HasForeignKey(ur => ur.RewardId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUserAchievementEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAchievementEvent>(entity =>
        {
            entity.ToTable("UserAchievementEvents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType).IsRequired().HasMaxLength(40);
            entity.Property(e => e.EventValue).HasMaxLength(32);
            entity.Property(e => e.EventMetadata).HasMaxLength(32);
            entity.Property(e => e.DedupeKey).IsRequired().HasMaxLength(48);
            entity.Property(e => e.LocalDate).HasColumnType("date");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

            // Idempotency for retries of the same phone event.
            entity.HasIndex(e => new { e.UserId, e.ClientEventId }).IsUnique();

            // At most one row per day / week / theme / "once", depending on the event type.
            entity.HasIndex(e => new { e.UserId, e.EventType, e.DedupeKey }).IsUnique();

            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
