using System.Globalization;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

/// <summary>
/// An older build meeting a database a newer one has already migrated.
/// </summary>
/// <remarks>
/// The MSIX installer refuses a downgrade on its own, but the release also ships as a ZIP with no
/// installer at all: anyone can extract an older build over a folder a newer one has been writing
/// to. In that case nothing but the binary can refuse, and a runner that only looks for migrations
/// it has not applied yet would find none missing, conclude everything was in order, and start
/// writing rows into a schema it does not understand.
/// </remarks>
public sealed class SchemaDowngradeTests
{
    [Fact]
    public async Task A_database_migrated_by_a_newer_build_is_refused_rather_than_opened()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));
        var future = await RecordFutureMigrationAsync(factory);

        var refusal = await Assert.ThrowsAsync<InvalidDataException>(
            () => DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory)));

        Assert.Contains(future.ToString(CultureInfo.InvariantCulture), refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Refusing has to mean touching nothing. A refusal that has already written is a downgrade with
    /// an error message on top of it.
    /// </summary>
    [Fact]
    public async Task The_refusal_leaves_the_database_exactly_as_it_found_it()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));
        _ = await RecordFutureMigrationAsync(factory);
        var before = await ReadHistoryAsync(factory);
        // The backups the first migration legitimately took. A refusal must not add to them: nothing
        // is about to be migrated, so there is nothing to take a copy of.
        var backupsBefore = Directory.GetFiles(directory.Path, "*.bak").Length;

        _ = await Assert.ThrowsAsync<InvalidDataException>(
            () => DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory)));

        Assert.Equal(before, await ReadHistoryAsync(factory));
        Assert.Equal(backupsBefore, Directory.GetFiles(directory.Path, "*.bak").Length);
    }

    /// <summary>
    /// The check has to be about being behind, not about being different. A build whose migrations
    /// match the database exactly, or exceed it, has nothing to refuse.
    /// </summary>
    [Fact]
    public async Task A_build_that_is_current_or_ahead_still_opens_the_database()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));

        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));

        await using var connection = await DatabaseTestHarness.OpenAsync(factory);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_history;";
        Assert.Equal(
            16L,
            Convert.ToInt64(
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                CultureInfo.InvariantCulture));
    }

    /// <summary>Writes the row a future release would have left behind, and returns its version.</summary>
    private static async Task<int> RecordFutureMigrationAsync(object factory)
    {
        await using var connection = await DatabaseTestHarness.OpenAsync(factory);
        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT MAX(version) FROM schema_history;";
        var newest = Convert.ToInt32(
            await read.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            CultureInfo.InvariantCulture);
        var future = newest + 1;

        await using var write = connection.CreateCommand();
        write.CommandText = """
            INSERT INTO schema_history (version, name, applied_utc, checksum)
            VALUES ($version, 'from_a_later_release', $appliedUtc, 'not-a-checksum-this-build-knows');
            """;
        write.Parameters.AddWithValue("$version", future);
        write.Parameters.AddWithValue("$appliedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        _ = await write.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return future;
    }

    private static async Task<string> ReadHistoryAsync(object factory)
    {
        await using var connection = await DatabaseTestHarness.OpenAsync(factory);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version, name, checksum FROM schema_history ORDER BY version;";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add($"{reader.GetInt32(0)}|{reader.GetString(1)}|{reader.GetString(2)}");
        }

        return string.Join(';', rows);
    }
}
