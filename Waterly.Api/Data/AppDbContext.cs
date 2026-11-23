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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureWaterIntake(modelBuilder);
        ConfigureHydrationGoal(modelBuilder);
        ConfigureReminderSettings(modelBuilder);
        ConfigureDailyActivity(modelBuilder);
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
}
