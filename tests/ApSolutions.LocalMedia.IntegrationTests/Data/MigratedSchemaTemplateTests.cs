// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

/// <summary>
/// What the shared template owes the two hundred tests that stopped rebuilding the schema themselves.
/// A test that starts from a copy has to be indistinguishable from one that migrated: same tables,
/// same indexes, same triggers, same recorded history. If it were not, the saving would be bought by
/// letting tests pass over less than they used to, which is the failure this suite exists to refuse.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigratedSchemaTemplateTests
{
    [Fact]
    public async Task A_copied_database_carries_the_same_schema_as_one_that_migrated()
    {
        using var migrated = new DatabaseTestDirectory();
        using var copied = new DatabaseTestDirectory();

        var migratedFactory = new SqliteConnectionFactory(migrated.DatabasePath);
        await new MigrationRunner(migratedFactory).MigrateAsync(TestContext.Current.CancellationToken);
        var copiedFactory = await MigratedSchemaTemplate.CreateFactoryAsync(
            copied.DatabasePath,
            TestContext.Current.CancellationToken);

        var expected = await ReadSchemaAsync(migratedFactory);
        var actual = await ReadSchemaAsync(copiedFactory);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task A_copied_database_records_the_same_history_as_one_that_migrated()
    {
        using var migrated = new DatabaseTestDirectory();
        using var copied = new DatabaseTestDirectory();

        var migratedFactory = new SqliteConnectionFactory(migrated.DatabasePath);
        await new MigrationRunner(migratedFactory).MigrateAsync(TestContext.Current.CancellationToken);
        var copiedFactory = await MigratedSchemaTemplate.CreateFactoryAsync(
            copied.DatabasePath,
            TestContext.Current.CancellationToken);

        var expected = await ReadHistoryAsync(migratedFactory);
        var actual = await ReadHistoryAsync(copiedFactory);

        Assert.Equal(DatabaseTestHarness.MigrationCount, expected.Count);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The runner is the judge of its own work: handed the copy, it has to find nothing left to do.
    /// A copy that looked right but recorded a checksum the build no longer carries would be caught
    /// here rather than by whichever unrelated suite happened to open it first.
    /// </summary>
    [Fact]
    public async Task The_runner_finds_nothing_left_to_apply_on_a_copied_database()
    {
        using var directory = new DatabaseTestDirectory();

        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(
            directory.DatabasePath,
            TestContext.Current.CancellationToken);
        var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, runner.AppliedMigrationCount);
    }

    /// <summary>
    /// The whole point, stated as an assertion rather than as a measurement in a handover: the copy
    /// leaves one file where migrating left twenty-four. If a later change reintroduces the per-test
    /// backups, this fails instead of quietly returning the suite to twenty-seven minutes.
    /// </summary>
    [Fact]
    public async Task A_copied_database_leaves_one_file_where_migrating_leaves_twenty_four()
    {
        using var migrated = new DatabaseTestDirectory();
        using var copied = new DatabaseTestDirectory();

        var migratedFactory = new SqliteConnectionFactory(migrated.DatabasePath);
        await new MigrationRunner(migratedFactory).MigrateAsync(TestContext.Current.CancellationToken);
        await MigratedSchemaTemplate.CreateFactoryAsync(
            copied.DatabasePath,
            TestContext.Current.CancellationToken);
        SqliteConnection.ClearAllPools();

        Assert.Equal(DatabaseTestHarness.MigrationCount, Directory.GetFiles(migrated.Path, "*.bak").Length);
        Assert.Empty(Directory.GetFiles(copied.Path, "*.bak"));
        Assert.Single(Directory.GetFiles(copied.Path));
    }

    /// <summary>
    /// Two tests asking for a database must not be handed the same one. The template is shared; the
    /// databases made from it are not, and a leak here would be a flake in whichever pair of suites
    /// happened to run together.
    /// </summary>
    [Fact]
    public async Task Two_databases_made_from_the_template_do_not_share_their_contents()
    {
        using var first = new DatabaseTestDirectory();
        using var second = new DatabaseTestDirectory();

        var firstFactory = await MigratedSchemaTemplate.CreateFactoryAsync(
            first.DatabasePath,
            TestContext.Current.CancellationToken);
        var secondFactory = await MigratedSchemaTemplate.CreateFactoryAsync(
            second.DatabasePath,
            TestContext.Current.CancellationToken);

        await using (var connection = await firstFactory.OpenAsync(TestContext.Current.CancellationToken))
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
                VALUES ('a', 'c:\\only-in-the-first', 0, 0, 1);
                """;
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = await secondFactory.OpenAsync(TestContext.Current.CancellationToken);
        await using var count = reader.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM library_roots;";
        Assert.Equal(0L, Convert.ToInt64(
            await count.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task<List<string>> ReadSchemaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT type || '|' || name || '|' || COALESCE(sql, '')
            FROM sqlite_schema
            ORDER BY type, name;
            """;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    /// <summary>
    /// Version, name and checksum, and deliberately not the applied timestamp: the template was
    /// migrated once at the start of the run and a rebuilt database a minute later, so the one column
    /// that is allowed to differ is the one that records when.
    /// </summary>
    private static async Task<List<string>> ReadHistoryAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version || '|' || name || '|' || checksum FROM schema_history ORDER BY version;";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
