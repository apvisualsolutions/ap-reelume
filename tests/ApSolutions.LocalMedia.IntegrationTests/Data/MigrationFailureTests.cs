using System.Reflection;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

public sealed class MigrationFailureTests
{
    [Fact]
    public async Task Injected_failure_rolls_back_schema_history_and_preserves_active_and_backup_databases()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));
        var expectedMigrationCount = await CreateSentinelAsync(factory);
        var failingRunner = CreateFailingRunner(factory);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => DatabaseTestHarness.MigrateAsync(failingRunner));
        Assert.Contains("injected_failure", failure.ToString(), StringComparison.OrdinalIgnoreCase);

        var backupPath = Assert.IsType<string>(
            failingRunner.GetType().GetProperty("LastBackupPath")?.GetValue(failingRunner));
        Assert.True(File.Exists(backupPath));
        Assert.NotEqual(directory.DatabasePath, backupPath);

        await using (var active = await DatabaseTestHarness.OpenAsync(factory))
        {
            await AssertPreservedAsync(active, expectedMigrationCount);
        }

        await using var backup = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly;Pooling=False");
        await backup.OpenAsync(TestContext.Current.CancellationToken);
        await AssertPreservedAsync(backup, expectedMigrationCount);
    }

    private static object CreateFailingRunner(object factory)
    {
        var factoryType = factory.GetType();
        var migrationType = DatabaseTestHarness.RequireType(
            "ApSolutions.LocalMedia.Infrastructure.Data.SqlMigration");
        var migration = Activator.CreateInstance(
            migrationType,
            int.MaxValue,
            "injected_failure",
            "CREATE TABLE rolled_back_probe (id INTEGER PRIMARY KEY) STRICT; THIS_IS_NOT_SQL;");
        Assert.NotNull(migration);
        var migrations = Array.CreateInstance(migrationType, 1);
        migrations.SetValue(migration, 0);

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(migrationType);
        var runnerType = DatabaseTestHarness.RequireType(
            "ApSolutions.LocalMedia.Infrastructure.Data.MigrationRunner");
        var constructor = runnerType.GetConstructor([factoryType, enumerableType]);
        Assert.NotNull(constructor);
        return constructor.Invoke([factory, migrations]);
    }

    private static async Task<long> CreateSentinelAsync(object factory)
    {
        await using var connection = await DatabaseTestHarness.OpenAsync(factory);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE sentinel (
                id INTEGER NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            ) STRICT;
            INSERT INTO sentinel (id, value) VALUES (1, 'preserved');
            """;
        await command.ExecuteNonQueryAsync();
        return await SqliteBootstrapTests.ScalarInt64Async(
            connection,
            "SELECT COUNT(*) FROM schema_history;");
    }

    private static async Task AssertPreservedAsync(SqliteConnection connection, long expectedMigrationCount)
    {
        Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        Assert.Equal(
            "preserved",
            await SqliteBootstrapTests.ScalarTextAsync(connection, "SELECT value FROM sentinel WHERE id = 1;"));
        Assert.Equal(
            expectedMigrationCount,
            await SqliteBootstrapTests.ScalarInt64Async(connection, "SELECT COUNT(*) FROM schema_history;"));
        Assert.Equal(
            0L,
            await SqliteBootstrapTests.ScalarInt64Async(
                connection,
                "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = 'rolled_back_probe';"));
    }
}
