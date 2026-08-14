// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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

    /// <summary>
    /// The first migration in this repository that runs over data an earlier release wrote, rather
    /// than over an empty file.
    /// </summary>
    /// <remarks>
    /// Asking TMDB for the trailer key appends `videos` to the details request, but the cache is
    /// keyed by provider, title, language and provider version — never by the address. A payload
    /// stored before the upgrade would therefore be served as the answer to a question it was never
    /// asked, and a conditional request could keep renewing it, so a library that already had a
    /// cache would never grow a trailer button at all. Raising the provider version would hide those
    /// rows instead of removing them, and the only place the 180-day retention limit is enforced is
    /// the read of that same key — so rows nothing reads are rows nothing can ever delete. Emptying
    /// what belongs to TMDB is the one option that leaves neither a wrong answer nor a row that
    /// outlives its terms. Everything else in the table is left alone, which is what the second row
    /// is here to prove.
    /// </remarks>
    [Fact]
    public async Task Upgrading_empties_the_TMDB_cache_this_build_can_no_longer_read_correctly()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        var migrations = DatabaseTestHarness.EmbeddedMigrations;
        using (var before = new MigrationRunner(factory, migrations.Where(entry => entry.Version < 17)))
        {
            await before.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await StoreCachedPayloadAsync(factory, "tmdb");
        await StoreCachedPayloadAsync(factory, "other-provider");

        using (var upgrade = new MigrationRunner(factory, migrations))
        {
            await upgrade.MigrateAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0L, await CountCachedAsync(factory, "tmdb"));
        Assert.Equal(1L, await CountCachedAsync(factory, "other-provider"));
    }

    private static async Task StoreCachedPayloadAsync(SqliteConnectionFactory factory, string provider)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO metadata_cache (
                provider, cache_key, language, provider_version, payload, etag, stored_utc, expires_utc)
            VALUES ($provider, 'movie:329865', 'es-ES', 3, '{"id":329865}', '"v1"', '2026-08-01T10:00:00+00:00', '2027-08-01T10:00:00+00:00');
            """;
        _ = command.Parameters.AddWithValue("$provider", provider);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountCachedAsync(SqliteConnectionFactory factory, string provider)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM metadata_cache WHERE provider = $provider;";
        _ = command.Parameters.AddWithValue("$provider", provider);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
