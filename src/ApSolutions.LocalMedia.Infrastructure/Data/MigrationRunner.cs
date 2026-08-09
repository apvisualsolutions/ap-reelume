using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Data;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data;

public sealed record SqlMigration(int Version, string Name, string Sql)
{
    public string Checksum { get; } = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Sql)));
}

public sealed class MigrationRunner : IMigrationRunner, IDisposable
{
    private const string ResourceRoot = "ApSolutions.LocalMedia.Infrastructure.Data.Migrations.";
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqlMigration[] _migrations;
    private readonly SemaphoreSlim _migrationLock = new(1, 1);

    public MigrationRunner(SqliteConnectionFactory connectionFactory)
        : this(connectionFactory, LoadDefaultMigrations())
    {
    }

    public MigrationRunner(
        SqliteConnectionFactory connectionFactory,
        IEnumerable<SqlMigration> migrations)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        ArgumentNullException.ThrowIfNull(migrations);
        _migrations = migrations.OrderBy(migration => migration.Version).ToArray();
        if (_migrations.Any(migration => migration.Version <= 0)
            || _migrations.Select(migration => migration.Version).Distinct().Count() != _migrations.Length)
        {
            throw new ArgumentException("Migration versions must be unique positive integers.", nameof(migrations));
        }
    }

    public string? LastBackupPath { get; private set; }

    /// <summary>
    /// How many migrations the last run actually applied. Zero means the file was not rewritten,
    /// which is what lets the startup skip asking SQLite the same integrity question twice
    /// (BUG-012): the pre-migration check this runner performs on every run already covered it.
    /// </summary>
    public int AppliedMigrationCount { get; private set; }

    public void Dispose()
    {
        _migrationLock.Dispose();
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await _migrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppliedMigrationCount = 0;
            await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureIntegrityAsync(connection, cancellationToken).ConfigureAwait(false);
            var applied = await GetAppliedMigrationsAsync(connection, cancellationToken).ConfigureAwait(false);
            EnsureNotAheadOfThisBuild([.. applied.Keys]);
            EnsureHistoryMatchesThisBuild(applied);

            foreach (var migration in _migrations.Where(item => !applied.ContainsKey(item.Version)))
            {
                LastBackupPath = await CreateBackupAsync(
                    connection,
                    migration.Version,
                    cancellationToken).ConfigureAwait(false);
                await ApplyMigrationAsync(connection, migration, cancellationToken).ConfigureAwait(false);
                AppliedMigrationCount++;
            }
        }
        finally
        {
            _migrationLock.Release();
        }
    }

    /// <summary>
    /// Refuses a database a later release has already migrated.
    /// </summary>
    /// <remarks>
    /// Looking only for migrations that have not been applied yet answers the wrong question: an
    /// older build finds none missing, concludes the schema is current, and starts writing rows into
    /// tables it does not know the shape of. The installer refuses a downgrade on its own, but the
    /// release also ships as an archive with no installer, so the binary has to be able to refuse
    /// alone. This runs before anything is written, and a refusal leaves the file untouched — not
    /// even the pre-migration backup is taken, because nothing is about to be migrated.
    /// </remarks>
    private void EnsureNotAheadOfThisBuild(HashSet<int> appliedVersions)
    {
        var newestKnown = _migrations.Length == 0 ? 0 : _migrations[^1].Version;
        var newestApplied = appliedVersions.Count == 0 ? 0 : appliedVersions.Max();
        if (newestApplied <= newestKnown)
        {
            return;
        }

        throw new InvalidDataException(
            $"The database was migrated to schema version {newestApplied} by a later release, and this "
            + $"build only knows version {newestKnown}. Opening it would write into a schema this build "
            + "cannot read. Install the newer release again, or restore a backup taken before it.");
    }

    private static SqlMigration[] LoadDefaultMigrations()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        using var manifestStream = assembly.GetManifestResourceStream($"{ResourceRoot}Manifest.json")
            ?? throw new InvalidDataException("The embedded migration manifest is missing.");
        var manifest = JsonSerializer.Deserialize<MigrationManifest>(
            manifestStream,
            ManifestSerializerOptions)
            ?? throw new InvalidDataException("The migration manifest is invalid.");
        if (manifest.FormatVersion != 1)
        {
            throw new InvalidDataException($"Unsupported migration manifest format {manifest.FormatVersion}.");
        }

        return manifest.Migrations
            .Select(entry => LoadMigration(assembly, entry))
            .ToArray();
    }

    private static SqlMigration LoadMigration(Assembly assembly, ManifestMigration entry)
    {
        using var sqlStream = assembly.GetManifestResourceStream(entry.Resource)
            ?? throw new InvalidDataException($"Migration resource is missing: {entry.Resource}");
        using var reader = new StreamReader(sqlStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var migration = new SqlMigration(entry.Version, entry.Name, reader.ReadToEnd());
        if (!migration.Checksum.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Migration checksum mismatch for version {entry.Version}.");
        }

        return migration;
    }

    /// <summary>
    /// A version number lining up is not the same schema (BUG-012): the history carries the
    /// checksum each migration was applied with, and a build whose migration text differs from
    /// what actually shaped the file has to refuse it before writing a single row.
    /// </summary>
    private void EnsureHistoryMatchesThisBuild(Dictionary<int, string> applied)
    {
        foreach (var migration in _migrations)
        {
            if (applied.TryGetValue(migration.Version, out var storedChecksum)
                && !migration.Checksum.Equals(storedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Migration {migration.Version} ({migration.Name}) was applied with checksum "
                    + $"{storedChecksum}, but this build carries {migration.Checksum}. The schema on "
                    + "disk and the schema this build assumes are not the same thing.");
            }
        }
    }

    private static async Task<Dictionary<int, string>> GetAppliedMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM sqlite_schema
                WHERE type = 'table' AND name = 'schema_history'
            );
            """;
        var exists = Convert.ToInt64(
            await existsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture) == 1;
        if (!exists)
        {
            return [];
        }

        await using var versionsCommand = connection.CreateCommand();
        versionsCommand.CommandText = "SELECT version, checksum FROM schema_history ORDER BY version;";
        await using var reader = await versionsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var applied = new Dictionary<int, string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            applied[reader.GetInt32(0)] = reader.GetString(1);
        }

        return applied;
    }

    private async Task<string> CreateBackupAsync(
        SqliteConnection connection,
        int migrationVersion,
        CancellationToken cancellationToken)
    {
        await using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var directory = Path.GetDirectoryName(_connectionFactory.DatabasePath)
            ?? throw new InvalidOperationException("The database path has no directory.");
        var fileName = $"{Path.GetFileName(_connectionFactory.DatabasePath)}.pre-migration-v{migrationVersion:D4}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.bak";
        var backupPath = Path.Combine(directory, fileName);
        var backupConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString();
        await using var backup = new SqliteConnection(backupConnectionString);
        await backup.OpenAsync(cancellationToken).ConfigureAwait(false);
        connection.BackupDatabase(backup);
        await using var integrity = backup.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(
            await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The pre-migration backup failed its integrity check.");
        }

        return backupPath;
    }

    private static async Task ApplyMigrationAsync(
        SqliteConnection connection,
        SqlMigration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var migrationCommand = connection.CreateCommand())
            {
                migrationCommand.Transaction = (SqliteTransaction)transaction;
                migrationCommand.CommandText = migration.Sql;
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var historyCommand = connection.CreateCommand())
            {
                historyCommand.Transaction = (SqliteTransaction)transaction;
                historyCommand.CommandText = """
                    INSERT INTO schema_history (version, name, applied_utc, checksum)
                    VALUES ($version, $name, $appliedUtc, $checksum);
                    """;
                historyCommand.Parameters.AddWithValue("$version", migration.Version);
                historyCommand.Parameters.AddWithValue("$name", migration.Name);
                historyCommand.Parameters.AddWithValue(
                    "$appliedUtc",
                    DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                historyCommand.Parameters.AddWithValue("$checksum", migration.Checksum);
                await historyCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Migration {migration.Version} ({migration.Name}) failed.",
                exception);
        }
    }

    private static async Task EnsureIntegrityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Database integrity check failed: {result}");
        }
    }

    private sealed record MigrationManifest(int FormatVersion, IReadOnlyList<ManifestMigration> Migrations);

    private sealed record ManifestMigration(int Version, string Name, string Resource, string Sha256);
}
