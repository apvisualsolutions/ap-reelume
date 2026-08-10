// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.About;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Updates;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Shell;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The application, not the surfaces it is made of.
/// <para>
/// Every other suite constructs the screen it is about to examine. This one asks the composition root
/// for the shell it would hand to the main window, walks the destinations a person walks, and looks
/// for the surfaces in the tree that came back. A surface that only exists when a test builds it is
/// exactly what ADR-0003 found fourteen of.
/// </para>
/// </summary>
[Collection(AssembledShellSuites.Name)]
public sealed class AssembledJourneyTests : IDisposable
{
    private readonly string _dataRoot = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"assembled-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    [AvaloniaFact]
    public void The_library_route_offers_a_way_to_add_a_folder()
    {
        using var host = ShowShell();

        Navigate(host, AppRoute.Library);

        Assert.NotNull(Find<RootOnboardingView>(host));
        Assert.NotNull(Find<LibraryView>(host));
    }

    [AvaloniaFact]
    public void The_review_route_offers_the_inbox()
    {
        using var host = ShowShell();

        Navigate(host, AppRoute.Review);

        Assert.NotNull(Find<ReviewInboxView>(host));
    }

    /// <summary>
    /// Settings carries the four preference surfaces plus the two that were built for the player and
    /// never reachable, and the credits the TMDB licence requires somebody to be able to read.
    /// </summary>
    [AvaloniaFact]
    public void The_settings_route_carries_every_preference_surface_and_the_credits()
    {
        using var host = ShowShell();

        Navigate(host, AppRoute.Settings);

        Assert.NotNull(Find<AppearanceSettingsView>(host));
        Assert.NotNull(Find<RecommendationSettingsView>(host));

        // The threshold section only exists when the application handed the use case over
        // (CNT-A01): a hidden section here would mean the settings were assembled without it.
        Assert.True(host.ViewModel.RecommendationSettings!.HasWatchedThreshold);
        Assert.NotNull(Find<ScanSettingsView>(host));
        Assert.NotNull(Find<ShortcutSettingsView>(host));
        Assert.NotNull(Find<SubtitleStyleView>(host));
        Assert.NotNull(Find<LifecycleSettingsView>(host));
        Assert.NotNull(Find<PrivacySettingsView>(host));
        Assert.NotNull(Find<UpdateView>(host));
        Assert.NotNull(Find<CreditsView>(host));
    }

    /// <summary>
    /// Reaching the update surface is not the same as it being wired. This asks the application for
    /// the one it built: whether it knows what version it is, whether it is looking for updates on
    /// its own, and whether anything has been staged before a person has said a word.
    /// </summary>
    /// <remarks>
    /// A first run that had already downloaded something would be the worst possible finding here,
    /// and it is the kind that only shows up when the composition root — not a test — builds the
    /// thing.
    /// </remarks>
    [AvaloniaFact]
    public void The_update_surface_arrives_wired_switched_off_and_with_nothing_staged()
    {
        using var host = ShowShell();
        Navigate(host, AppRoute.Settings);

        var updates = host.ViewModel.Updates;

        Assert.NotNull(updates);
        Assert.False(updates!.AutomaticCheckEnabled, "A first run is already looking for updates.");
        Assert.False(updates.HasOffer);
        Assert.False(updates.IsReadyToInstall);
        Assert.Equal("UpdateStatusIdle", updates.StatusKey);
        Assert.False(
            Directory.Exists(Path.Combine(_dataRoot, "updates")),
            "Something was staged before anybody asked for an update.");
    }

