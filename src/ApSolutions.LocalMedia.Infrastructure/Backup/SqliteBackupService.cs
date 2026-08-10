// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Backup;

/// <summary>
/// Takes a database file that can be opened on its own while the application keeps writing. It is the
/// same mechanism the migration runner already trusts — checkpoint, online backup, integrity check —
/// rather than a second way of copying a database, and the result only reaches its destination once it
/// has passed that check.
/// </summary>
public sealed class SqliteBackupService(SqliteConnectionFactory connectionFactory) : IBackupSnapshotWriter
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory
        ?? throw new ArgumentNullException(nameof(connectionFactory));

    public long EstimateBytes() =>
        File.Exists(_connectionFactory.DatabasePath)
            ? new FileInfo(_connectionFactory.DatabasePath).Length
            : 0;

    public async Task<BackupFileEntry> WriteAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath))
            ?? throw new InvalidOperationException("The snapshot destination has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var source = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                await using (var checkpoint = source.CreateCommand())
                {
                    checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
                    await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = temporaryPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Private,
                    Pooling = false,
                }.ToString();
                await using var destination = new SqliteConnection(connectionString);
                await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
                source.BackupDatabase(destination);

                await using var integrity = destination.CreateCommand();
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = Convert.ToString(
                    await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                    CultureInfo.InvariantCulture);
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The database snapshot failed its integrity check: {result}");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destinationPath, overwrite: true);
            await using var written = new FileStream(
                destinationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var hash = await SHA256.HashDataAsync(written, cancellationToken).ConfigureAwait(false);
            return new BackupFileEntry(
                BackupContentPolicy.DatabaseEntryName,
                Convert.ToHexString(hash).ToLowerInvariant(),
                written.Length);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
