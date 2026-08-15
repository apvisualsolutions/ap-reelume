// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Shell;
using Avalonia;
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
        host.Application.ConfigureWindow(host.Window);

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

    /// <summary>
    /// LIB-006 as lived, and with the mouse: a title somebody identified, its card opened, its
    /// editor opened, and "Refresh from provider" <em>clicked</em> — after which the entry shows
    /// what the provider says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The click is the point. This walk already drove the assembled application with
    /// <c>Window.KeyPress</c>, but nothing in the repository used
    /// <c>Avalonia.Headless</c>'s mouse input, and that gap is how a pair of buttons that were
    /// visible, enabled and incapable of doing anything survived a whole audit.
    /// </para>
    /// <para>
    /// The provider answers out of its own cache, which is the shipped path exactly: with no token
    /// configured, <c>TmdbMetadataProvider</c> serves what it has stored and opens no connection. So
    /// the real provider, the real parser and the real repository all take part — the harness only
    /// puts the payload where a previous lookup would have left it.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task Clicking_refresh_on_an_identified_title_brings_the_provider_into_the_entry()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.mp4");

        // No decoding happens here, so the file only has to exist and be catalogued.
        await File.WriteAllBytesAsync(mediaPath, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromMinutes(116));
        await SeedIdentifiedTitleAsync(factory, fileId);

        // Tall enough for the editor to be on screen without scrolling. A person with a smaller
        // window scrolls to it; the click is what this walk is here to prove, not the scrolling.
        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        await host.ViewModel.OpenMetadataEditorAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        var editor = host.ViewModel.MetadataEditor;
        Assert.NotNull(editor);
        Assert.NotEqual("La llegada", editor!.Title);

        // The control that makes the click mean something: pressing beside the button changes
        // nothing, so what changes afterwards can only have come from pressing the button itself.
        ClickBeside(host, "RefreshProviderMetadata");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual("La llegada", editor.Title);

        Click(host, "RefreshProviderMetadata");
        await WaitForAsync(
            () => Task.FromResult(editor.Title == "La llegada"),
            "clicking Refresh from provider never brought the provider's answer into the editor");

        Assert.Equal("Una lingüista traduce a los visitantes.", editor.Overview);
        Assert.False(editor.IsUnidentified);
        Assert.False(editor.HasNoProviderAnswer);

        // And it is stored, not merely on screen: the entry a person reopens tomorrow says the same.
        var stored = await new CatalogMetadataRepository(factory).GetAsync(
            new TitleId(fileId),
            TestContext.Current.CancellationToken);
        Assert.Equal("La llegada", stored?.Metadata.Title);
        Assert.NotNull(stored?.RefreshedUtc);
    }

    /// <summary>
    /// Presses a named control with the mouse, at its centre in window coordinates, the way a person
    /// with a pointing device does.
    /// </summary>
    private static void Click(ShellHost host, string controlName)
    {
        var control = host.Shell.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(candidate => candidate.Name == controlName);
        Assert.True(control is not null, $"The assembled shell has no control named {controlName}.");

        // Scrolling to it first is what a person does, and it is not optional: the editor sits far
        // enough down the shell that the button's centre lands outside the window until it is
        // brought into view, and a click there hits nothing at all. Two scroll viewers are nested
        // here, so the scroll and the layout settle over a few passes rather than one.
        var scrollers = control!.GetVisualAncestors().OfType<ScrollViewer>().ToArray();
        Point? centre = null;
        for (var settle = 0; settle < 24; settle++)
        {
            control.BringIntoView();
            host.Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            centre = control.TranslatePoint(
                new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
                host.Window);
            if (centre is { } candidate && IsUnder(host, candidate, control))
            {
                break;
            }

            // BringIntoView stops at the nearest edge, which leaves a control at the very bottom of a
            // nested viewer still clipped. The wheel keeps going, so this does too.
            foreach (var scroller in scrollers.Where(s => s.Offset.Y < s.Extent.Height - s.Viewport.Height))
            {
                scroller.Offset = scroller.Offset.WithY(Math.Min(
                    scroller.Offset.Y + 120,
                    Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height)));
            }
        }

        Assert.True(
            control!.IsEffectivelyVisible && control.IsEffectivelyEnabled,
            $"{controlName} is on screen but cannot be pressed: "
            + $"visible={control.IsEffectivelyVisible}, enabled={control.IsEffectivelyEnabled}.");
        Assert.True(centre.HasValue, $"{controlName} has no position in the window.");

        host.Window.MouseMove(centre.Value, RawInputModifiers.None);
        host.Window.MouseDown(centre.Value, MouseButton.Left, RawInputModifiers.None);
        host.Window.MouseUp(centre.Value, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Presses well clear of a control, on the empty strip above the row it sits in. It is the
    /// control for the click: whatever the button does, this must not do it.
    /// </summary>
    private static void ClickBeside(ShellHost host, string controlName)
    {
        var control = host.Shell.GetVisualDescendants()
            .OfType<Control>()
            .First(candidate => candidate.Name == controlName);
        control.BringIntoView();
        host.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            host.Window);
        Assert.True(centre.HasValue, $"{controlName} has no position in the window.");
        var beside = centre!.Value.WithY(centre.Value.Y - control.Bounds.Height);
        host.Window.MouseMove(beside, RawInputModifiers.None);
        host.Window.MouseDown(beside, MouseButton.Left, RawInputModifiers.None);
        host.Window.MouseUp(beside, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Whether a click at this point would land on that control, or inside it.</summary>
    private static bool IsUnder(ShellHost host, Point point, Control control) =>
        host.Window.InputHitTest(point) is Visual hit
        && (ReferenceEquals(hit, control) || hit.GetVisualAncestors().Contains(control));

    /// <summary>
    /// A title somebody already identified, plus the provider answer a previous lookup cached.
    /// Written through the real repository and the real cache so the refresh reads them the way the
    /// application does.
    /// </summary>
    private static async Task SeedIdentifiedTitleAsync(SqliteConnectionFactory factory, Guid fileId)
    {
        const string ProviderKey = "movie:329865";
        _ = await new CatalogMetadataRepository(factory).TrySaveAsync(
            new CatalogMetadata(
                new TitleId(fileId),
                new EditableMetadata(
                    "Arrival 2016",
                    OriginalTitle: null,
                    Overview: null,
                    ReleaseYear: null,
                    Genres: [],
                    PosterPath: null,
                    BackdropPath: null,
                    TrailerKey: null,
                    LockedFields: new HashSet<MetadataField>()),
                Revision: 0,
                Provider: "tmdb",
                ProviderKey: ProviderKey),
            expectedRevision: 0,
            TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        await new SqliteMetadataCache(factory).StoreAsync(
            new MetadataCacheEntry(
                new MetadataCacheKey("tmdb", ProviderKey, "es-ES", new TmdbOptions(accessToken: null).ProviderVersion),
                """
                {
                  "title": "La llegada",
                  "original_title": "Arrival",
                  "overview": "Una lingüista traduce a los visitantes.",
                  "release_date": "2016-11-11",
                  "genres": [{ "name": "Ciencia ficción" }],
                  "poster_path": "/poster.jpg",
                  "backdrop_path": "/backdrop.jpg"
                }
                """,
                ETag: null,
                now,
                now.AddDays(1)),
            TestContext.Current.CancellationToken);
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

    private ShellHost ShowShell(int height = 1000)
    {
        Assert.NotNull(Avalonia.Application.Current);
        ApSolutions.LocalMedia.Presentation.App.ApplyLanguage(
            Avalonia.Application.Current,
            CultureInfo.GetCultureInfo("es-ES"));
        Directory.CreateDirectory(_dataRoot);
        var application = ApplicationHost.Create(new AppDataPaths(_dataRoot));

        // The window shows what the application put in it — the container — rather than the shell
        // lifted out of it. Lifting it out left the whole tree attached to a container that was never
        // shown, and a Button only evaluates its Command once it is on the logical tree: every
        // command-bound button in the shell reported itself disabled, so no click could reach one.
        // Buttons wired with Click= were unaffected, which is why nothing noticed until this walk
        // tried to press one.
        var created = application.CreateShell();
        var window = new Window { Width = 1600, Height = height, Content = created };
        window.Show();

        // ARQ-005: the shell arrives after the database is ready, not with it, so the walk waits
        // for it. The wait names what stood in its place if it never comes.
        var shell = Assert.IsType<ShellView>(AssembledStartup.FinalContent(created));
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        return new ShellHost(application, window, shell, Assert.IsType<ShellViewModel>(shell.DataContext));
    }

    private sealed record ShellHost(
        ApplicationHost Application,
        Window Window,
        ShellView Shell,
        ShellViewModel ViewModel) : IDisposable
    {
        public void Dispose()
        {
            // The close walks the assembled path: the window lifecycle's handler flushes and stops
            // the background work, and it needs the dispatcher pumped to finish before the directory
            // underneath it is deleted.
            Window.Close();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(50);
            }

            // And then the application lets go of what it built, which is what a real exit does.
            Application.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
        var destination = Path.Combine(RepositoryLayout.Root, "artifacts", "test-media", "walk", name);
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
}
