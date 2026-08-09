using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Windows.Shell;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Windows;

/// <summary>
/// Opening a loose file must leave the database exactly as it found it. The proof is a census of
/// every persistent table taken before and after, not a spot check of the tables that seemed likely.
/// </summary>
public sealed class FileActivationTests : IAsyncLifetime
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("reelume-activation");
    private string _databasePath = string.Empty;
    private SqliteConnectionFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _databasePath = Path.Combine(_directory.FullName, "library.db");
        _factory = new SqliteConnectionFactory(_databasePath);
        await new MigrationRunner(_factory).MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            _directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // A pooled handle can outlive the test on Windows; the temp directory is disposable.
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Every_persistent_table_holds_the_same_rows_before_and_after_an_activation()
    {
        var before = await CensusAsync();
        var path = CreateFile("loose.mkv");
        var coordinator = new CountingCoordinator();

        var session = await new OpenLooseFile(coordinator)
            .ExecuteAsync(path, TestContext.Current.CancellationToken);

        var after = await CensusAsync();
        Assert.Equal(before, after);

        // The census has to cover the whole schema, not the tables that seemed likely to change.
        Assert.True(before.Count >= 20, $"The census only saw {before.Count} tables.");
        Assert.Contains("titles", before.Keys);
        Assert.Contains("media_files", before.Keys);
        Assert.Contains("watch_state", before.Keys);
        Assert.Contains("personal_state", before.Keys);
        Assert.Equal(0, before["titles"] + before["media_files"] + before["watch_state"]);
        Assert.Equal(path, session.Path);
    }

    [Fact]
    public async Task A_second_activation_and_a_failed_one_also_leave_the_database_alone()
    {
        var before = await CensusAsync();
        var path = CreateFile("loose.mp4");
        var coordinator = new CountingCoordinator();
        var open = new OpenLooseFile(coordinator);

        await open.ExecuteAsync(path, TestContext.Current.CancellationToken);
        await open.ExecuteAsync(path, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            open.ExecuteAsync(Path.Combine(_directory.FullName, "missing.mkv"), TestContext.Current.CancellationToken));

        Assert.Equal(before, await CensusAsync());
    }

    [Fact]
    public async Task A_path_with_spaces_unicode_and_extreme_length_still_opens_and_still_writes_nothing()
    {
        var before = await CensusAsync();
        var deep = _directory.FullName;
        while (deep.Length < 200)
        {
            deep = Path.Combine(deep, "carpeta con nombre largo y acentuación ñ");
        }

        Directory.CreateDirectory(deep);
        var path = Path.Combine(deep, "película 日本語 con espacios.mkv");
        await File.WriteAllBytesAsync(path, [0, 1, 2, 3], TestContext.Current.CancellationToken);
        Assert.True(path.Length > 240, $"The path was only {path.Length} characters long.");

        var session = await new OpenLooseFile(new CountingCoordinator())
            .ExecuteAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal("película 日本語 con espacios.mkv", session.DisplayName);
        Assert.Equal(before, await CensusAsync());
    }

    [Fact]
    public void The_argument_parser_takes_the_first_positional_path_and_nothing_else()
    {
        Assert.Equal(@"C:\media\a.mkv", FileActivationHandler.Parse([@"C:\media\a.mkv"]));
        Assert.Equal(@"C:\media\a.mkv", FileActivationHandler.Parse([@"C:\media\a.mkv", @"C:\media\b.mkv"]));
        Assert.Equal("a b.mkv", FileActivationHandler.Parse(["a b.mkv"]));
    }

    [Fact]
    public void The_argument_parser_refuses_switches_and_empty_input()
    {
        Assert.Null(FileActivationHandler.Parse([]));
        Assert.Null(FileActivationHandler.Parse(["   "]));
        Assert.Null(FileActivationHandler.Parse(["--open", @"C:\media\a.mkv"]));
        Assert.Null(FileActivationHandler.Parse(["-o"]));
        Assert.Null(FileActivationHandler.Parse(["/silent"]));
        Assert.Throws<ArgumentNullException>(() => FileActivationHandler.Parse(null!));
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_directory.FullName, name);
        File.WriteAllBytes(path, [0, 1, 2, 3]);
        return path;
    }

    /// <summary>Counts the rows of every table the schema declares, so nothing can hide.</summary>
    private async Task<Dictionary<string, long>> CensusAsync()
    {
        await using var connection = await _factory.OpenAsync(TestContext.Current.CancellationToken);
        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        var census = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables.Where(name => !name.Equals("schema_history", StringComparison.Ordinal)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
            census[table] = Convert.ToInt64(
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return census;
    }

    private sealed class CountingCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession { get; private set; }

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            ActiveSession = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            return Task.FromResult(ActiveSession);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            ActiveSession = null;
            return Task.CompletedTask;
        }
    }
}
