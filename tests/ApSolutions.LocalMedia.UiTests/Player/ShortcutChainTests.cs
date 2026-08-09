using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The essential keys operating playback through the exact chain the composition root installs:
/// the view resolves the gesture in the shared map, the router executes it once, and the action
/// lands on the session. Before WP-2 every link existed and none of them touched another.
/// </summary>
public sealed class ShortcutChainTests
{
    [AvaloniaFact]
    public void The_essential_keys_operate_the_session_through_the_wired_chain()
    {
        var executed = new List<PlaybackInputCommand>();
        using var router = new InputCommandRouter((command, _) =>
        {
            executed.Add(command);
            return Task.CompletedTask;
        });
        var map = new ShortcutMap();
        var player = new PlayerViewModel(new IdleCoordinator())
        {
            GestureHandler = gesture =>
            {
                if (map.Resolve(gesture) is not { } command)
                {
                    return false;
                }

                _ = router.DispatchAsync(command, InputOrigin.Keyboard, CancellationToken.None);
                return true;
            },
        };
        var window = new Window { Content = new PlayerView { DataContext = player } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyPress(Key.M, RawInputModifiers.None, PhysicalKey.M, null);
        window.KeyPress(Key.F, RawInputModifiers.None, PhysicalKey.F, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            [
                PlaybackInputCommand.PlayPause,
                PlaybackInputCommand.SkipForward,
                PlaybackInputCommand.ToggleMute,
                PlaybackInputCommand.ToggleFullscreen,
            ],
            executed);
    }

    [AvaloniaFact]
    public void A_key_bound_to_nothing_is_left_for_whoever_wanted_it()
    {
        var executed = new List<PlaybackInputCommand>();
        using var router = new InputCommandRouter((command, _) =>
        {
            executed.Add(command);
            return Task.CompletedTask;
        });
        var map = new ShortcutMap();
        var player = new PlayerViewModel(new IdleCoordinator())
        {
            GestureHandler = gesture =>
            {
                if (map.Resolve(gesture) is not { } command)
                {
                    return false;
                }

                _ = router.DispatchAsync(command, InputOrigin.Keyboard, CancellationToken.None);
                return true;
            },
        };
        var window = new Window { Content = new PlayerView { DataContext = player } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Q, RawInputModifiers.None, PhysicalKey.Q, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(executed);
    }

    private sealed class IdleCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path));

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
