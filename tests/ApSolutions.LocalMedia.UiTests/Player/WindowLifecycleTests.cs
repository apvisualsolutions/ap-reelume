// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Window modes must move the same surface rather than build a new one, and fullscreen must size
/// itself in logical units. Sizing in physical pixels is exactly the defect that pushed the transport
/// bar off a 150% display in the T18 spike.
/// </summary>
public sealed class WindowLifecycleTests
{
    private static readonly PixelRect Screen2560X1440 = new(0, 0, 2560, 1440);

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Fullscreen_geometry_is_the_screen_in_logical_units_not_in_physical_pixels(double scaling)
    {
        var geometry = PlayerWindowCoordinator.GeometryFor(
            PlaybackMode.Fullscreen,
            Screen2560X1440,
            scaling);

        Assert.Equal(Screen2560X1440.Width / scaling, geometry.Width);
        Assert.Equal(Screen2560X1440.Height / scaling, geometry.Height);
        Assert.Equal(0, geometry.X);
        Assert.Equal(0, geometry.Y);
    }

    [AvaloniaFact]
    public void At_one_hundred_and_fifty_percent_the_transport_bar_stays_on_the_screen()
    {
        const double scaling = 1.5;
        var geometry = PlayerWindowCoordinator.GeometryFor(
            PlaybackMode.Fullscreen,
            Screen2560X1440,
            scaling);
        var viewModel = new PlayerViewModel(new InertCoordinator());
        var view = new PlayerView { DataContext = viewModel };
        var window = new Window { Width = geometry.Width, Height = geometry.Height, Content = view };
        window.SetRenderScaling(scaling);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var transport = view.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Name == "TransportControlsSurface");
        var origin = transport.TranslatePoint(default, view);
        Assert.NotNull(origin);
        var bottomInPhysicalPixels = (origin!.Value.Y + transport.Bounds.Height) * scaling;

