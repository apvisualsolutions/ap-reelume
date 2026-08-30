// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Reflection;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.TestSupport;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

[Collection(ChildProcessSuites.Name)]
public sealed class SqliteBootstrapTests
{
    /// <summary>
    /// A child that inherits these overwrites the parent's coverage data, which is why the crash
    /// writer clears them. The rule is recorded in the development guide.
    /// </summary>
    private static readonly string[] ProfilerVariables =
    [
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "CORECLR_PROFILER_PATH",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "COR_PROFILER_PATH",
    ];

    [Fact]
    public async Task Bootstrap_uses_WAL_foreign_keys_busy_timeout_and_the_current_schema()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        var runner = DatabaseTestHarness.CreateDefaultRunner(factory);

        await DatabaseTestHarness.MigrateAsync(runner);
        await using var connection = await DatabaseTestHarness.OpenAsync(factory);

        Assert.Equal("wal", await ScalarTextAsync(connection, "PRAGMA journal_mode;"));
        Assert.Equal(1L, await ScalarInt64Async(connection, "PRAGMA foreign_keys;"));
        Assert.True(await ScalarInt64Async(connection, "PRAGMA busy_timeout;") >= 5000L);
        Assert.Equal("ok", await ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        Assert.Equal(22L, await ScalarInt64Async(connection, "SELECT COUNT(*) FROM schema_history;"));
        Assert.Equal(22L, await ScalarInt64Async(connection, "SELECT MAX(version) FROM schema_history;"));
        Assert.Equal(
            "initial,library_roots,media_files_scans,catalog_fts,file_identity,scanned_catalog_projection,match_candidates,metadata_cache,rename_log,playback_preferences,watch_state,intro_markers,personal_state,episode_media,catalog_metadata_versions,detected_markers,trailer_key,provider_reference,match_candidate_title,five_star_rating,scanned_title_year,courses",
            await ScalarTextAsync(connection, "SELECT group_concat(name, ',') FROM schema_history ORDER BY version;"));

        var tables = await ReadStringsAsync(
            connection,
            "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;");
        Assert.Equal(
            [
                "alternate_titles",
                "catalog_fts",
                "catalog_fts_config",
                "catalog_fts_content",
                "catalog_fts_data",
                "catalog_fts_docsize",
                "catalog_fts_idx",
                "catalog_metadata",
                "courses",
                "detected_markers",
                "episode_media",
                "episodes",
                "intro_markers",
                "lessons",
                "library_roots",
                "match_candidates",
                "media_file_identities",
                "media_files",
                "media_version_group_members",
                "media_version_groups",
                "metadata_cache",
                "personal_state",
                "playback_preferences",
                "rename_log",
                "scan_checkpoints",
                "scanned_catalog_fts",
                "scanned_catalog_fts_config",
                "scanned_catalog_fts_content",
                "scanned_catalog_fts_data",
                "scanned_catalog_fts_docsize",
                "scanned_catalog_fts_idx",
                "scanned_titles",
                "schema_history",
                "seasons",
                "title_cast",
                "title_genres",
                "titles",
                "watch_state",
            ],
            tables);
    }

    /// <summary>
    /// The YouTube key rides on the metadata row, and it is nullable on purpose: a title nobody has
    /// identified and a title TMDB has no trailer for both end up without one, and neither is a
    /// failure. What is stored is the key alone — never an address — so nothing that came off the
    /// network can decide where a browser goes.
    /// </summary>
    [Fact]
    public async Task The_metadata_row_carries_a_nullable_trailer_key()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        var runner = DatabaseTestHarness.CreateDefaultRunner(factory);

        await DatabaseTestHarness.MigrateAsync(runner);
        await using var connection = await DatabaseTestHarness.OpenAsync(factory);

