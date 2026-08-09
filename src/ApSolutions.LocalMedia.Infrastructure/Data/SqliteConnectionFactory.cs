using ApSolutions.LocalMedia.Application.Storage;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data;

public sealed class SqliteConnectionFactory : IDisposable
{
    private readonly SemaphoreSlim _databaseConfigurationLock = new(1, 1);
    private int _databaseConfigured;

    public SqliteConnectionFactory(IAppDataPaths appDataPaths)
        : this(appDataPaths?.DatabasePath ?? throw new ArgumentNullException(nameof(appDataPaths)))
    {
    }

    public SqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The database path must have a parent directory.");
        }

        Directory.CreateDirectory(directory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = false,
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureDatabaseAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA foreign_keys=ON;
                PRAGMA busy_timeout=5000;
                PRAGMA synchronous=FULL;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose()
    {
        _databaseConfigurationLock.Dispose();
    }

    private async Task ConfigureDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _databaseConfigured) == 1)
        {
            return;
        }

        await _databaseConfigurationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_databaseConfigured == 1)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            var mode = Convert.ToString(
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite rejected WAL journal mode: {mode}");
            }

            Volatile.Write(ref _databaseConfigured, 1);
        }
        finally
        {
            _databaseConfigurationLock.Release();
        }
    }
}
