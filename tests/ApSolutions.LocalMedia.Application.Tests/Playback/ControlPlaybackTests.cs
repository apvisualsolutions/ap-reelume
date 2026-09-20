// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Playback;

/// <summary>
/// Transport commands are serialised against the active session and clamped by the domain policies,
/// so the engine only ever receives values inside its contract.
/// </summary>
public sealed class ControlPlaybackTests
{
    [Fact]
    public async Task Skips_use_the_configured_intervals_and_land_exactly_on_both_boundaries()
    {
        var engine = new RecordingEngine { Position = TimeSpan.FromMinutes(5) };
        using var control = new ControlPlayback(engine);

        var forward = await control.SkipForwardAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromMinutes(5) + PlaybackControlPolicy.DefaultForwardSkip, forward.Position);

        var backward = await control.SkipBackwardAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(20), backward.Position);

        engine.Position = TimeSpan.FromSeconds(4);
        var atStart = await control.SkipBackwardAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.Zero, atStart.Position);

        engine.Position = engine.Duration - TimeSpan.FromSeconds(3);
        var atEnd = await control.SkipForwardAsync(TestContext.Current.CancellationToken);
        Assert.Equal(engine.Duration, atEnd.Position);
    }

    [Fact]
    public async Task Skip_intervals_are_configurable_and_always_positive()
    {
        var engine = new RecordingEngine { Position = TimeSpan.FromMinutes(2) };
        using var control = new ControlPlayback(engine);

        var configured = await control.ConfigureSkipsAsync(
            TimeSpan.FromSeconds(-90),
            TimeSpan.FromHours(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(PlaybackControlPolicy.MinimumSkipInterval, configured.BackwardSkip);
        Assert.Equal(PlaybackControlPolicy.MaximumSkipInterval, configured.ForwardSkip);

        var usable = await control.ConfigureSkipsAsync(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(5), usable.BackwardSkip);
        Assert.Equal(TimeSpan.FromSeconds(45), usable.ForwardSkip);

        var moved = await control.SkipForwardAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(45), moved.Position);
    }

    [Fact]
    public async Task Seeking_is_clamped_into_the_observed_duration()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);

        var overrun = await control.SeekAsync(TimeSpan.FromHours(1), TestContext.Current.CancellationToken);
        Assert.Equal(engine.Duration, overrun.Position);

        var underrun = await control.SeekAsync(TimeSpan.FromMinutes(-5), TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.Zero, underrun.Position);
    }

    /// <summary>
    /// ENG-011: the other end of it. The speed reached the engine and stopped there, so closing the
    /// application forgot it and the person had to choose it again every time. What is handed over
    /// is the CLAMPED value, for the same reason the seek hands its clamped target: what gets stored
    /// has to be what the engine was actually told, never what was asked for.
    /// </summary>
    [Fact]
    public async Task Every_speed_change_hands_its_clamped_value_to_the_persistence_callback()
    {
        var engine = new RecordingEngine();
        var persisted = new List<double>();
        using var control = new ControlPlayback(
            engine,
            persistSpeed: (speed, _) =>
            {
                persisted.Add(speed);
                return Task.CompletedTask;
            });

        _ = await control.SetSpeedAsync(1.5, TestContext.Current.CancellationToken);
        _ = await control.SetSpeedAsync(99.0, TestContext.Current.CancellationToken);

        Assert.Equal(2, persisted.Count);
        Assert.Equal(1.5, persisted[0]);
        Assert.Equal(PlaybackControlPolicy.ClampSpeed(99.0), persisted[1]);
    }

    /// <summary>
    /// Opening a file resolves a speed and hands it to the engine; the transport has to take the
    /// same value or the screen reads one thing while the film runs at another.
    /// </summary>
    [Fact]
    public async Task Adopting_the_resolved_speed_makes_the_transport_agree_with_the_engine()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);

        var state = await control.AdoptSpeedAsync(1.5, TestContext.Current.CancellationToken);

        Assert.Equal(1.5, state.SpeedMultiplier);
    }

    /// <summary>
    /// Adopting is not choosing: the stored value arriving must not be written back as if somebody
    /// had just picked it. The negative control of the persistence test above.
    /// </summary>
    [Fact]
    public async Task Adopting_a_speed_stores_nothing_because_nobody_chose_it()
    {
        var engine = new RecordingEngine();
        var persisted = new List<double>();
        using var control = new ControlPlayback(
            engine,
            persistSpeed: (speed, _) =>
            {
                persisted.Add(speed);
                return Task.CompletedTask;
            });

        _ = await control.AdoptSpeedAsync(1.5, TestContext.Current.CancellationToken);

        Assert.Empty(persisted);
    }

    /// <summary>A stored value out of range is brought back, exactly like one somebody types.</summary>
    [Fact]
    public async Task An_adopted_speed_out_of_range_is_brought_back_into_it()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);

        var state = await control.AdoptSpeedAsync(99.0, TestContext.Current.CancellationToken);

        Assert.Equal(PlaybackControlPolicy.ClampSpeed(99.0), state.SpeedMultiplier);
    }

    /// <summary>Nothing to store is the ordinary case for a caller that only drives the engine.</summary>
    [Fact]
    public async Task A_speed_change_with_nowhere_to_store_it_still_reaches_the_engine()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);

        var state = await control.SetSpeedAsync(1.5, TestContext.Current.CancellationToken);

        Assert.Equal(1.5, state.SpeedMultiplier);
    }

    [Fact]
    public async Task Every_seek_hands_its_clamped_target_to_the_persistence_callback()
    {
        var engine = new RecordingEngine();
        var persisted = new List<TimeSpan>();
        using var control = new ControlPlayback(engine, (position, _, _) =>
        {
            persisted.Add(position);
            return Task.CompletedTask;
        });

        _ = await control.SeekAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
        _ = await control.SeekAsync(TimeSpan.FromHours(1), TestContext.Current.CancellationToken);
        _ = await control.SkipForwardAsync(TestContext.Current.CancellationToken);

        // The callback sees what the engine was told, clamped included: a crash right after any of
        // these must resume at the point the person actually reached.
        Assert.Equal(3, persisted.Count);
        Assert.Equal(TimeSpan.FromMinutes(2), persisted[0]);
        Assert.Equal(engine.Duration, persisted[1]);
    }

    [Fact]
    public async Task Boost_always_reaches_the_engine_together_with_its_limiter()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);

        var normal = await control.SetVolumeAsync(80, TestContext.Current.CancellationToken);
        Assert.False(normal.Volume.IsBoosted);
        Assert.False(engine.LastVolume!.LimiterEngaged);

        var boosted = await control.SetVolumeAsync(175, TestContext.Current.CancellationToken);
        Assert.True(boosted.Volume.IsBoosted);
        Assert.True(boosted.Volume.RequiresWarning);
        Assert.True(engine.LastVolume!.LimiterEngaged);

        var clamped = await control.SetVolumeAsync(4000, TestContext.Current.CancellationToken);
        Assert.Equal(VolumeBoostPolicy.MaximumBoostPercent, clamped.Volume.Percent);
    }

    [Fact]
    public async Task Muting_is_reversible_and_keeps_the_chosen_level()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);
        _ = await control.SetVolumeAsync(160, TestContext.Current.CancellationToken);

        var muted = await control.ToggleMuteAsync(TestContext.Current.CancellationToken);
        Assert.True(muted.Volume.IsMuted);
        Assert.Equal(160, muted.Volume.Percent);
        Assert.Equal(0, engine.LastVolume!.LinearGain);

        var restored = await control.ToggleMuteAsync(TestContext.Current.CancellationToken);
        Assert.False(restored.Volume.IsMuted);
        Assert.Equal(160, restored.Volume.Percent);
        Assert.Equal(1.6, engine.LastVolume!.LinearGain, 6);
    }

    [Fact]
    public async Task Speed_is_clamped_before_the_engine_ever_sees_it()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);

        _ = await control.SetSpeedAsync(12.0, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackControlPolicy.MaximumSpeed, engine.LastSpeed);

        var slow = await control.SetSpeedAsync(0.01, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackControlPolicy.MinimumSpeed, engine.LastSpeed);
        Assert.Equal(PlaybackControlPolicy.MinimumSpeed, slow.SpeedMultiplier);
    }

    [Fact]
    public async Task A_hundred_concurrent_commands_never_overlap_and_end_deterministically()
    {
        var engine = new RecordingEngine();
        using var control = new ControlPlayback(engine);

        var commands = Enumerable.Range(0, 100).Select(index => index % 2 == 0
            ? control.SetSpeedAsync(2.0, TestContext.Current.CancellationToken)
            : control.SetVolumeAsync(120, TestContext.Current.CancellationToken));
        _ = await Task.WhenAll(commands);

        var final = await control.SetSpeedAsync(1.0, TestContext.Current.CancellationToken);
        Assert.Equal(1, engine.MaxConcurrentCommands);
        Assert.Equal(1.0, final.SpeedMultiplier);
        Assert.Equal(120, final.Volume.Percent);
    }

    [Fact]
    public void The_use_case_refuses_to_exist_without_an_engine() =>
        Assert.Throws<ArgumentNullException>(() => new ControlPlayback(null!));

    private sealed class RecordingEngine : IMediaPlayerEngine
    {
        private int _active;

#pragma warning disable CS0067 // The contract declares these events; this double never raises them.
        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

        public event EventHandler<PlaybackFailureEventArgs>? Failure;
#pragma warning restore CS0067

        public PlaybackState State => PlaybackState.Playing;

        public TimeSpan Position { get; set; } = TimeSpan.FromMinutes(1);

        public TimeSpan Duration { get; } = TimeSpan.FromMinutes(10);

        public double LastSpeed { get; private set; } = 1.0;

        public VolumeDecision? LastVolume { get; private set; }

        public int MaxConcurrentCommands { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            TrackAsync(() => Position = position);

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(State, Position, Duration, []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle, IsExternal: true));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            TrackAsync(() => LastSpeed = multiplier);
        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            TrackAsync(() =>
            {
                Assert.False(decision.IsBoosted && !decision.LimiterEngaged);
                LastVolume = decision;
            });

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task TrackAsync(Action action)
        {
            MaxConcurrentCommands = Math.Max(MaxConcurrentCommands, Interlocked.Increment(ref _active));
            try
            {
                await Task.Yield();
                action();
            }
            finally
            {
                _ = Interlocked.Decrement(ref _active);
            }
        }
    }
}