        Assert.InRange(bottomInPhysicalPixels, 1, Screen2560X1440.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void The_defect_this_guards_against_is_reproducible_from_physical_pixel_sizing()
    {
        const double scaling = 1.5;

        // The old behaviour: the window is given the screen size in physical pixels while rendering
        // still scales, so a bottom-anchored bar lands past the bottom of the display.
        var wrong = new PlayerWindowGeometry(0, 0, Screen2560X1440.Width, Screen2560X1440.Height);
        var right = PlayerWindowCoordinator.GeometryFor(PlaybackMode.Fullscreen, Screen2560X1440, scaling);

        Assert.True(wrong.Height * scaling > Screen2560X1440.Height);
        Assert.Equal(Screen2560X1440.Height, right.Height * scaling);
    }

    [AvaloniaFact]
    public void Each_mode_leaves_the_window_in_the_shape_that_mode_asks_for()
    {
        var coordinator = new PlayerWindowCoordinator();
        var window = new Window { Width = 800, Height = 600 };
        window.Show();

        foreach (var mode in new[] { PlaybackMode.Embedded, PlaybackMode.Fullscreen, PlaybackMode.Mini })
        {
            coordinator.Apply(window, mode, Screen2560X1440, 1.5);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(mode, coordinator.Current);
        }

        Assert.True(window.Topmost);
        coordinator.Apply(window, PlaybackMode.Embedded, Screen2560X1440, 1.5);
        Assert.False(window.Topmost);
        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        window.Close();
    }

    /// <summary>
    /// Fullscreen is a window state and not just a size, and leaving it gives the state back.
    /// </summary>
    /// <remarks>
    /// <b>It was only a size until 2026-09-02</b>, and the owner reported what that costs: the
    /// Windows taskbar stayed on top of the picture. A window merely as large as the screen is not a
    /// fullscreen window — the taskbar is drawn over ordinary windows whatever their size and steps
    /// aside only for this state. Measured on a 2560x1440 display whose working area is 1392 tall:
    /// 48 px of taskbar over the video.
    /// <para>
    /// The other direction matters as much and is easier to lose: a window left in the state is a
    /// window whose Width and Height are stored and never drawn, so the embedded mode would come
    /// back the size of the screen. That is why the state is dropped before the geometry is written
    /// and set after it.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Fullscreen_is_a_window_state_and_leaving_it_gives_the_state_back()
    {
        var coordinator = new PlayerWindowCoordinator();
        var window = new Window { Width = 800, Height = 600 };
        window.Show();

        coordinator.Apply(window, PlaybackMode.Fullscreen, Screen2560X1440, 1.0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WindowState.FullScreen, window.WindowState);

        // And the size is still written, because it is what the window returns to and what decides
        // which screen the state applies on.
        Assert.Equal(2560, window.Width);
        Assert.Equal(1440, window.Height);

        coordinator.Apply(window, PlaybackMode.Embedded, Screen2560X1440, 1.0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WindowState.Normal, window.WindowState);

        // The embedded geometry actually reached the window rather than being stored behind a state
        // that ignores it.
        Assert.NotEqual(2560, window.Width);
        window.Close();
    }

    /// <summary>The mini player is not a fullscreen window, and never inherits the state.</summary>
    [AvaloniaFact]
    public void Going_from_fullscreen_to_the_mini_player_leaves_the_state_behind()
    {
        var coordinator = new PlayerWindowCoordinator();
        var window = new Window { Width = 800, Height = 600 };
        window.Show();

        coordinator.Apply(window, PlaybackMode.Fullscreen, Screen2560X1440, 1.0);
        coordinator.Apply(window, PlaybackMode.Mini, Screen2560X1440, 1.0);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.True(window.Topmost);
        Assert.Equal(PlayerWindowCoordinator.DefaultMiniGeometry.Width, window.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void Fullscreen_removes_the_decorations_and_returning_restores_them()
    {
        var coordinator = new PlayerWindowCoordinator();
        var window = new Window { Width = 800, Height = 600 };
        window.Show();

        coordinator.Apply(window, PlaybackMode.Fullscreen, Screen2560X1440, 1.5);
        Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        Assert.False(window.Topmost);

        coordinator.Apply(window, PlaybackMode.Embedded, Screen2560X1440, 1.5);
        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        window.Close();
    }

    [AvaloniaFact]
    public void Only_geometry_that_is_actually_on_a_screen_is_remembered()
    {
        var coordinator = new PlayerWindowCoordinator();
        var offScreen = new PlayerWindowGeometry(-9000, -9000, 480, 270);
        var onScreen = new PlayerWindowGeometry(100, 100, 480, 270);

        coordinator.Remember(PlaybackMode.Mini, offScreen, Screen2560X1440, 1.5);
        Assert.Null(coordinator.Recall(PlaybackMode.Mini));

        coordinator.Remember(PlaybackMode.Mini, onScreen, Screen2560X1440, 1.5);
        Assert.Equal(onScreen, coordinator.Recall(PlaybackMode.Mini));
        Assert.False(offScreen.IsVisibleOn(Screen2560X1440, 1.5));
        Assert.True(onScreen.IsVisibleOn(Screen2560X1440, 1.5));
    }

    /// <summary>
    /// A hundred mode changes leave one window and one surface, on the path the shell actually takes.
    /// </summary>
    /// <remarks>
    /// The window is a real <see cref="MiniPlayerWindow"/> and the surface is placed the way the shell
    /// places it, because that is where a second video frame would be built if one were going to be.
    /// A generic window and an assignment would have proved it about a path nothing runs.
    /// </remarks>
    [AvaloniaFact]
    public void A_hundred_mode_changes_leave_one_window_and_one_surface()
    {
        var coordinator = new PlayerWindowCoordinator();
        var surface = new PlayerView { DataContext = new PlayerViewModel(new InertCoordinator()) };
        var window = new MiniPlayerWindow();
        window.Show();
        var modes = new[] { PlaybackMode.Fullscreen, PlaybackMode.Mini, PlaybackMode.Embedded };

        for (var iteration = 0; iteration < 100; iteration++)
        {
            window.Host(surface);
            coordinator.Apply(window, modes[iteration % modes.Length], Screen2560X1440, 1.5);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.Contains(surface, window.GetVisualDescendants().OfType<PlayerView>());
        Assert.Single(surface.GetVisualDescendants().OfType<VideoFrameView>());
        window.Close();
    }

    [AvaloniaFact]
    public void The_mini_player_is_always_on_top_and_out_of_the_taskbar()
    {
        var mini = new MiniPlayerWindow();
        var surface = new PlayerView { DataContext = new PlayerViewModel(new InertCoordinator()) };

        mini.Host(surface);
        mini.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(mini.Topmost);
        Assert.False(mini.ShowInTaskbar);
        Assert.Contains(surface, mini.GetVisualDescendants().OfType<PlayerView>());
        mini.Close();
    }

    /// <summary>
    /// Hosting the same surface twice leaves it where it is instead of building it again.
    /// </summary>
    /// <remarks>
    /// A mode applied twice is not rare — the shell reapplies it whenever the view model says so —
    /// and re-parenting a video surface tears its frame down and makes a new one. What that costs is
    /// the session, which is the one thing this whole coordinator exists to keep.
    /// </remarks>
    [AvaloniaFact]
    public void Hosting_the_same_surface_again_does_not_rebuild_it()
    {
        var mini = new MiniPlayerWindow();
        var surface = new PlayerView { DataContext = new PlayerViewModel(new InertCoordinator()) };

        mini.Host(surface);
        mini.Show();
        Dispatcher.UIThread.RunJobs();
        var frame = surface.GetVisualDescendants().OfType<VideoFrameView>().Single();

        mini.Host(surface);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(frame, surface.GetVisualDescendants().OfType<VideoFrameView>().Single());
        Assert.Contains(surface, mini.GetVisualDescendants().OfType<PlayerView>());

        // And letting go leaves the window without it, which is what the shell asks for on the way
        // back: a control that still has a parent cannot be given a second one.
        mini.Release();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(surface, mini.GetVisualDescendants().OfType<PlayerView>());
        mini.Close();
    }

    /// <summary>
    /// A mode that has been on screen before comes back to where it was, not to the default.
    /// </summary>
    /// <remarks>
    /// This is what <c>Remember</c> and <c>Recall</c> are for, and until 2026-08-19 the two were only
    /// tested against each other: nothing asked whether <c>Apply</c> actually used what had been
    /// remembered. A coordinator that stored a position and then ignored it would have passed every
    /// test in this file while moving somebody's mini player back to the corner on every switch.
    /// </remarks>
    [AvaloniaFact]
    public void A_mode_that_was_on_screen_before_returns_to_where_it_was_left()
    {
        var coordinator = new PlayerWindowCoordinator();
        var window = new Window { Width = 800, Height = 600 };
        window.Show();
        var moved = new PlayerWindowGeometry(300, 200, 640, 360);

        coordinator.Remember(PlaybackMode.Mini, moved, Screen2560X1440, 1.5);
        coordinator.Apply(window, PlaybackMode.Mini, Screen2560X1440, 1.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(moved.Width, window.Width);
        Assert.Equal(moved.Height, window.Height);
        Assert.NotEqual(
            PlayerWindowCoordinator.DefaultMiniGeometry.Width,
            window.Width);
        window.Close();
    }

    /// <summary>
    /// Embedded uses the geometry it is handed, and falls back only when handed none.
    /// </summary>
    /// <remarks>
    /// The fallback is the size the shell opens at. Both halves are asserted because a parameter that
    /// is accepted and ignored looks exactly like one that works, from the side that passes it.
    /// </remarks>
    [AvaloniaFact]
    public void Embedded_uses_the_geometry_it_is_given_and_falls_back_only_without_one()
    {
        var shell = new PlayerWindowGeometry(10, 20, 1400, 900);

        var given = PlayerWindowCoordinator.GeometryFor(
            PlaybackMode.Embedded,
            Screen2560X1440,
            1.5,
            shell);
        var withoutOne = PlayerWindowCoordinator.GeometryFor(
            PlaybackMode.Embedded,
            Screen2560X1440,
            1.5);

        Assert.Equal(shell, given);
        Assert.NotEqual(shell, withoutOne);
        Assert.Equal(1180, withoutOne.Width);
        Assert.Equal(760, withoutOne.Height);
    }

    [AvaloniaFact]
    public void A_zero_or_negative_scaling_is_refused_rather_than_producing_an_infinite_window()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerWindowCoordinator.GeometryFor(PlaybackMode.Fullscreen, Screen2560X1440, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerWindowCoordinator.GeometryFor(PlaybackMode.Fullscreen, Screen2560X1440, -1.5));
    }

    /// <summary>
    /// The mini player loses its frame too, and that is not the same statement as fullscreen's.
    /// </summary>
    /// <remarks>
    /// Fullscreen without decorations gives up a title bar nobody could reach anyway. The mini
    /// player gives up the only way the system offered to move it and to close it, so both are asked
    /// for here: the mode has no frame, and it is still on top of everything.
    /// </remarks>
    [AvaloniaFact]
    public void The_mini_player_has_no_frame_either_and_is_still_on_top()
    {
        var coordinator = new PlayerWindowCoordinator();
        var window = new Window { Width = 800, Height = 600 };
        window.Show();

        coordinator.Apply(window, PlaybackMode.Mini, Screen2560X1440, 1.5);

        Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        Assert.True(window.Topmost);

        coordinator.Apply(window, PlaybackMode.Embedded, Screen2560X1440, 1.5);
        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        Assert.False(window.Topmost);
        window.Close();
    }

    /// <summary>
    /// Where the mini player was left survives the application closing, and comes back on the switch.
    /// </summary>
    /// <remarks>
    /// Until 2026-08-28 nothing in the product called <c>Remember</c> at all: it and <c>Recall</c>
    /// were written, kept in a dictionary, and read by their own tests and by nobody else — this
    /// repository's characteristic defect, wearing the shape of a coordinator. What this asserts is
    /// the path that closes it: a placement written through the store on one run is what
    /// <c>Apply</c> puts on screen on the next, with no dictionary in between.
    /// </remarks>
    [AvaloniaFact]
    public void A_mini_player_left_somewhere_opens_there_in_the_next_session()
    {
        var placements = new RecordingPlacements();
        var firstRun = new PlayerWindowCoordinator(placements);
        var moved = new PlayerWindowGeometry(700, 400, 640, 400);

        firstRun.Remember(PlaybackMode.Mini, moved, Screen2560X1440, 1.5);
        Assert.Equal(new MiniPlayerPlacement(700, 400, 640, 400), placements.Saved);

        // A coordinator of its own, with an empty dictionary, which is what the next launch has.
        var nextRun = new PlayerWindowCoordinator(placements);
        var window = new Window { Width = 800, Height = 600 };
        window.Show();
        nextRun.Apply(window, PlaybackMode.Mini, Screen2560X1440, 1.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(moved, nextRun.Recall(PlaybackMode.Mini));
        Assert.Equal(640, window.Width);
        Assert.Equal(400, window.Height);
        window.Close();
    }

    /// <summary>Only the mini player is written to disk, and only what is on a screen.</summary>
    [AvaloniaFact]
    public void Nothing_but_the_mini_player_is_written_down_and_nothing_off_screen_is()
    {
        var placements = new RecordingPlacements();
        var coordinator = new PlayerWindowCoordinator(placements);

        coordinator.Remember(
            PlaybackMode.Fullscreen,
            new PlayerWindowGeometry(0, 0, 1706, 960),
            Screen2560X1440,
            1.5);
        Assert.Null(placements.Saved);

        coordinator.Remember(
            PlaybackMode.Mini,
            new PlayerWindowGeometry(-9000, -9000, 480, 270),
            Screen2560X1440,
            1.5);
        Assert.Null(placements.Saved);
    }

    /// <summary>
    /// A placement written on another screen is not applied on this one.
    /// </summary>
    /// <remarks>
    /// <c>Remember</c> refuses what is off the screen it was written on; a second monitor is on a
    /// screen, and its coordinates land on nothing once it is unplugged. Since 2026-08-28 the window
    /// has no title bar to drag it back by, so a placement that cannot be seen has to be dropped at
    /// the moment it would be used rather than at the moment it was stored.
    /// </remarks>
    [AvaloniaFact]
    public void A_placement_from_a_screen_that_is_gone_falls_back_to_the_default()
    {
        var placements = new RecordingPlacements
        {
            Saved = new MiniPlayerPlacement(-3000, 100, 640, 400),
        };
        var coordinator = new PlayerWindowCoordinator(placements);
        var window = new Window { Width = 800, Height = 600 };
        window.Show();

        coordinator.Apply(window, PlaybackMode.Mini, Screen2560X1440, 1.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PlayerWindowCoordinator.DefaultMiniGeometry.Width, window.Width);
        Assert.Equal(PlayerWindowCoordinator.DefaultMiniGeometry.Height, window.Height);
        window.Close();
    }

    /// <summary>A coordinator with no store behind it works exactly as it did before there was one.</summary>
    [AvaloniaFact]
    public void Without_a_store_the_coordinator_remembers_only_for_as_long_as_it_runs()
    {
        var coordinator = new PlayerWindowCoordinator(placements: null);
        var moved = new PlayerWindowGeometry(300, 200, 640, 400);

        coordinator.Remember(PlaybackMode.Mini, moved, Screen2560X1440, 1.5);

        Assert.Equal(moved, coordinator.Recall(PlaybackMode.Mini));
        Assert.Null(new PlayerWindowCoordinator(placements: null).Recall(PlaybackMode.Mini));
    }

    /// <summary>
    /// A resize keeps the picture at 16:9, and the side that gives way is the one that moved less.
    /// </summary>
    /// <remarks>
    /// The second half is the whole reason this takes a previous geometry. A constraint that always
    /// derived the height would snap back on every frame of a drag on the bottom edge, which reads
    /// as a window that refuses to be resized rather than as one that keeps its shape.
    /// </remarks>
    [AvaloniaTheory]
    // Dragged wider: the height follows the width.
    [InlineData(480, 270, 640, 270, 640, 400)]
    // Dragged taller: the width follows the height.
    [InlineData(480, 310, 480, 400, 640, 400)]
    // Dragged by a corner, the width further: the width leads.
    [InlineData(480, 310, 700, 330, 700, 433.75)]
    public void A_resize_keeps_the_picture_at_sixteen_by_nine(
        double fromWidth,
        double fromHeight,
        double toWidth,
        double toHeight,
        double expectedWidth,
        double expectedHeight)
    {
        const double chromeHeight = 40;

        var constrained = PlayerWindowCoordinator.ConstrainToVideoAspect(
            new PlayerWindowGeometry(10, 20, toWidth, toHeight),
            new PlayerWindowGeometry(10, 20, fromWidth, fromHeight),
            PlayerWindowCoordinator.MiniVideoAspect,
            chromeHeight,
            minimumWidth: 320);

        Assert.Equal(expectedWidth, constrained.Width, precision: 6);
        Assert.Equal(expectedHeight, constrained.Height, precision: 6);
        Assert.Equal(
            PlayerWindowCoordinator.MiniVideoAspect,
            constrained.Width / (constrained.Height - chromeHeight),
            precision: 6);

        // The position is carried through untouched: a resize from a north or a west edge moves the
        // window as well, and this is the half that must not undo that.
        Assert.Equal(10, constrained.X);
        Assert.Equal(20, constrained.Y);
    }

    [AvaloniaFact]
    public void A_window_squeezed_under_its_own_minimum_comes_back_in_shape()
    {
        var constrained = PlayerWindowCoordinator.ConstrainToVideoAspect(
            new PlayerWindowGeometry(0, 0, 120, 90),
            new PlayerWindowGeometry(0, 0, 480, 270),
            PlayerWindowCoordinator.MiniVideoAspect,
            chromeHeight: 40,
            minimumWidth: 320);

        Assert.Equal(320, constrained.Width);
        Assert.Equal((320 / PlayerWindowCoordinator.MiniVideoAspect) + 40, constrained.Height, precision: 6);
    }

    [AvaloniaFact]
    public void An_aspect_or_a_minimum_that_no_window_could_have_is_refused()
    {
        var geometry = new PlayerWindowGeometry(0, 0, 480, 270);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerWindowCoordinator.ConstrainToVideoAspect(geometry, geometry, 0, 40, 320));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerWindowCoordinator.ConstrainToVideoAspect(geometry, geometry, 16.0 / 9.0, -1, 320));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerWindowCoordinator.ConstrainToVideoAspect(geometry, geometry, 16.0 / 9.0, 40, 0));
        Assert.Throws<ArgumentNullException>(() =>
            PlayerWindowCoordinator.ConstrainToVideoAspect(null!, geometry, 16.0 / 9.0, 40, 320));
        Assert.Throws<ArgumentNullException>(() =>
            PlayerWindowCoordinator.ConstrainToVideoAspect(geometry, null!, 16.0 / 9.0, 40, 320));
    }

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
            ApSolutions.LocalMedia.Domain.Playback.PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult(new PlaybackSession(Guid.Empty, request.MediaFileId, request.Path));
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