        Assert.Equal(
            "trailer_key|TEXT|0|",
            await ScalarTextAsync(
                connection,
                """
                SELECT name || '|' || type || '|' || "notnull" || '|' || COALESCE(dflt_value, '')
                FROM pragma_table_info('catalog_metadata')
                WHERE name = 'trailer_key';
                """));
    }

    /// <summary>
    /// What a title was identified as, and when that answer was last taken.
    /// </summary>
    /// <remarks>
    /// Both nullable, because a title nobody identified has neither, and that is an absence rather
    /// than a defect. The provider is stored beside its key on purpose: <c>match_candidates</c> keeps
    /// the key alone, so the one place that recorded a reference could not say whose it was.
    /// </remarks>
    [Theory]
    [InlineData("provider", "TEXT")]
    [InlineData("provider_key", "TEXT")]
    [InlineData("refreshed_utc", "TEXT")]
    public async Task The_metadata_row_records_who_answered_and_when(string column, string type)
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        var runner = DatabaseTestHarness.CreateDefaultRunner(factory);

        await DatabaseTestHarness.MigrateAsync(runner);
        await using var connection = await DatabaseTestHarness.OpenAsync(factory);

        Assert.Equal(
            $"{column}|{type}|0|",
            await ScalarTextAsync(
                connection,
                $"""
                SELECT name || '|' || type || '|' || "notnull" || '|' || COALESCE(dflt_value, '')
                FROM pragma_table_info('catalog_metadata')
                WHERE name = '{column}';
                """));
    }

    [Fact]
    public async Task Migration_is_idempotent_and_creates_one_valid_copy_per_new_migration()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        var runner = DatabaseTestHarness.CreateDefaultRunner(factory);

        await DatabaseTestHarness.MigrateAsync(runner);
        var backupPath = Assert.IsType<string>(runner.GetType().GetProperty("LastBackupPath")?.GetValue(runner));
        Assert.True(File.Exists(backupPath));
        var backups = Directory.EnumerateFiles(directory.Path, "*.pre-migration-*.bak").Order().ToArray();
        Assert.Equal(22, backups.Length);

        await DatabaseTestHarness.MigrateAsync(runner);
        Assert.Equal(backupPath, runner.GetType().GetProperty("LastBackupPath")?.GetValue(runner));
        Assert.Equal(backups, Directory.EnumerateFiles(directory.Path, "*.pre-migration-*.bak").Order().ToArray());

        await using var active = await DatabaseTestHarness.OpenAsync(factory);
        Assert.Equal(22L, await ScalarInt64Async(active, "SELECT COUNT(*) FROM schema_history;"));
        Assert.Equal("ok", await ScalarTextAsync(active, "PRAGMA integrity_check;"));

        foreach (var path in backups)
        {
            await using var backup = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            await backup.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("ok", await ScalarTextAsync(backup, "PRAGMA integrity_check;"));
        }
    }

    [Fact]
    public async Task Integrity_checker_reports_corruption_without_modifying_the_file()
    {
        using var directory = new DatabaseTestDirectory();
        await File.WriteAllBytesAsync(
            directory.DatabasePath,
            [0x41, 0x50, 0x53, 0x4F, 0x4C],
            TestContext.Current.CancellationToken);
        var original = await File.ReadAllBytesAsync(
            directory.DatabasePath,
            TestContext.Current.CancellationToken);
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        var checker = DatabaseTestHarness.CreateIntegrityChecker(factory);

        var result = await DatabaseTestHarness.CheckIntegrityAsync(checker);

        Assert.Equal(false, result.GetType().GetProperty("IsValid")?.GetValue(result));
        Assert.False(string.IsNullOrWhiteSpace(result.GetType().GetProperty("Detail")?.GetValue(result)?.ToString()));
        Assert.Equal(
            original,
            await File.ReadAllBytesAsync(directory.DatabasePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Committed_WAL_transaction_survives_forced_child_process_termination()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));
        var signalPath = System.IO.Path.Combine(directory.Path, "committed.signal");
        var child = StartCrashWriter(directory.DatabasePath, signalPath);

        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(signalPath) && DateTime.UtcNow < deadline && !child.HasExited)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }

            if (!File.Exists(signalPath))
            {
                var output = await child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                var error = await child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                Assert.Fail($"Crash writer did not commit. stdout={output}; stderr={error}");
            }

            var fixtureProcessId = int.Parse(
                File.ReadAllText(signalPath),
                System.Globalization.CultureInfo.InvariantCulture);
            using (var fixtureProcess = Process.GetProcessById(fixtureProcessId))
            {
                fixtureProcess.Kill(entireProcessTree: true);
                await fixtureProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            child.Dispose();
        }

        // The claim under test is the durability of the committed transaction, not how fast a
        // shared runner's disk settles after a hard kill: that reopen answered a transient
        // "disk I/O error" twice on runners and never locally (CI-005). Three attempts, one second
        // apart, only around this reopen — an assertion failure is not retried, and a disk that
        // still errors on the third attempt fails the test with its own exception.
        SqliteException? transient = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var connection = new SqliteConnection(
                    $"Data Source={directory.DatabasePath};Pooling=False");
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                Assert.Equal("ok", await ScalarTextAsync(connection, "PRAGMA integrity_check;"));
                Assert.Equal(
                    "committed",
                    await ScalarTextAsync(connection, "SELECT value FROM crash_probe WHERE id = 1;"));
                transient = null;
                break;
            }
            catch (SqliteException exception)
            {
                transient = exception;
                await Task.Delay(1000, TestContext.Current.CancellationToken);
            }
        }

        if (transient is not null)
        {
            throw transient;
        }
    }

    [Fact]
    public async Task Crash_writer_process_fixture()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("AP_LOCALMEDIA_CRASH_CHILD"),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        var databasePath = Environment.GetEnvironmentVariable("AP_LOCALMEDIA_CRASH_DB");
        var signalPath = Environment.GetEnvironmentVariable("AP_LOCALMEDIA_CRASH_SIGNAL");
        Assert.False(string.IsNullOrWhiteSpace(databasePath));
        Assert.False(string.IsNullOrWhiteSpace(signalPath));

        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS crash_probe (
                    id INTEGER NOT NULL PRIMARY KEY,
                    value TEXT NOT NULL
                ) STRICT;
                INSERT OR REPLACE INTO crash_probe (id, value) VALUES (1, 'committed');
                """;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        File.WriteAllText(
            signalPath,
            Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Task.Delay(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task App_data_database_verification_fixture()
    {
        var databasePath = Environment.GetEnvironmentVariable("AP_LOCALMEDIA_VERIFY_DB");
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            return;
        }

        var factory = DatabaseTestHarness.CreateFactory(databasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ok", await ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        Assert.Equal(2L, await ScalarInt64Async(connection, "SELECT COUNT(*) FROM schema_history;"));
        Assert.Equal(2L, await ScalarInt64Async(connection, "SELECT MAX(version) FROM schema_history;"));
    }

    private static Process StartCrashWriter(string databasePath, string signalPath)
    {
        var projectPath = System.IO.Path.Combine(
            RepositoryLayout.Root,
            "tests",
            "ApSolutions.LocalMedia.IntegrationTests",
            "ApSolutions.LocalMedia.IntegrationTests.csproj");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add("FullyQualifiedName~Crash_writer_process_fixture");
        startInfo.Environment["AP_LOCALMEDIA_CRASH_CHILD"] = "1";
        startInfo.Environment["AP_LOCALMEDIA_CRASH_DB"] = databasePath;
        startInfo.Environment["AP_LOCALMEDIA_CRASH_SIGNAL"] = signalPath;
        foreach (var profiling in ProfilerVariables)
        {
            startInfo.Environment[profiling] = string.Empty;
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start crash writer process.");
    }

    internal static async Task<long> ScalarInt64Async(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static async Task<string> ScalarTextAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)
            ?? string.Empty;
    }

    private static async Task<string[]> ReadStringsAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }
}

internal static class DatabaseTestHarness
{
    private const string InfrastructureAssemblyName = "ApSolutions.LocalMedia.Infrastructure";

    /// <summary>
    /// Every migration this build carries, read from the embedded manifest.
    /// </summary>
    /// <remarks>
    /// Three suites that have nothing to do with schemas — backup, downgrade, recovery — assert that
    /// every migration was applied, and each held its own literal count. Adding migration 17 turned
    /// four tests red across three files that the change had not touched, which is the same number
    /// written down in five places. Those three read it from here now.
    /// <see cref="SqliteBootstrapTests"/> keeps its literals on purpose: pinning the exact schema,
    /// by count and by name, is what that suite is for.
    /// </remarks>
    public static SqlMigration[] EmbeddedMigrations { get; } = LoadEmbeddedMigrations();

    /// <summary>How many migrations a fully migrated database should record.</summary>
    public static long MigrationCount => EmbeddedMigrations.Length;

    public static object CreateFactory(string databasePath)
    {
        var factoryType = RequireType("ApSolutions.LocalMedia.Infrastructure.Data.SqliteConnectionFactory");
        var factory = Activator.CreateInstance(factoryType, databasePath);
        Assert.NotNull(factory);
        return factory;
    }

    public static object CreateDefaultRunner(object factory)
    {
        var runnerType = RequireType("ApSolutions.LocalMedia.Infrastructure.Data.MigrationRunner");
        var runner = Activator.CreateInstance(runnerType, factory);
        Assert.NotNull(runner);
        return runner;
    }

    public static object CreateIntegrityChecker(object factory)
    {
        var checkerType = RequireType("ApSolutions.LocalMedia.Infrastructure.Data.IntegrityChecker");
        var checker = Activator.CreateInstance(checkerType, factory);
        Assert.NotNull(checker);
        return checker;
    }

    public static async Task MigrateAsync(object runner)
    {
        var method = runner.GetType().GetMethod("MigrateAsync", [typeof(CancellationToken)]);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(runner, [CancellationToken.None]));
        await task;
    }

    public static async Task<SqliteConnection> OpenAsync(object factory)
    {
        var method = factory.GetType().GetMethod("OpenAsync", [typeof(CancellationToken)]);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(factory, [CancellationToken.None]));
        await task;
        return Assert.IsType<SqliteConnection>(task.GetType().GetProperty("Result")?.GetValue(task));
    }

    public static async Task<object> CheckIntegrityAsync(object checker)
    {
        var method = checker.GetType().GetMethod("CheckAsync", [typeof(CancellationToken)]);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(checker, [CancellationToken.None]));
        await task;
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.NotNull(result);
        return result;
    }

    public static Type RequireType(string fullName)
    {
        var type = Assembly.Load(InfrastructureAssemblyName).GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type;
    }

    private static SqlMigration[] LoadEmbeddedMigrations()
    {
        var assembly = typeof(SqlMigration).Assembly;
        using var manifestStream = assembly.GetManifestResourceStream(
            "ApSolutions.LocalMedia.Infrastructure.Data.Migrations.Manifest.json");
        Assert.NotNull(manifestStream);
        using var manifest = System.Text.Json.JsonDocument.Parse(manifestStream);
        return [.. manifest.RootElement.GetProperty("migrations").EnumerateArray().Select(entry =>
        {
            using var sqlStream = assembly.GetManifestResourceStream(entry.GetProperty("resource").GetString()!);
            Assert.NotNull(sqlStream);
            using var reader = new StreamReader(sqlStream);
            return new SqlMigration(
                entry.GetProperty("version").GetInt32(),
                entry.GetProperty("name").GetString()!,
                reader.ReadToEnd());
        })];
    }
}

internal sealed class DatabaseTestDirectory : IDisposable
{
    private static readonly string TestRoot = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests");

    public DatabaseTestDirectory()
    {
        Path = System.IO.Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string DatabasePath => System.IO.Path.Combine(Path, "library.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var resolved = System.IO.Path.GetFullPath(Path);
        var root = System.IO.Path.GetFullPath(TestRoot) + System.IO.Path.DirectorySeparatorChar;
        if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    Directory.Delete(resolved, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < 49)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
