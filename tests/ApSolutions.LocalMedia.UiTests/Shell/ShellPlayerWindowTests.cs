// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The shell owns the window the mini player lives in.
/// <para>
/// A mode change has to move the surface that is already playing rather than build a second one:
/// building a new player for the mini window is how two sessions end up running at the same time,
/// and it is the reason the coordinator hands the same control back and forth.
/// </para>
/// </summary>
public sealed class ShellPlayerWindowTests
{
    private static readonly MediaFileId MediaFile = new(new Guid("44444444-4444-4444-4444-444444444444"));

    [AvaloniaFact]
    public async Task The_mini_mode_moves_the_playing_surface_out_of_the_shell_and_back()
    {
        var (window, view, viewModel) = Show();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        var stage = Stage(view);
        var host = Host(view);
        Assert.Same(host, stage.Parent);

        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // The stage is inside the mini window rather than being all of it: the window keeps a tree of
        // its own, which is where its five controls live. What is asserted is the window it ended up
        // under, because the parent it hangs from is now the panel that holds it.
        var mini = Assert.IsType<MiniPlayerWindow>(
            stage.GetVisualAncestors().OfType<Window>().FirstOrDefault());
        Assert.True(mini.Topmost);
        Assert.Contains(stage, mini.GetVisualDescendants());
        Assert.NotEmpty(mini.GetVisualDescendants().OfType<MiniPlayerChromeView>());

        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(host, stage.Parent);
        Assert.Equal(PlaybackMode.Embedded, viewModel.PlaybackMode);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Closing_the_session_takes_the_mini_window_with_it()
    {
        var (window, view, viewModel) = Show();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        var stage = Stage(view);
        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<MiniPlayerWindow>(stage.GetVisualAncestors().OfType<Window>().FirstOrDefault());

        await viewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(Host(view), stage.Parent);
        Assert.False(viewModel.IsPlayerVisible);
        window.Close();
    }

    /// <summary>
    /// Where the mini player was left is written down when it closes, and read back when it opens.
    /// </summary>
    /// <remarks>
    /// This is the path that stopped <c>PlayerWindowCoordinator.Remember</c> being registered and
    /// never fed. It is written on the close and not on the move for a measured reason: a move drag
    /// raises <c>PositionChanged</c> on every frame and the placement goes to a file, so saving per
    /// frame would put a few hundred writes behind one gesture for an answer read once a launch.
    /// </remarks>
    [AvaloniaFact]
    public async Task Where_the_mini_player_was_left_is_written_down_when_it_closes()
    {
        var placements = new RecordingPlacements();
        var viewModel = CreateViewModel(placements);
        var view = new ShellView { DataContext = viewModel };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        var mini = Assert.IsType<MiniPlayerWindow>(
            Stage(view).GetVisualAncestors().OfType<Window>().FirstOrDefault());
        Assert.Null(placements.Saved);

        mini.Position = new PixelPoint(360, 240);
        mini.Width = 560;
        Dispatcher.UIThread.RunJobs();

        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(placements.Saved);
        Assert.Equal(560, placements.Saved!.Width);
        Assert.Equal(360 / mini.RenderScaling, placements.Saved.X);
        Assert.Equal(240 / mini.RenderScaling, placements.Saved.Y);

        // And the second visit opens on what the first one wrote, through a window built afresh:
        // the shell closes the mini window on the way back rather than hiding it.
        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        var second = Assert.IsType<MiniPlayerWindow>(
            Stage(view).GetVisualAncestors().OfType<Window>().FirstOrDefault());

        Assert.Equal(560, second.Width);
        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        window.Close();
    }

    /// <summary>
    /// A shell whose data context is replaced stops listening to the old one. Without that, two view
    /// models would both drive the same window and the second mode change would fight the first.
    /// </summary>
    [AvaloniaFact]
    public async Task A_replaced_view_model_no_longer_drives_the_window()
    {
        var (window, view, first) = Show();
        var second = CreateViewModel();
        view.DataContext = second;
        Dispatcher.UIThread.RunJobs();

        await first.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await first.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(Host(view), Stage(view).Parent);
        window.Close();
    }

    [AvaloniaFact]
    public void A_shell_without_a_view_model_at_all_still_shows()
    {
        var view = new ShellView();
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(Stage(view));
        window.Close();
    }

    /// <summary>
    /// A session leaves the rail reachable, heads itself with three glyphs, and gives the picture the
    /// side column's width whenever no panel is using it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All three are the prototype's composition and all three were different: the player spanned
    /// both columns so opening a film took the five destinations away with it, its three session
    /// buttons carried words at the head of a 320 px column of panels, and that column was 320 px of
    /// window whether or not one of its five panels existed.
    /// </para>
    /// <para>
    /// The rail is asserted by measuring where the session starts rather than by looking for the
    /// rail: a destination that is on the tree behind an opaque surface is a destination nobody can
    /// press, and only the geometry says which it is.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task A_session_heads_itself_leaves_the_rail_alone_and_only_takes_the_column_it_uses()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var (window, view, viewModel) = Show();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        // The rail's 64 px are still the rail's.
        var surface = view.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "PlayerSurface");
        var left = surface.TranslatePoint(default, window)!.Value.X;
        Assert.Equal(64, left);

