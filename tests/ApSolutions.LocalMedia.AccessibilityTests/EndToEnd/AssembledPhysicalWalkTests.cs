using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Windows;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The physical walk of the assembled artifact, as far as a headless harness can carry it.
/// <para>
/// Every scene here plays against the application the composition root builds — the same assembly
/// the package seals — with real files on a real disk, a real SQLite catalogue, and the real LibVLC
/// engine decoding real frames. Nothing is stubbed and nothing is built by the test except the
/// media files themselves, which come from FFmpeg's synthetic generators. What headless cannot
/// prove — a picture on a physical screen, TMDB answering over the network — is written down as
/// the ten-minute script in docs/evidence/stable/audit-walkthrough.md.
/// </para>
/// </summary>
[Collection(AssembledShellSuites.Name)]
public sealed class AssembledPhysicalWalkTests : IDisposable
{
    private readonly string _dataRoot = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"walk-{Guid.NewGuid():N}");

    public void Dispose()
    {
        // The watcher lets go of its directory handle asynchronously on the close path; the delete
        // retries rather than racing it.
        for (var attempt = 0; attempt < 5 && Directory.Exists(_dataRoot); attempt++)
        {
            try
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }
    }

    /// <summary>
    /// LIB-002/003 and LIB-008 as lived, not as wired: the watchers start with the window the way
    /// <c>ConfigureWindow</c> starts them for a person, the startup scan catalogues what was already
    /// in the folder, a file dropped afterwards is catalogued with nobody pressing anything, and the
    /// two copies reach one version group that the card can open.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task A_dropped_copy_is_catalogued_by_the_watching_application_and_groups_with_the_first()
    {
        var sample = await RequireSampleAsync("walk-copy.mp4", durationSeconds: 3);
        var watched = Path.Combine(_dataRoot, "watched");
        Directory.CreateDirectory(watched);
        File.Copy(sample, Path.Combine(watched, "Dune.2021.1080p.mp4"));
        var factory = await SeedRootAsync(watched, ScanPolicy.Startup | ScanPolicy.Continuous);

        using var host = ShowShell();
        CompositionRoot.ConfigureWindow(host.Window);

        await WaitForAsync(
            async () => await CountAsync(factory, "media_files") == 1,
            "the startup scan never catalogued the copy that was already there");

        File.Copy(sample, Path.Combine(watched, "Dune.2021.720p.mp4"));
        await WaitForAsync(
            async () => await CountAsync(factory, "media_files") == 2,
            "the watcher never catalogued the copy dropped after the window opened");
        await WaitForAsync(
            async () => await CountAsync(factory, "media_version_groups") >= 1,
            "the watcher's scan never grouped the two copies");

        // The group is reachable from a card, which is where a person meets it.
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(library.Items);
        await library.OpenDetailsAsync(library.Items[0], TestContext.Current.CancellationToken);
        await host.ViewModel.OpenDuplicatesAsync(TestContext.Current.CancellationToken);
        Assert.True(host.ViewModel.HasDuplicates, "The two copies never became a group a card can open.");
    }

    /// <summary>
    /// PLY-014 and BUG-008 as lived: a real video decoding through the session the card opened, the
    /// space bar pausing and resuming it through the assembled chain, and a marker saved mid-session
    /// making the skip offer appear on the playhead without closing and reopening anything.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_keys_pause_the_playing_video_and_a_marker_saved_mid_session_offers_the_skip()
    {
        var sample = await RequireSampleAsync("walk-feature.mp4", durationSeconds: 8);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.mp4");
        File.Copy(sample, mediaPath);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromSeconds(8));

        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        library.MovieDetails.PlayCommand.Execute(null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");

        // The keyboard operates the session through the assembled chain: view → shared map →
        // router → coordinator → engine.
        var playerView = host.Shell.GetVisualDescendants().OfType<PlayerView>().First();
        Assert.True(playerView.Focus(), "The player surface refused the focus.");
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPaused == true),
            "the space bar never paused the playing session");

        // The router coalesces one command arriving twice within 250 ms — the media-key rule — so
        // the second press waits the way a person's second press does.
        await Task.Delay(300, TestContext.Current.CancellationToken);
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the space bar never resumed the paused session");

        // A marker made mid-playback reaches the skip offer without reopening: the save recomposes
        // the session's ranges and the next position event applies them.
        var surfaces = host.ViewModel.Player;
        Assert.NotNull(surfaces?.Markers);
        Assert.NotNull(surfaces?.Skip);
        Assert.False(surfaces!.Skip!.IsVisible, "The skip offer was on screen before any marker existed.");
        surfaces.Markers!.SelectedKind = MarkerKind.Intro;
        surfaces.Markers.StartSeconds = 0;
        surfaces.Markers.EndSeconds = 7;
        surfaces.Markers.SaveCommand.Execute(null);
        await WaitForAsync(
            () => Task.FromResult(surfaces.Skip.IsVisible),
            "the marker saved mid-session never surfaced the skip offer on the playhead");

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// PLY-011 as lived: the first episode decodes to its own end, the engine's ended state raises
    /// the offer with the next episode's name, and "play now" chains the session onto the second
    /// file — two episodes, one sitting, no hands.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_end_of_an_episode_offers_the_next_and_play_now_chains_the_session()
    {
        var sample = await RequireSampleAsync("walk-episode.mp4", durationSeconds: 3);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var first = Path.Combine(media, "Show.S01E01.mp4");
        var second = Path.Combine(media, "Show.S01E02.mp4");
        File.Copy(sample, first);
        File.Copy(sample, second);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var duration = TimeSpan.FromSeconds(3);
        var firstId = await SeedMediaFileAsync(factory, media, first, duration);
        var secondId = await SeedMediaFileAsync(factory, media, second, duration);
        await SeedSeriesAsync(factory, firstId, secondId);

        using var host = ShowShell();
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(firstId), TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the first episode never reached the playing state on the real engine");

        var overlay = host.ViewModel.Player?.NextEpisode;
        Assert.NotNull(overlay);
        await WaitForAsync(
            () => Task.FromResult(overlay!.IsVisible),
            "the end of the first episode never raised the next-episode offer");
        Assert.Equal("T1 E2", overlay!.EpisodeLabel);

        overlay.PlayNowCommand.Execute(null);
        await WaitForAsync(
            () => Task.FromResult(
                host.ViewModel.Player?.Player.MediaPath.EndsWith("Show.S01E02.mp4", StringComparison.Ordinal) == true
                && host.ViewModel.Player.Player.IsPlaying),
            "accepting the offer never chained the session onto the second episode");

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Drives the dispatcher while real time passes, because the engine works on its own threads.</summary>
    private static async Task WaitForAsync(Func<Task<bool>> condition, string complaint)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (await condition())
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail(complaint);
    }

    private static void Navigate(ShellHost host, AppRoute route)
    {
        host.ViewModel.NavigateCommand.Execute(route);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<SqliteConnectionFactory> SeedRootAsync(string mediaRoot, ScanPolicy policy)
    {
        Directory.CreateDirectory(_dataRoot);
        var factory = new SqliteConnectionFactory(new AppDataPaths(_dataRoot).DatabasePath);
        using (var runner = new MigrationRunner(factory))
        {
            await runner.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await new LibraryRootRepository(factory).AddAsync(
            new LibraryRoot(
                new LibraryRootId(Guid.NewGuid()),
                mediaRoot,
                RootKind.Local,
                RootAvailability.Available,
                policy),
            TestContext.Current.CancellationToken);
        return factory;
    }

    private static async Task<Guid> SeedMediaFileAsync(
        SqliteConnectionFactory factory,
        string mediaRoot,
        string mediaPath,
        TimeSpan duration)
    {
        var roots = new LibraryRootRepository(factory);
        var all = await roots.ListAsync(TestContext.Current.CancellationToken);
        var root = all.Single(candidate => candidate.Path == mediaRoot);
        var id = Guid.NewGuid();
        await new MediaFileRepository(factory).UpsertAsync(
            new MediaFile(
                new MediaFileId(id),
                root.Id,
                mediaPath,
                new FileInfo(mediaPath).Length,
                DateTimeOffset.UnixEpoch,
                new TechnicalMetadata(duration, "mp4", ["H264"], ["AAC"], 320, 240)),
            TestContext.Current.CancellationToken);
        return id;
    }

    /// <summary>
    /// One show, one season, two episodes, each backed by its file — written through SQL because the
    /// catalogue writes these rows during identification, which needs the network the harness does
    /// not have.
    /// </summary>
    private static async Task SeedSeriesAsync(SqliteConnectionFactory factory, Guid firstFile, Guid secondFile)
    {
        var showId = Guid.NewGuid();
        var firstEpisode = Guid.NewGuid();
        var secondEpisode = Guid.NewGuid();
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO titles (id, kind, primary_title, sort_title, release_year, added_utc,
                                last_played_utc, has_progress, is_personal, is_available)
            VALUES ($show, 1, 'Show', 'show', 2020, $added, NULL, 0, 0, 1);
            INSERT INTO seasons (show_id, season_number, title) VALUES ($show, 1, 'T1');
            INSERT INTO episodes (id, show_id, season_number, episode_number, absolute_number,
                                  title, sort_order, is_available)
            VALUES ($e1, $show, 1, 1, 1, 'E1', 1, 1), ($e2, $show, 1, 2, 2, 'E2', 2, 1);
            INSERT INTO episode_media (episode_id, media_file_id) VALUES ($e1, $f1), ($e2, $f2);
            """;
        command.Parameters.AddWithValue("$show", showId.ToString("D"));
        command.Parameters.AddWithValue("$added", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$e1", firstEpisode.ToString("D"));
        command.Parameters.AddWithValue("$e2", secondEpisode.ToString("D"));
        command.Parameters.AddWithValue("$f1", firstFile.ToString("D"));
        command.Parameters.AddWithValue("$f2", secondFile.ToString("D"));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountAsync(SqliteConnectionFactory factory, string table)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private ShellHost ShowShell()
    {
        Assert.NotNull(Avalonia.Application.Current);
        ApSolutions.LocalMedia.Presentation.App.ApplyLanguage(
            Avalonia.Application.Current,
            CultureInfo.GetCultureInfo("es-ES"));
        Directory.CreateDirectory(_dataRoot);
        var shell = Assert.IsType<ShellView>(CompositionRoot.CreateShell(new AppDataPaths(_dataRoot)));
        var window = new Window { Width = 1600, Height = 1000, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        return new ShellHost(window, shell, Assert.IsType<ShellViewModel>(shell.DataContext));
    }

    private sealed record ShellHost(Window Window, ShellView Shell, ShellViewModel ViewModel) : IDisposable
    {
        public void Dispose()
        {
            // The close walks the assembled path: ConfigureWindow's handler flushes and stops the
            // background work, and it needs the dispatcher pumped to finish before the directory
            // underneath it is deleted.
            Window.Close();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    /// Materialises one synthetic sample under the ignored test-media tree, or declares the machine
    /// assumption when no encoder exists — the same rule the media suites follow.
    /// </summary>
    private static async Task<string> RequireSampleAsync(string name, int durationSeconds)
    {
        var encoder = FindEncoder();
        Assert.SkipWhen(
            encoder is null,
            "ffmpeg was not found. Set FFMPEG_PATH or install ffmpeg to generate the walk's media.");
        var destination = Path.Combine(FindRepositoryRoot(), "artifacts", "test-media", "walk", name);
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            return destination;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var arguments =
            $"-hide_banner -loglevel error -nostdin -y " +
            $"-f lavfi -i testsrc2=size=320x240:rate=15:duration={durationSeconds} " +
            $"-f lavfi -i sine=frequency=440:duration={durationSeconds} " +
            $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -shortest " +
            $"\"{destination}\"";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(encoder!, arguments)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        _ = process.Start();
        var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, $"The encoder failed with exit code {process.ExitCode}: {error}");
        return destination;
    }

    private static string? FindEncoder()
    {
        var configured = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var candidates = new List<string> { @"C:\ffmpeg\bin\ffmpeg.exe" };
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, "ffmpeg.exe")));
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
