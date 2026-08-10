// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Recovery;

/// <summary>
/// A migration fails halfway. The upgrade is abandoned, the database keeps the schema it had, and the
/// copy taken before the attempt is still there — in that order of importance.
/// </summary>
[Trait("Category", "Recovery")]
public sealed class FailedMigrationTests
{
    [Fact]
    public async Task A_failing_migration_abandons_the_upgrade_and_keeps_both_databases()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var before = await ReadSchemaAsync(factory);

        var runner = new MigrationRunner(
            factory,
            [new SqlMigration(
                int.MaxValue,
                "recovery_injected_failure",
                "CREATE TABLE never_arrives (id INTEGER PRIMARY KEY) STRICT; THIS IS NOT SQL;")]);
        var failure = await Assert.ThrowsAnyAsync<Exception>(
            () => runner.MigrateAsync(TestContext.Current.CancellationToken));

        Assert.Contains("recovery_injected_failure", failure.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await ReadSchemaAsync(factory));
        Assert.NotNull(runner.LastBackupPath);
        Assert.True(File.Exists(runner.LastBackupPath));

        await using (var backup = new SqliteConnection(
            $"Data Source={runner.LastBackupPath};Mode=ReadOnly;Pooling=False"))
        {
            await backup.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(backup, "PRAGMA integrity_check;"));
        }

        await using var active = await factory.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(active, "PRAGMA integrity_check;"));
        Assert.Equal(
            0L,
            await SqliteBootstrapTests.ScalarInt64Async(
                active,
                "SELECT COUNT(*) FROM sqlite_schema WHERE name = 'never_arrives';"));

        await RecoveryEvidence.RecordAsync(
            "failed-migration",
            "Migration fails",
            RecoveryOutcome.AbortedSafely,
            "The upgrade rolled back, the schema is unchanged, and the pre-migration copy is valid.",
            TestContext.Current.CancellationToken);
    }

    /// <summary>Retrying after the failure still finds a database it can open and migrate.</summary>
    [Fact]
    public async Task The_next_start_after_a_failed_migration_opens_normally()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var runner = new MigrationRunner(
            factory,
            [new SqlMigration(int.MaxValue, "recovery_injected_failure", "THIS IS NOT SQL;")]);
        await Assert.ThrowsAnyAsync<Exception>(() => runner.MigrateAsync(TestContext.Current.CancellationToken));

        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);

        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            16L,
            await SqliteBootstrapTests.ScalarInt64Async(connection, "SELECT COUNT(*) FROM schema_history;"));
        Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(connection, "PRAGMA integrity_check;"));
    }

    private static async Task<string> ReadSchemaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        return await SqliteBootstrapTests.ScalarTextAsync(
            connection,
            "SELECT group_concat(name, ',') FROM schema_history ORDER BY version;");
    }
}
