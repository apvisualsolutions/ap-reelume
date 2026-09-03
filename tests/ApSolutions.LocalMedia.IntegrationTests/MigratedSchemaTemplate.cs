// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.IntegrationTests;

/// <summary>
/// One migrated database per test process, copied for every test that only needs the schema to exist.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 2026-09-03. Around two hundred tests in this suite each rebuilt the schema from the
/// twenty-two migrations, and the runner writes a full backup of the file before each one: twenty-four
/// files and 5.5 MB created and deleted per test, roughly 1.3 GB and 5,300 files per run, all in one
/// temporary directory. Locally that start-up costs 118 ms; on a hosted runner the same tests stopped
/// tracking their own work and paid a flat toll instead — regression against the local times gave
/// 21.7 seconds fixed plus 0.3x the local duration, with r=0.10. A cost that does not depend on the
/// work is contention, and the shared resource was the directory those files churn through.
/// </para>
/// <para>
/// Copying a prepared file costs 3 ms and leaves one file instead of twenty-four. The template itself
/// is migrated exactly once, by the real runner, so the schema every test sees is still the one the
/// migrations produce rather than a second definition that could drift from them.
/// </para>
/// <para>
/// <b>Who uses this is decided by what the test does, never by the folder it sits in.</b> Reading
/// the forty-four files that build a database one by one moved six of them across the line a folder
/// would have drawn, in both directions:
/// </para>
/// <list type="bullet">
/// <item>
/// Seven keep calling <see cref="MigrationRunner"/> because rebuilding step by step is what they
/// measure — the history, the downgrade, the yield, the bootstrap and the injected failure. The one
/// that settles it is <c>Recovery/DamagedDatabaseTests</c>: it counts the <c>.bak</c> files the
/// migration leaves behind, so a template that leaves none would have made it assert over nothing.
/// </item>
/// <item>
/// Three under <c>Data/</c> use the template despite their neighbours — they only need somewhere to
/// read courses and duplicates from — and three under <c>Recovery/</c> do too, because a forced
/// shutdown, a failing media engine and a removed drive have nothing to do with schemas.
/// </item>
/// <item>
/// Four calls stay behind inside converted files: the assembly-isolation checks that build the
/// runner through reflection. There the construction <i>is</i> the assertion, and replacing it would
/// have removed a check while appearing to save time.
/// </item>
/// </list>
/// <para>
/// Classifying by location is this repository's best-documented trap, and it bit twice elsewhere on
/// the day this was written.
/// </para>
/// </remarks>
internal static class MigratedSchemaTemplate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? _templatePath;

    /// <summary>
    /// Puts a fully migrated database at <paramref name="databasePath"/> and hands back a factory for
    /// it. Equivalent to constructing the factory and running every migration, minus the backups.
    /// </summary>
    public static async Task<SqliteConnectionFactory> CreateFactoryAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        await CopyToAsync(databasePath, cancellationToken).ConfigureAwait(false);
        return new SqliteConnectionFactory(databasePath);
    }

    /// <summary>
    /// Puts a fully migrated database at <paramref name="databasePath"/>, for the callers that build
    /// their own factory or hand the path to something else.
    /// </summary>
    public static async Task CopyToAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var template = await EnsureTemplateAsync(cancellationToken).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(template, databasePath, overwrite: true);
    }

    private static async Task<string> EnsureTemplateAsync(CancellationToken cancellationToken)
    {
        if (_templatePath is { } ready)
        {
            return ready;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_templatePath is { } alreadyBuilt)
            {
                return alreadyBuilt;
            }

            _templatePath = await BuildTemplateAsync(cancellationToken).ConfigureAwait(false);
            return _templatePath;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Migrates once with the real runner, then folds the write-ahead log back into the file and drops
    /// the pre-migration backups, so what gets copied is a single self-contained database. Without the
    /// checkpoint the copy would be a file whose most recent pages live in a log left behind.
    /// </summary>
    private static async Task<string> BuildTemplateAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "APSolutions.LocalMedia.Tests",
            "schema-template-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "template.db");

        var factory = new SqliteConnectionFactory(databasePath);
        await new MigrationRunner(factory).MigrateAsync(cancellationToken).ConfigureAwait(false);

        await using (var connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            await using var checkpoint = connection.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        SqliteConnection.ClearAllPools();
        foreach (var backup in Directory.GetFiles(directory, "*.bak"))
        {
            File.Delete(backup);
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Discard(directory);
        return databasePath;
    }

    /// <summary>
    /// Best effort: the template lives in the same temporary root the per-test directories use, and a
    /// process that dies without running this leaves 392 KB behind rather than a broken run.
    /// </summary>
    private static void Discard(string directory)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