        // The header, and the buttons this session actually draws, named in the language in force
        // rather than by their glyphs — the glyph is what the eye lands on and the name is what a
        // reader hears. Effectively visible and not merely declared: the six panel pills are
        // declared on every session and drawn only by the ones that have those panels, and this
        // file has none of them, so what is left is the one that is always there.
        //
        // One and not three since 2026-08-25: full screen and the floating window are in the
        // transport bar now, which is where the owner looked for them, and they are not in both
        // places. Two buttons answering to «Pantalla completa» on one screen is a name that names
        // neither, and the walk says so out loud — it refuses a click it cannot aim.
        var header = view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PlayerHeaderSurface");
        var headed = header.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .Select(button => Avalonia.Automation.AutomationProperties.GetName(button) ?? string.Empty)
            .ToArray();
        Assert.Equal(["Cerrar el reproductor"], headed);
        Assert.All(
            header.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible),
            button => Assert.Contains("player-chrome", button.Classes));

        // The pills are declared even here, and every one of them is a player-pill: a session that
        // grew a track list must not find a pill wearing the wrong grammar.
        //
        // Six since 2026-09-01, when «Lecciones» arrived (CRS-004). It is declared like the other
        // five and drawn like them only when the session has it — and this session is a loose file,
        // which is not a lesson, so the sixth is exactly as absent as the rest.
        Assert.Equal(
            6,
            header.GetVisualDescendants()
                .OfType<Button>()
                .Count(button => button.Classes.Contains("player-pill")));

        // And the column: this session has no panel at all, so no pill is drawn, nothing has been
        // opened, and it takes no width.
        Assert.False(viewModel.HasPlayerPanels);
        Assert.False(viewModel.IsPlayerPanelOpen);
        // By name and not by type: the column was a TabControl until the pills took over the
        // switching, and what this measures is its width.
        var column = view.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "PlayerPanelColumn");
        Assert.False(column.IsVisible);
        Assert.Equal(0, column.Bounds.Width);
        window.Close();
    }

    // The stage is looked up by name rather than walked to: once the mini window owns it, it is no
    // longer a visual descendant of the shell, which is the whole thing under test.
    private static Panel Stage(ShellView view) =>
        view.FindControl<Panel>("PlayerStage") ?? throw new InvalidOperationException("No player stage.");

    private static ContentControl Host(ShellView view) =>
        view.FindControl<ContentControl>("PlayerHost") ?? throw new InvalidOperationException("No player host.");

    private static (Window Window, ShellView View, ShellViewModel ViewModel) Show()
    {
        var viewModel = CreateViewModel();
        var view = new ShellView { DataContext = viewModel };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view, viewModel);
    }

    private static ShellViewModel CreateViewModel(IMiniPlayerPlacementStore? placements = null) => new(
        new NavigationService(),
        new ShellSurfaces
        {
            OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(new PlayerSurfaces
            {
                Player = new PlayerViewModel(new InertCoordinator()),
            }),
            ClosePlayer = _ => Task.CompletedTask,
            ChangePlaybackMode = (mode, _) => Task.FromResult(mode),
            MiniPlayerPlacement = placements,
        });

    private sealed class RecordingPlacements : IMiniPlayerPlacementStore
    {
        public MiniPlayerPlacement? Saved { get; set; }

        public MiniPlayerPlacement? Read() => Saved;

        public void Save(MiniPlayerPlacement placement) => Saved = placement;
    }

    private sealed class InertCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlaybackSession(Guid.Empty, request.MediaFileId, request.Path));

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
