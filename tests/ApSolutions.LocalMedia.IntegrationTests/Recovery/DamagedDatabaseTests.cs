using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Recovery;

/// <summary>
/// The database is damaged. The only acceptable behaviour is to refuse and change nothing: a repair
/// that overwrites the last good copy turns one bad day into a permanent loss.
/// </summary>
[Trait("Category", "Recovery")]
public sealed class DamagedDatabaseTests
{
    [Fact]
    public async Task A_damaged_database_is_reported_and_left_exactly_as_it_was()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var backups = Directory.GetFiles(directory.Path, "*.pre-migration-*.bak");
        SqliteConnection.ClearAllPools();

        await Damage(directory.DatabasePath, TestContext.Current.CancellationToken);
        var damaged = await File.ReadAllBytesAsync(
            directory.DatabasePath,
            TestContext.Current.CancellationToken);

        var result = await new IntegrityChecker(new SqliteConnectionFactory(directory.DatabasePath))
            .CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal(
            damaged,
            await File.ReadAllBytesAsync(directory.DatabasePath, TestContext.Current.CancellationToken));
        Assert.Equal(backups, Directory.GetFiles(directory.Path, "*.pre-migration-*.bak"));
        foreach (var backup in backups)
        {
            await using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        }

        await RecoveryEvidence.RecordAsync(
            "damaged-database",
            "Damaged database",
            RecoveryOutcome.AbortedSafely,
            "Corruption reported; the damaged file and all fourteen pre-migration copies are untouched.",
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The harness has to be shown to notice. A check that always says "ok" would pass this suite
    /// forever without ever having looked, so the seeded corruption is proved detectable first.
    /// </summary>
    [Fact]
    public async Task The_check_says_ok_before_the_damage_and_not_after()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);

        var before = await new IntegrityChecker(factory).CheckAsync(TestContext.Current.CancellationToken);
        SqliteConnection.ClearAllPools();
        await Damage(directory.DatabasePath, TestContext.Current.CancellationToken);
        var after = await new IntegrityChecker(new SqliteConnectionFactory(directory.DatabasePath))
            .CheckAsync(TestContext.Current.CancellationToken);

        Assert.True(before.IsValid, "The harness reported a healthy database as damaged.");
        Assert.False(after.IsValid, "The harness did not notice a database it was told to damage.");
    }

    /// <summary>
    /// Overwrites the pages that hold the schema, leaving the SQLite header intact so the file still
    /// looks like a database. A file of random bytes would be refused by the format check alone.
    /// </summary>
    private static async Task Damage(string databasePath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(databasePath, cancellationToken).ConfigureAwait(false);
        for (var offset = 4096; offset < Math.Min(bytes.Length, 65536); offset++)
        {
            bytes[offset] = 0x5A;
        }

        await File.WriteAllBytesAsync(databasePath, bytes, cancellationToken).ConfigureAwait(false);
    }
}
