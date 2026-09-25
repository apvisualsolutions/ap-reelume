// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The chrome goes away by itself after a while without the mouse, the way every player does it
/// (ENG-018).
/// </summary>
/// <remarks>
/// Until 2026-09-25 a single movement of the mouse brought the chrome back for good — until the next
/// pause — and that was written down as a decision: a clock «no test can ask a question of without
/// waiting for it». These tests are the answer to that objection. The clock is a port, the shell is
/// handed one it can move by hand, and not one of them waits for anything.
/// </remarks>
public sealed class ChromeIdleTests
{
    private static readonly MediaFileId MediaFile = new(new Guid("66666666-6666-6666-6666-666666666666"));

    private static readonly Point OverThePicture = new(640, 300);

    [AvaloniaFact]
    public async Task Three_seconds_without_the_mouse_put_the_chrome_away_while_the_film_plays()
    {
        var clock = new ManualClock();
        var (window, viewModel) = await ShowPlayingSessionAsync(clock);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsChromeRevealed);

        clock.Advance(TimeSpan.FromSeconds(2.9));
        Assert.True(viewModel.IsChromeRevealed);

        clock.Advance(TimeSpan.FromSeconds(0.1));
        Assert.False(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Every_movement_starts_the_count_again()
    {
        var clock = new ManualClock();
        var (window, viewModel) = await ShowPlayingSessionAsync(clock);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        window.MouseMove(new Point(OverThePicture.X + 5, OverThePicture.Y), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(viewModel.IsChromeRevealed);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(viewModel.IsChromeRevealed);

        // And once it has gone, the next movement starts a whole new round rather than finding the
        // old one spent.
        window.MouseMove(OverThePicture, RawInputModifiers.None);
        Assert.True(viewModel.IsChromeRevealed);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_paused_film_keeps_its_chrome_however_long_nobody_moves()
    {
        var clock = new ManualClock();
        var (window, viewModel, player) = await ShowPlayingSessionWithPlayerAsync(clock);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        player.ApplySessionState(PlaybackState.Paused, failure: null);
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.True(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task An_open_panel_keeps_the_chrome_because_somebody_is_reading_it()
    {
        var clock = new ManualClock();
        var (window, viewModel) = await ShowPlayingSessionAsync(clock);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Audio);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(viewModel.IsChromeRevealed);

        // Closed again, the next movement brings the clock back.
        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Audio);
        window.MouseMove(OverThePicture, RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_open_gear_keeps_the_chrome_as_well()
    {
        var clock = new ManualClock();
        var (window, viewModel, player) = await ShowPlayingSessionWithPlayerAsync(clock);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        player.Settings!.ToggleCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.True(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_pointer_resting_on_the_controls_keeps_them_and_one_on_the_picture_does_not()
    {
        var clock = new ManualClock();
        var (window, viewModel) = await ShowPlayingSessionAsync(clock);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        window.MouseMove(OverTheTransport(window), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(viewModel.IsChromeRevealed);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task An_infinite_timeout_turns_the_clock_off()
    {
        var clock = new ManualClock();
        var (window, viewModel) = await ShowPlayingSessionAsync(clock);
        viewModel.ChromeIdleTimeout = Timeout.InfiniteTimeSpan;

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        clock.Advance(TimeSpan.FromHours(1));

        Assert.True(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_shell_handed_no_clock_keeps_what_it_revealed()
    {
        var (window, viewModel) = await ShowPlayingSessionAsync(clock: null);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_wait_that_outlives_its_session_decides_nothing_about_the_next_one()
    {
        var clock = new ManualClock();
        var (window, viewModel) = await ShowPlayingSessionAsync(clock);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        await viewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.True(viewModel.IsChromeRevealed);
        window.Close();
    }

    /// <summary>
    /// The pointer goes with the chrome: an arrow left in the middle of a film is the one piece of
    /// chrome nobody can put away.
    /// </summary>
    [AvaloniaFact]
    public async Task The_pointer_hides_over_the_picture_with_the_chrome_and_comes_back_with_it()
    {
        var clock = new ManualClock();
        var (window, viewModel) = await ShowPlayingSessionAsync(clock);
        var stage = window.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "PlayerStage");

        Assert.False(viewModel.IsChromeRevealed);
        Assert.NotNull(stage.Cursor);

        window.MouseMove(OverThePicture, RawInputModifiers.None);
        Assert.Null(stage.Cursor);

        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.NotNull(stage.Cursor);
        window.Close();
    }

    /// <summary>
    /// A point on the transport band, read from where the band is drawn rather than written down.
    /// </summary>
    private static Point OverTheTransport(Window window)
    {
        var band = window.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Name == "TransportControlsSurface");
        var bounds = band.Bounds;
        return band.TranslatePoint(new Point(bounds.Width / 2, bounds.Height / 2), window)!.Value;
    }

    private static async Task<(Window Window, ShellViewModel ViewModel)> ShowPlayingSessionAsync(ManualClock? clock)
    {
        var (window, viewModel, _) = await ShowPlayingSessionWithPlayerAsync(clock);
        return (window, viewModel);
    }

    private static async Task<(Window Window, ShellViewModel ViewModel, PlayerViewModel Player)>
        ShowPlayingSessionWithPlayerAsync(ManualClock? clock)
    {
        var player = new PlayerViewModel(new InertCoordinator(), settings: new PlayerSettingsMenuViewModel());
        var viewModel = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces
            {
                OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(new PlayerSurfaces { Player = player }),
                ClosePlayer = _ => Task.CompletedTask,
                ChangePlaybackMode = (mode, _) => Task.FromResult(mode),
                ChromeClock = clock,
            });
        var window = new Window { Width = 1280, Height = 800, Content = new ShellView { DataContext = viewModel } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        player.ApplySessionState(PlaybackState.Playing, failure: null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsChromeRevealed);
        return (window, viewModel, player);
    }

    /// <summary>
    /// A clock that only moves when the test moves it, and runs whatever was waiting on the way.
    /// </summary>
    private sealed class ManualClock : IClock
    {
        private readonly List<(DateTimeOffset Due, TaskCompletionSource Done)> _waiting = [];

        public DateTimeOffset UtcNow { get; private set; } = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => done.TrySetCanceled(cancellationToken));
            _waiting.Add((UtcNow + delay, done));
            return done.Task;
        }

        public void Advance(TimeSpan by)
        {
            UtcNow += by;
            foreach (var entry in _waiting.Where(entry => entry.Due <= UtcNow).ToArray())
            {
                _waiting.Remove(entry);
                entry.Done.TrySetResult();
            }

            Dispatcher.UIThread.RunJobs();
        }
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
