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
