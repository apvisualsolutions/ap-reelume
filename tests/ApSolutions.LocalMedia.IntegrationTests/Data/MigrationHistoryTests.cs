using ApSolutions.LocalMedia.Infrastructure.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

/// <summary>
/// BUG-012: the history of applied migrations is re-verified, not merely counted. A build whose
/// migration text differs from what the file was actually migrated with must refuse the file — the
/// schema on disk and the schema this build assumes are not the same thing just because the
/// version numbers line up.
/// </summary>
public sealed class MigrationHistoryTests
{
    [Fact]
    public async Task An_applied_migration_whose_text_changed_is_refused()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        var applied = new SqlMigration(1, "baseline", "CREATE TABLE schema_history (version INTEGER PRIMARY KEY, name TEXT NOT NULL, applied_utc TEXT NOT NULL, checksum TEXT NOT NULL); CREATE TABLE first_table (id INTEGER PRIMARY KEY);");
        using (var runner = new MigrationRunner(factory, [applied]))
        {
            await runner.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var rewritten = new SqlMigration(1, "baseline", "CREATE TABLE schema_history (version INTEGER PRIMARY KEY, name TEXT NOT NULL, applied_utc TEXT NOT NULL, checksum TEXT NOT NULL); CREATE TABLE another_table (id INTEGER PRIMARY KEY);");
        using var suspicious = new MigrationRunner(factory, [rewritten]);

        var refusal = await Assert.ThrowsAsync<InvalidDataException>(() =>
            suspicious.MigrateAsync(TestContext.Current.CancellationToken));

        Assert.Contains("checksum", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_untouched_history_migrates_again_without_complaint_and_counts_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        var migration = new SqlMigration(1, "baseline", "CREATE TABLE schema_history (version INTEGER PRIMARY KEY, name TEXT NOT NULL, applied_utc TEXT NOT NULL, checksum TEXT NOT NULL); CREATE TABLE first_table (id INTEGER PRIMARY KEY);");
        using (var first = new MigrationRunner(factory, [migration]))
        {
            await first.MigrateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, first.AppliedMigrationCount);
        }

        using var second = new MigrationRunner(factory, [migration]);
        await second.MigrateAsync(TestContext.Current.CancellationToken);

        // Nothing new was applied, so the startup that follows has no reason to ask SQLite the
        // same integrity question twice.
        Assert.Equal(0, second.AppliedMigrationCount);
    }
}
