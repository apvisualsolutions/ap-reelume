// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The five-second promise is only real when the assembly runs the loop that keeps it: the tracker's
/// periodic write has to be started, the pause and seek moments have to flush, and the position
/// handler has to be detached when its session ends. The deep audit found the loop invoked from
/// tests alone (BUG-003) and the handler accumulating once per session (BUG-007).
///
/// <para>
/// The playhead reaches more than the tracker, and each surface it reaches is a wire somebody has to
/// have run. The transport bar is the last of them and was the last to be found: measured on
/// 2026-08-24 by playing a real film, the scrubber and both clocks were <b>absent for the whole
/// session</b> and appeared only after a transport button was pressed, because the view model's state
/// changes on its own commands and on nothing else.
/// </para>
/// </summary>
public sealed class ProgressWiringTests
{
    [Fact]
    public void The_periodic_save_loop_is_started_by_the_assembly()
    {
        // ProgressPolicy.SaveInterval exists so a crash costs five seconds, not a session. The loop
        // that honours it is RunAsync, and only tests were calling it.
        Assert.Contains(".RunAsync(", CompositionRootSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Pausing_flushes_the_position()
    {
        Assert.Contains("PersistenceTrigger.Pause", CompositionRootSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Seeking_flushes_the_position()
    {
        Assert.Contains("PersistenceTrigger.Seek", CompositionRootSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_position_handler_is_detached_when_its_session_ends()
    {
        // The engine is a singleton and the handler captures per-session state: subscribing on every
        // open without the matching unsubscribe stacks one dead session's work on the next.
        Assert.Contains("PositionChanged -=", CompositionRootSource(), StringComparison.Ordinal);
    }

    /// <summary>The bar follows the playhead, which is the only way it can ever show one.</summary>
    /// <remarks>
    /// What the capture showed: a film playing, "Playing" under the buttons, and no bar and no clock
    /// anywhere — <c>HasDuration</c> is false until something puts a duration in the view model, and
    /// <c>OnPositionChanged</c> was handing the engine's position to the tracker and to the skip
    /// offer and to nobody else. Two surfaces in that same handler carry a comment about having been
    /// "reachable and never fed"; this is the third, and the one a person looks at.
    /// </remarks>
    [Fact]
    public void The_transport_bar_follows_the_playhead()
    {
        Assert.Contains("transport.Observe(", CompositionRootSource(), StringComparison.Ordinal);
    }

    /// <summary>What the wire carries: a position and the length that makes a bar possible.</summary>
    [Fact]
    public void Observing_the_playhead_gives_the_bar_its_scale_and_its_clocks()
    {
        var viewModel = new TransportControlsViewModel(new ControlPlayback(new StubEngine()));

        Assert.False(viewModel.HasDuration);

        viewModel.Observe(TimeSpan.FromMinutes(52), TimeSpan.FromMinutes(96));

        Assert.True(viewModel.HasDuration);
        Assert.Equal(5760, viewModel.DurationSeconds);
        Assert.Equal(3120, viewModel.PositionSeconds);
        Assert.Equal("1:36:00", viewModel.DurationLabel);
        Assert.Equal("52:00", viewModel.PositionLabel);
    }

    /// <summary>A tick with no length yet moves the playhead and leaves the bar away.</summary>
    /// <remarks>
    /// The engine reports a position before it reports a length, and a bar whose maximum is unknown
    /// is the state <c>HasDuration</c> exists to keep off the screen. Observing must not invent one.
    /// </remarks>
    [Fact]
    public void Observing_without_a_length_leaves_the_bar_off_the_screen()
    {
        var viewModel = new TransportControlsViewModel(new ControlPlayback(new StubEngine()));

        viewModel.Observe(TimeSpan.FromSeconds(9), duration: null);

        Assert.False(viewModel.HasDuration);
        Assert.Equal(9, viewModel.PositionSeconds);
    }

    private static string CompositionRootSource()
    {
        return CompositionSourceText.Read();
    }

    /// <summary>An engine that answers nothing, which is all a view model's own arithmetic needs.</summary>
    private sealed class StubEngine : IMediaPlayerEngine
    {
        public PlaybackState State => PlaybackState.Idle;

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackFailureEventArgs>? Failure
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(PlaybackState.Idle, TimeSpan.Zero, null, []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
