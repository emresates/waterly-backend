using Microsoft.EntityFrameworkCore;
using Npgsql;
using Waterly.Api.Data;

namespace Waterly.Api.Tests.Infrastructure;

// Creates a fresh, real PostgreSQL database for the test run, applies the migrations, and drops it afterwards.
// Set WATERLY_TEST_CONNECTION to point at another server, e.g.
//   Host=localhost;Port=5432;Username=postgres;Password=postgres
public sealed class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        var baseConnection = Environment.GetEnvironmentVariable("WATERLY_TEST_CONNECTION")
                             ?? $"Host=localhost;Port=5432;Username={Environment.UserName}";

        var builder = new NpgsqlConnectionStringBuilder(baseConnection)
        {
            Database = $"waterly_test_{Guid.NewGuid():N}"[..30]
        };
        ConnectionString = builder.ConnectionString;
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }
}

// All database tests share one database and run one after another.
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
