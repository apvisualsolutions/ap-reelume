// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Playback;

/// <summary>
/// Moving between window modes must never restart the media: the engine is not reopened, the
/// position is carried across, and exactly one mode is in force at any moment.
/// </summary>
public sealed class PlaybackModeTests
{
    [Fact]
    public async Task Changing_mode_preserves_the_position_and_never_reopens_the_engine()
    {
        var engine = new CountingEngine { Position = TimeSpan.FromMinutes(3) };
        var change = new ChangePlaybackMode(engine);

        var toFullscreen = await change.ExecuteAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        var toMini = await change.ExecuteAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        var back = await change.ExecuteAsync(PlaybackMode.Embedded, TestContext.Current.CancellationToken);

        Assert.Equal(PlaybackMode.Embedded, toFullscreen.From);
        Assert.Equal(PlaybackMode.Fullscreen, toFullscreen.To);
        Assert.Equal(PlaybackMode.Mini, toMini.To);
        Assert.Equal(PlaybackMode.Embedded, back.To);
        Assert.All(
            new[] { toFullscreen, toMini, back },
            transition => Assert.Equal(TimeSpan.FromMinutes(3), transition.Position));
        Assert.Equal(0, engine.OpenCount);
        Assert.Equal(0, engine.StopCount);
    }

    [Fact]
    public async Task Only_one_mode_is_ever_in_force()
    {
        var change = new ChangePlaybackMode(new CountingEngine());

        Assert.Equal(PlaybackMode.Embedded, change.Current);
        _ = await change.ExecuteAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Fullscreen, change.Current);
        _ = await change.ExecuteAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Mini, change.Current);
    }

    [Fact]
    public async Task Asking_for_the_current_mode_is_not_a_transition()
    {
        var change = new ChangePlaybackMode(new CountingEngine());

        _ = await change.ExecuteAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        var repeat = await change.ExecuteAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);

        Assert.Equal(PlaybackMode.Fullscreen, repeat.From);
        Assert.Equal(PlaybackMode.Fullscreen, repeat.To);
        Assert.Equal(1, change.TransitionCount);
    }

    [Fact]
    public async Task A_toggle_returns_to_embedded_from_the_mode_it_toggles()
    {
        var change = new ChangePlaybackMode(new CountingEngine());

        _ = await change.ToggleAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Fullscreen, change.Current);
        _ = await change.ToggleAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Embedded, change.Current);

        _ = await change.ToggleAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Mini, change.Current);
    }

    [Fact]
    public async Task A_hundred_mode_changes_keep_one_session_and_one_position()
    {
        var engine = new CountingEngine { Position = TimeSpan.FromMinutes(7) };
        var change = new ChangePlaybackMode(engine);
        var modes = new[] { PlaybackMode.Fullscreen, PlaybackMode.Mini, PlaybackMode.Embedded };

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var transition = await change.ExecuteAsync(
                modes[iteration % modes.Length],
                TestContext.Current.CancellationToken);
            Assert.Equal(TimeSpan.FromMinutes(7), transition.Position);
        }

        Assert.Equal(100, change.TransitionCount);
        Assert.Equal(0, engine.OpenCount);
        Assert.Equal(0, engine.StopCount);
        Assert.Equal(0, engine.DisposeCount);
    }

    [Fact]
    public async Task An_unknown_mode_is_refused_and_a_missing_engine_is_refused_at_construction()
    {
        var change = new ChangePlaybackMode(new CountingEngine());

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => change.ExecuteAsync((PlaybackMode)99, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => new ChangePlaybackMode(null!));
    }

    [Theory]
    [InlineData(InputOrigin.Keyboard, InputOrigin.MediaKey)]
    [InlineData(InputOrigin.MediaKey, InputOrigin.Keyboard)]
    [InlineData(InputOrigin.Mouse, InputOrigin.Keyboard)]
    public async Task The_same_command_from_two_origins_runs_once(InputOrigin first, InputOrigin second)
    {
        var executed = new List<PlaybackInputCommand>();
        using var router = new InputCommandRouter((command, _) =>
        {
            executed.Add(command);
            return Task.CompletedTask;
        });

        var accepted = await router.DispatchAsync(
            PlaybackInputCommand.PlayPause,
            first,
            TestContext.Current.CancellationToken);
        var duplicate = await router.DispatchAsync(
            PlaybackInputCommand.PlayPause,
            second,
            TestContext.Current.CancellationToken);

        Assert.True(accepted);
        Assert.False(duplicate);
        Assert.Equal([PlaybackInputCommand.PlayPause], executed);
        Assert.Contains(second, router.Suppressed);
    }

    [Fact]
    public async Task Different_commands_are_never_coalesced_with_each_other()
    {
        using var router = new InputCommandRouter((_, _) => Task.CompletedTask);

        Assert.True(await router.DispatchAsync(
            PlaybackInputCommand.SkipForward,
            InputOrigin.Keyboard,
            TestContext.Current.CancellationToken));
        Assert.True(await router.DispatchAsync(
            PlaybackInputCommand.SkipBackward,
            InputOrigin.Keyboard,
            TestContext.Current.CancellationToken));
        Assert.True(await router.DispatchAsync(
            PlaybackInputCommand.ToggleMute,
            InputOrigin.MediaKey,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                PlaybackInputCommand.SkipForward,
                PlaybackInputCommand.SkipBackward,
                PlaybackInputCommand.ToggleMute,
            ],
            router.Executed);
    }

    [Fact]
    public async Task After_the_coalescing_window_the_same_command_runs_again()
    {
        using var router = new InputCommandRouter(
            (_, _) => Task.CompletedTask,
            TimeSpan.FromMilliseconds(20));

        Assert.True(await router.DispatchAsync(
            PlaybackInputCommand.PlayPause,
            InputOrigin.Keyboard,
            TestContext.Current.CancellationToken));
        await Task.Delay(60, TestContext.Current.CancellationToken);
        Assert.True(await router.DispatchAsync(
            PlaybackInputCommand.PlayPause,
            InputOrigin.Keyboard,
            TestContext.Current.CancellationToken));

        Assert.Equal(2, router.Executed.Count);
        Assert.Throws<ArgumentNullException>(() => new InputCommandRouter(null!));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => router.DispatchAsync(
            (PlaybackInputCommand)99,
            InputOrigin.Keyboard,
            TestContext.Current.CancellationToken));
    }

    private sealed class CountingEngine : IMediaPlayerEngine
    {
#pragma warning disable CS0067 // The contract declares these events; this double never raises them.
        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

        public event EventHandler<PlaybackFailureEventArgs>? Failure;
#pragma warning restore CS0067

        public PlaybackState State => PlaybackState.Playing;

        public TimeSpan Position { get; init; } = TimeSpan.Zero;

        public int OpenCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.CompletedTask;
        }

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(State, Position, TimeSpan.FromMinutes(30), []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle, IsExternal: true));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