    /// <summary>
    /// A licence condition nobody can read is not met. The attribution has to be in the tree, have a
    /// name assistive technology can announce, and actually say what TMDB requires it to say.
    /// </summary>
    [AvaloniaFact]
    public void The_tmdb_attribution_is_announced_and_readable_inside_the_application()
    {
        using var host = ShowShell();
        Navigate(host, AppRoute.Settings);

        var credits = Find<CreditsView>(host);

        Assert.NotNull(credits);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(credits!)));
        var text = string.Join(
            " ",
            credits!.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));
        Assert.Contains("TMDB", text, StringComparison.Ordinal);
        Assert.Contains("GPL-3.0", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void The_card_actions_reach_the_editor_the_rename_preview_and_the_versions()
    {
        using var host = ShowShell();
        Navigate(host, AppRoute.Library);

        var buttons = host.Shell.GetVisualDescendants()
            .OfType<Button>()
            .Select(button => AutomationProperties.GetName(button))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.Contains(Resource("TitleEditMetadataAction"), buttons);
        Assert.Contains(Resource("TitlePreviewRenameAction"), buttons);
        Assert.Contains(Resource("TitleReviewDuplicatesAction"), buttons);
    }

    /// <summary>
    /// The player is a surface of the application rather than a window a test opens. With no session
    /// it is out of the way; the shell still owns it, and the card is what turns it on.
    /// </summary>
    [AvaloniaFact]
    public void The_player_belongs_to_the_shell_and_stays_out_of_the_way_until_a_card_asks_for_it()
    {
        using var host = ShowShell();

        Assert.False(host.ViewModel.IsPlayerVisible);
        var surface = host.Shell.GetVisualDescendants()
            .OfType<Control>()
            .SingleOrDefault(control => control.Name == "PlayerSurface");
        Assert.NotNull(surface);
        Assert.False(surface!.IsVisible);

        // A first run lands on Home, so the welcome card is what the player would have to cover.
        Assert.Equal(AppRoute.Home, host.ViewModel.CurrentRoute);
        Assert.True(host.ViewModel.IsHomeVisible);
    }

    /// <summary>
    /// The walk the increment exists for: a folder with a file in it, a card opened from the library,
    /// and every action that card offers reaching the surface it promises. Nothing here is built by
    /// the test except the file on disk; the application assembles the rest.
    /// </summary>
    [AvaloniaFact]
    public async Task A_card_opened_from_the_library_reaches_the_editor_the_rename_and_the_player()
    {
        var mediaPath = SeedOneFile();
        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        var item = Assert.Single(library.Items);
        await library.OpenDetailsAsync(item, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        await host.ViewModel.OpenMetadataEditorAsync(TestContext.Current.CancellationToken);
        Assert.True(host.ViewModel.HasMetadataEditor);
        Assert.Equal(item.Title, host.ViewModel.MetadataEditor!.Title);

        await host.ViewModel.OpenRenamePreviewAsync(TestContext.Current.CancellationToken);
        Assert.True(host.ViewModel.HasRename);
        Assert.False(host.ViewModel.Rename!.IsConfirmed);
        Assert.True(File.Exists(mediaPath), "Asking for a preview must not move anything.");

        // A title with one copy has no version group, so the comparison is absent rather than empty.
        await host.ViewModel.OpenDuplicatesAsync(TestContext.Current.CancellationToken);
        Assert.False(host.ViewModel.HasDuplicates);

        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Find<MetadataEditorView>(host));
        Assert.NotNull(Find<RenamePreviewView>(host));
    }

    /// <summary>
    /// Playback starts from the card, through the application's own wiring. The seeded file is one
    /// byte, so the engine reports a failure rather than a picture — and that is the point: the
    /// session, its surfaces, and its diagnosis all reached the screen, which is what was missing.
    /// </summary>
    [AvaloniaFact]
    public async Task Playing_from_a_card_opens_the_session_the_application_wired()
    {
        _ = SeedOneFile();
        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);

        library.MovieDetails.PlayCommand.Execute(null);
        await WaitForAsync(() => host.ViewModel.IsPlayerVisible);

        Assert.True(host.ViewModel.IsPlayerVisible);
        Assert.True(host.ViewModel.HasTracks);
        Assert.True(host.ViewModel.HasAudioOutput);
        Assert.True(host.ViewModel.HasMarkers);
        Assert.True(host.ViewModel.HasVideoStatus);

        // A title with one copy has no version group, so the switch action is absent rather than
        // an empty list (VSW-A01) — and the confirmation dialog arrives wired but silent.
        Assert.False(host.ViewModel.HasPlayerVersions);
        Assert.True(host.ViewModel.HasVersionSwitch);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Find<PlayerView>(host));
        Assert.NotNull(Find<TrackSelectorView>(host));
        Assert.NotNull(Find<AudioOutputView>(host));
        Assert.NotNull(Find<MarkerEditorView>(host));

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
        Assert.False(host.ViewModel.IsPlayerVisible);
    }

    /// <summary>
    /// The watched toggle was built with a null handler (CNT-A01): every mark went nowhere and the
    /// card forgot it on the next load. This walks the mark through the application's own wiring
    /// into SQLite and back: mark, reload, clear — and the state is always what the repository says.
    /// </summary>
    [AvaloniaFact]
    public async Task Marking_a_card_watched_survives_reloading_through_the_application_wiring()
    {
        _ = SeedOneFile();
        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        var status = library.MovieDetails.WatchStatus;
        Assert.True(status.IsNotStarted);

        status.MarkWatchedCommand.Execute(null);
        await WaitForAsync(() => status.IsWatched);
        Assert.True(status.IsManualOverride);

        // Reloading the card reads the repository again, so surviving the reload is what proves
        // the mark was stored rather than painted.
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        Assert.True(status.IsWatched);
        Assert.True(status.IsManualOverride);

        // Clearing hands the state back to the rules: nothing was ever played, so it is unwatched.
        status.ClearOverrideCommand.Execute(null);
        await WaitForAsync(() => !status.IsManualOverride);
        Assert.True(status.IsNotStarted);
    }

    /// <summary>
    /// Removing a folder is a decision about the catalog and nothing else (LIB-A01): the library
    /// route lists the folders, asking removes nothing, and confirming removes the root while
    /// every video stays on disk exactly where it was.
    /// </summary>
    [AvaloniaFact]
    public async Task Removing_a_folder_from_the_library_route_leaves_every_video_on_disk()
    {
        var mediaPath = SeedOneFile();
        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var onboarding = host.ViewModel.Onboarding;
        Assert.NotNull(onboarding);
        await WaitForAsync(() => onboarding!.HasRoots);
        var row = Assert.Single(onboarding!.Roots);

        onboarding.RequestRemoveCommand.Execute(row);
        Assert.True(onboarding.IsConfirmingRemoval);
        Assert.True(File.Exists(mediaPath), "Asking for a removal must not touch the disk.");

        onboarding.ConfirmRemoveCommand.Execute(null);
        await WaitForAsync(() => !onboarding.HasRoots);

        Assert.False(onboarding.IsConfirmingRemoval);
        Assert.True(File.Exists(mediaPath), "Removing from the catalog must not touch the disk.");
    }

    /// <summary>Drives the dispatcher until a condition holds, because the card starts playback asynchronously.</summary>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void A_first_run_reaches_every_destination_without_anything_in_the_library()
    {
        using var host = ShowShell();

        foreach (var route in Enum.GetValues<AppRoute>())
        {
            Navigate(host, route);
            Assert.Equal(route, host.ViewModel.CurrentRoute);
        }
    }

    private static string Resource(string key)
    {
        Assert.NotNull(Avalonia.Application.Current);
        return Avalonia.Application.Current.TryGetResource(
            key,
            Avalonia.Application.Current.ActualThemeVariant,
            out var value)
            ? value?.ToString() ?? key
            : key;
    }

    private static void Navigate(ShellHost host, AppRoute route)
    {
        host.ViewModel.NavigateCommand.Execute(route);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
    }

    private static T? Find<T>(ShellHost host)
        where T : Control =>
        host.Shell.GetVisualDescendants().OfType<T>().FirstOrDefault();

    /// <summary>
    /// Puts one real file in one real root, through the repositories the application itself uses, so
    /// the library the shell loads is the library the application would have found.
    /// </summary>
    private string SeedOneFile()
    {
        Directory.CreateDirectory(_dataRoot);
        var mediaRoot = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(mediaRoot);
        var mediaPath = Path.Combine(mediaRoot, "pelicula.mkv");
        File.WriteAllBytes(mediaPath, [0]);

        var paths = new AppDataPaths(_dataRoot);
        var factory = new SqliteConnectionFactory(paths.DatabasePath);
        using (var runner = new MigrationRunner(factory))
        {
            runner.MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        var rootId = new LibraryRootId(Guid.NewGuid());
        new LibraryRootRepository(factory)
            .AddAsync(
                new LibraryRoot(rootId, mediaRoot, RootKind.Local, RootAvailability.Available, ScanPolicy.Manual),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        new MediaFileRepository(factory)
            .UpsertAsync(
                new MediaFile(
                    new MediaFileId(Guid.NewGuid()),
                    rootId,
                    mediaPath,
                    1,
                    DateTimeOffset.UnixEpoch,
                    new TechnicalMetadata(TimeSpan.FromMinutes(90), "mkv", ["HEVC"], ["EAC3"], 1920, 1080)),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return mediaPath;
    }

    private ShellHost ShowShell()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        Directory.CreateDirectory(_dataRoot);
        var application = ApplicationHost.Create(new AppDataPaths(_dataRoot));
        var shell = Assert.IsType<ShellView>(application.CreateShell());
        var window = new Window { Width = 1600, Height = 1000, Content = shell };
        window.Show();
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
            Window.Close();
            Application.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

/// <summary>
/// The two suites that build the whole application, kept together.
/// </summary>
/// <remarks>
/// This collection used to disable parallelisation, and the reason was a defect rather than a
/// property of the tests: the composition root kept its services in a static field, so two
/// applications in one process each saw the other's. ARQ-001 gave the provider an owner, one per
/// host, and the switch came off — which is the proof that the ownership is real and not just
/// tidier. If it ever has to go back, the question to ask first is what became shared again.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class AssembledShellSuites
{
    public const string Name = "AssembledShell";
}
