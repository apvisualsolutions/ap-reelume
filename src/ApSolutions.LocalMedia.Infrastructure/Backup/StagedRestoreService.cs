using System.Globalization;
using System.IO.Compression;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Backup;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Backup;

/// <summary>
/// Unpacks an archive somewhere disposable, answers questions about what is in it, rewrites its paths,
/// and — last of all — swaps it in while keeping the database it replaced.
/// <para>
/// The swap is the only destructive moment, and it is arranged so that no single failure loses data: the
/// live database is moved aside first, and if putting the staged one in its place fails, the one that
/// was moved aside goes straight back.
/// </para>
/// </summary>
public sealed class StagedRestoreService : IStagedRestoreService
{
    private readonly IAppDataPaths _paths;
    private readonly Func<string, long> _availableBytes;
    private readonly Action? _beforeSwap;

    public StagedRestoreService(IAppDataPaths paths)
        : this(paths, DefaultAvailableBytes, beforeSwap: null)
    {
    }

    public StagedRestoreService(IAppDataPaths paths, Func<string, long> availableBytes, Action? beforeSwap)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _availableBytes = availableBytes ?? throw new ArgumentNullException(nameof(availableBytes));
        _beforeSwap = beforeSwap;
    }

    private string StagingRoot => Path.Combine(_paths.BackupsDirectory, ".restore");

    public long GetAvailableBytes() => _availableBytes(_paths.DataRoot);

    public async Task<string> ExtractAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(StagingRoot);
        var staging = Path.Combine(StagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!BackupContentPolicy.IsAllowed(entry.FullName))
                {
                    throw new InvalidDataException($"The archive holds an entry a restore may not unpack: {entry.FullName}");
                }

                var destination = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (!destination.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The archive holds an entry outside its own folder: {entry.FullName}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var source = entry.Open();
                await using var target = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            return staging;
        }
        catch
        {
            Discard(staging);
            throw;
        }
    }

    public async Task<RestoreDatabaseFacts> InspectDatabaseAsync(
        string stagingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        var databasePath = Path.Combine(stagingDirectory, BackupContentPolicy.DatabaseEntryName);
        if (!File.Exists(databasePath))
        {
            return new RestoreDatabaseFacts(false, [], [], BackupContentPolicy.DatabaseEntryName);
        }

        try
        {
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Cache = SqliteCacheMode.Private,
                    Pooling = false,
                }.ToString());
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using (var integrity = connection.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = Convert.ToString(
                    await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                    CultureInfo.InvariantCulture);
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return new RestoreDatabaseFacts(false, [], [], result ?? "integrity_check failed");
                }
            }

            var roots = await ReadColumnAsync(
                connection,
                "SELECT normalized_path FROM library_roots ORDER BY normalized_path;",
                cancellationToken).ConfigureAwait(false);
            var media = await ReadColumnAsync(
                connection,
                "SELECT normalized_path FROM media_files ORDER BY normalized_path;",
                cancellationToken).ConfigureAwait(false);
            return new RestoreDatabaseFacts(true, roots, media, "ok");
        }
        catch (SqliteException exception)
        {
            return new RestoreDatabaseFacts(false, [], [], exception.Message);
        }
    }

    /// <summary>
    /// Rewrites the stored roots and every file path under them, on the staged copy and inside one
    /// transaction. The live database is not open at this point and is not involved.
    /// </summary>
    public async Task<int> ApplyRemapAsync(
        string stagingDirectory,
        IReadOnlyList<RootRemapDecision> decisions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentNullException.ThrowIfNull(decisions);
        var moved = decisions
            .Where(decision => decision.Status == RootRemapStatus.Remapped)
            .ToArray();
        if (moved.Length == 0)
        {
            return 0;
        }

        var databasePath = Path.Combine(stagingDirectory, BackupContentPolicy.DatabaseEntryName);
        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
            }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var changed = 0;

        foreach (var decision in moved)
        {
            await using (var updateRoot = connection.CreateCommand())
            {
                updateRoot.Transaction = (SqliteTransaction)transaction;
                updateRoot.CommandText = """
                    UPDATE library_roots
                    SET normalized_path = $new
                    WHERE normalized_path = $old;
                    """;
                updateRoot.Parameters.AddWithValue("$new", decision.NewPath);
                updateRoot.Parameters.AddWithValue("$old", decision.OldPath);
                await updateRoot.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var paths = await ReadColumnAsync(
                connection,
                "SELECT normalized_path FROM media_files;",
                cancellationToken,
                (SqliteTransaction)transaction).ConfigureAwait(false);
            foreach (var path in paths)
            {
                var rewritten = RootRemapPolicy.Rewrite(path, [decision]);
                if (rewritten.Equals(path, StringComparison.Ordinal))
                {
                    continue;
                }

                await using var updateFile = connection.CreateCommand();
                updateFile.Transaction = (SqliteTransaction)transaction;
                updateFile.CommandText = """
                    UPDATE media_files
                    SET normalized_path = $new
                    WHERE normalized_path = $old;
                    """;
                updateFile.Parameters.AddWithValue("$new", rewritten);
                updateFile.Parameters.AddWithValue("$old", path);
                changed += await updateFile.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return changed;
    }

    public async Task<string> SwapAsync(string stagingDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        _beforeSwap?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        SqliteConnection.ClearAllPools();

        var stagedDatabase = Path.Combine(stagingDirectory, BackupContentPolicy.DatabaseEntryName);
        var preserved = Path.Combine(
            _paths.DataRoot,
            $"{Path.GetFileName(_paths.DatabasePath)}.pre-restore-{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid():N}.bak");
        Directory.CreateDirectory(_paths.DataRoot);
        var hadDatabase = File.Exists(_paths.DatabasePath);
        if (hadDatabase)
        {
            File.Move(_paths.DatabasePath, preserved);
        }

        try
        {
            File.Move(stagedDatabase, _paths.DatabasePath);
        }
        catch
        {
            // The live database was moved aside a moment ago and nothing else has touched it. Putting it
            // back is the difference between a failed restore and a lost library.
            if (hadDatabase && !File.Exists(_paths.DatabasePath))
            {
                File.Move(preserved, _paths.DatabasePath);
            }

            throw;
        }

        // The write-ahead log and shared-memory file belong to the database that has just been replaced.
        // Leaving them behind would make SQLite read the old library's tail into the new one.
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            var path = _paths.DatabasePath + sidecar;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        await RestorePayloadAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
        Discard(stagingDirectory);
        return preserved;
    }

    public void Discard(string stagingDirectory)
    {
        if (string.IsNullOrWhiteSpace(stagingDirectory))
        {
            return;
        }

        var resolved = Path.GetFullPath(stagingDirectory);
        if (!resolved.StartsWith(Path.GetFullPath(StagingRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The staged restore is outside the restore folder.");
        }

        if (Directory.Exists(resolved))
        {
            Directory.Delete(resolved, recursive: true);
        }
    }

    private static long DefaultAvailableBytes(string directory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        return string.IsNullOrWhiteSpace(root) ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace;
    }

    private static async Task<IReadOnlyList<string>> ReadColumnAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    /// <summary>
    /// Puts back the two things that live outside the database: the preferences and the artwork a person
    /// chose. The downloaded cache is not restored because it was never carried; it regenerates.
    /// </summary>
    private async Task RestorePayloadAsync(string stagingDirectory, CancellationToken cancellationToken)
    {
        var preferences = Path.Combine(stagingDirectory, BackupContentPolicy.PreferencesEntryName);
        if (File.Exists(preferences))
        {
            File.Copy(preferences, _paths.SettingsPath, overwrite: true);
        }

        var artwork = Path.Combine(stagingDirectory, BackupContentPolicy.PersonalArtworkDirectoryName);
        if (!Directory.Exists(artwork))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(artwork, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Path.Combine(
                _paths.PersonalArtworkDirectory,
                Path.GetRelativePath(artwork, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
