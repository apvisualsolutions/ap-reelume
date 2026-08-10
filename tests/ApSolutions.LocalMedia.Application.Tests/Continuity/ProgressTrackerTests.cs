// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Continuity;

/// <summary>
/// The tracker owns every persistence decision: it ticks on an injected clock, keeps only the latest
/// observation between writes, writes on each critical trigger, and never performs input or output on
/// the thread that observes the engine.
/// </summary>
public sealed class ProgressTrackerTests
{
    private static readonly ContentKey Content =
        ContentKey.ForTitle(new TitleId(Guid.Parse("7c9a0001-0000-4000-8000-000000000001")));

    private static readonly MediaFileId Source = new(Guid.Parse("7c9a0001-0000-4000-8000-0000000000f1"));

    [Fact]
    public async Task The_loop_waits_exactly_the_approved_interval()
    {
        var repository = new RecordingWatchStateRepository();
        var clock = new ManualClock();
        await using var tracker = new PlaybackProgressTracker(repository, clock);
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        var loop = tracker.RunAsync(cancellation.Token);
        await clock.WaitForDelayAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        clock.ReleaseAll();
        await loop;

        Assert.Equal(ProgressPolicy.SaveInterval, clock.Delays[0]);
    }

    [Fact]
    public async Task A_tick_persists_the_latest_observation_only_once()
    {
        var repository = new RecordingWatchStateRepository();
        var clock = new ManualClock();
        await using var tracker = new PlaybackProgressTracker(repository, clock);
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var loop = tracker.RunAsync(cancellation.Token);

        await clock.WaitForDelayAsync(TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(50));
        tracker.Observe(TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(50));
        tracker.Observe(TimeSpan.FromSeconds(35), TimeSpan.FromMinutes(50));
        await clock.AdvanceAsync(TestContext.Current.CancellationToken);
        await repository.WaitForWritesAsync(1, TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        clock.ReleaseAll();
        await loop;

        var written = Assert.Single(repository.Writes);
        Assert.Equal(TimeSpan.FromSeconds(35), written.Position);
        Assert.Equal(TimeSpan.FromMinutes(50), written.ObservedDuration);
        Assert.Equal(Source, written.SourceMediaFileId);
    }

    [Fact]
    public async Task A_tick_that_repeats_the_stored_position_writes_nothing()
    {
        var repository = new RecordingWatchStateRepository();
        var clock = new ManualClock();
        await using var tracker = new PlaybackProgressTracker(repository, clock);
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(50));

        Assert.True(await tracker.FlushAsync(PersistenceTrigger.Tick, TestContext.Current.CancellationToken));
        Assert.False(await tracker.FlushAsync(PersistenceTrigger.Tick, TestContext.Current.CancellationToken));
        Assert.Equal(1, tracker.WriteCount);
    }

    [Theory]
    [InlineData(PersistenceTrigger.Pause)]
    [InlineData(PersistenceTrigger.Seek)]
    [InlineData(PersistenceTrigger.ModeChange)]
    [InlineData(PersistenceTrigger.FileChange)]
    [InlineData(PersistenceTrigger.Close)]
    [InlineData(PersistenceTrigger.EngineFailure)]
    public async Task Every_critical_trigger_writes_even_when_the_position_did_not_move(
        PersistenceTrigger trigger)
    {
        var repository = new RecordingWatchStateRepository();
        await using var tracker = new PlaybackProgressTracker(repository, new ManualClock());
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(50));

        Assert.True(await tracker.FlushAsync(trigger, TestContext.Current.CancellationToken));
        Assert.True(await tracker.FlushAsync(trigger, TestContext.Current.CancellationToken));
        Assert.Equal(2, tracker.WriteCount);
    }

    [Fact]
    public async Task Observing_a_position_performs_no_repository_work()
    {
        var repository = new RecordingWatchStateRepository();
        await using var tracker = new PlaybackProgressTracker(repository, new ManualClock());
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        repository.ResetCounters();

        for (var index = 0; index < 1_000; index++)
        {
            tracker.Observe(TimeSpan.FromSeconds(index), TimeSpan.FromMinutes(50));
        }

        Assert.Equal(0, repository.CallCount);
        Assert.Equal(0, tracker.WriteCount);
    }

    [Fact]
    public async Task A_tracker_without_a_session_writes_nothing_and_refuses_use_after_disposal()
    {
        var repository = new RecordingWatchStateRepository();
        var tracker = new PlaybackProgressTracker(repository, new ManualClock());

        Assert.False(await tracker.FlushAsync(PersistenceTrigger.Close, TestContext.Current.CancellationToken));
        Assert.Equal(0, tracker.WriteCount);

        await tracker.DisposeAsync();
        await tracker.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => tracker.FlushAsync(PersistenceTrigger.Close, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_written_position_is_clamped_into_the_observed_duration()
    {
        var repository = new RecordingWatchStateRepository();
        await using var tracker = new PlaybackProgressTracker(repository, new ManualClock());
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);

        tracker.Observe(TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(50));
        _ = await tracker.FlushAsync(PersistenceTrigger.Seek, TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromSeconds(-40), TimeSpan.FromMinutes(50));
        _ = await tracker.FlushAsync(PersistenceTrigger.Seek, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(50), repository.Writes[0].Position);
        Assert.Equal(TimeSpan.Zero, repository.Writes[1].Position);
    }

    [Fact]
    public async Task A_slow_repository_cannot_hold_a_critical_flush_open()
    {
        var repository = new RecordingWatchStateRepository
        {
            SaveDelay = TimeSpan.FromSeconds(30),
        };
        await using var tracker = new PlaybackProgressTracker(
            repository,
            new ManualClock(),
            criticalFlushTimeout: TimeSpan.FromMilliseconds(150));
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(50));

        var started = DateTimeOffset.UtcNow;
        var written = await tracker.FlushAsync(PersistenceTrigger.Close, TestContext.Current.CancellationToken);

        Assert.False(written);
        Assert.Equal(1, tracker.TimedOutFlushes);
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Concurrent_flushes_are_serialised_and_end_on_the_last_observation()
    {
        var repository = new RecordingWatchStateRepository();
        await using var tracker = new PlaybackProgressTracker(repository, new ManualClock());
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);

        var flushes = new List<Task>();
        for (var index = 1; index <= 50; index++)
        {
            tracker.Observe(TimeSpan.FromSeconds(index), TimeSpan.FromMinutes(50));
            flushes.Add(tracker.FlushAsync(PersistenceTrigger.Seek, TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(flushes);
        tracker.Observe(TimeSpan.FromSeconds(51), TimeSpan.FromMinutes(50));
        _ = await tracker.FlushAsync(PersistenceTrigger.Close, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromSeconds(51), repository.Writes[^1].Position);
        Assert.False(repository.ObservedOverlap);
    }

    [Fact]
    public async Task A_resumed_session_keeps_its_first_start_time_and_records_the_source_version()
    {
        var repository = new RecordingWatchStateRepository();
        var firstStart = new DateTimeOffset(2026, 7, 30, 21, 0, 0, TimeSpan.Zero);
        await repository.SaveAsync(
            new WatchState
            {
                Content = Content,
                Position = TimeSpan.FromMinutes(5),
                ObservedDuration = TimeSpan.FromMinutes(50),
                SourceMediaFileId = new MediaFileId(Guid.Parse("7c9a0001-0000-4000-8000-0000000000f0")),
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = firstStart,
                UpdatedUtc = firstStart,
            },
            TestContext.Current.CancellationToken);
        var clock = new ManualClock();
        await using var tracker = new PlaybackProgressTracker(repository, clock);

        var existing = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(50));
        _ = await tracker.FlushAsync(PersistenceTrigger.Pause, TestContext.Current.CancellationToken);

        Assert.NotNull(existing);
        Assert.Equal(TimeSpan.FromMinutes(5), existing!.Position);
        var written = repository.Writes[^1];
        Assert.Equal(firstStart, written.StartedUtc);
        Assert.Equal(clock.UtcNow, written.UpdatedUtc);
        Assert.Equal(Source, written.SourceMediaFileId);
    }

    [Fact]
    public async Task A_manual_override_already_stored_is_never_cleared_by_progress()
    {
        var repository = new RecordingWatchStateRepository();
        await repository.SaveAsync(
            new WatchState
            {
                Content = Content,
                Position = TimeSpan.FromMinutes(5),
                ObservedDuration = TimeSpan.FromMinutes(50),
                SourceMediaFileId = Source,
                Status = WatchStatus.Watched,
                IsManualOverride = true,
                StartedUtc = DateTimeOffset.UnixEpoch,
                UpdatedUtc = DateTimeOffset.UnixEpoch,
            },
            TestContext.Current.CancellationToken);
        await using var tracker = new PlaybackProgressTracker(repository, new ManualClock());
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);

        tracker.Observe(TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(50));
        _ = await tracker.FlushAsync(PersistenceTrigger.Pause, TestContext.Current.CancellationToken);

        Assert.True(repository.Writes[^1].IsManualOverride);
        Assert.Equal(WatchStatus.Watched, repository.Writes[^1].Status);
    }

    [Fact]
    public async Task A_stored_position_past_the_minimum_offers_a_resume_and_a_trivial_one_restarts()
    {
        var repository = new RecordingWatchStateRepository();
        var resume = new ResumePlayback(repository);
        var trivial = ContentKey.ForTitle(new TitleId(Guid.Parse("7c9a0002-0000-4000-8000-000000000002")));
        await repository.SaveAsync(Stored(Content, TimeSpan.FromMinutes(12)), TestContext.Current.CancellationToken);
        await repository.SaveAsync(Stored(trivial, TimeSpan.FromSeconds(12)), TestContext.Current.CancellationToken);

        var offered = await resume.DecideAsync(
            Content,
            TimeSpan.FromMinutes(50),
            TestContext.Current.CancellationToken);
        var restarted = await resume.DecideAsync(
            trivial,
            TimeSpan.FromMinutes(50),
            TestContext.Current.CancellationToken);
        var unknown = await resume.DecideAsync(
            ContentKey.ForTitle(new TitleId(Guid.NewGuid())),
            TimeSpan.FromMinutes(50),
            TestContext.Current.CancellationToken);

        Assert.Equal(ResumeChoice.Resume, offered.Choice);
        Assert.Equal(TimeSpan.FromMinutes(12), offered.Position);
        Assert.Equal(ResumeChoice.Restart, restarted.Choice);
        Assert.Equal(TimeSpan.Zero, restarted.Position);
        Assert.Equal(ResumeChoice.Restart, unknown.Choice);
    }

    private static WatchState Stored(ContentKey content, TimeSpan position) => new()
    {
        Content = content,
        Position = position,
        ObservedDuration = TimeSpan.FromMinutes(50),
        SourceMediaFileId = Source,
        Status = WatchStatus.InProgress,
        IsManualOverride = false,
        StartedUtc = DateTimeOffset.UnixEpoch,
        UpdatedUtc = DateTimeOffset.UnixEpoch,
    };

    private sealed class ManualClock : IClock
    {
        private readonly Lock _sync = new();
        private readonly List<TimeSpan> _delays = [];
        private TaskCompletionSource? _pending;
        private TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DateTimeOffset UtcNow { get; private set; } =
            new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

        public IReadOnlyList<TimeSpan> Delays
        {
            get
            {
                lock (_sync)
                {
                    return [.. _delays];
                }
            }
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource requested;
            lock (_sync)
            {
                _delays.Add(delay);
                _pending = source;
                requested = _requested;
            }

            _ = cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));
            requested.TrySetResult();
            return source.Task;
        }

        /// <summary>Waits until the loop parks on its next delay without releasing it.</summary>
        public async Task WaitForDelayAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource requested;
            lock (_sync)
            {
                requested = _requested;
            }

            await requested.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }

        /// <summary>Releases the parked delay and moves the clock forward by exactly that delay.</summary>
        public async Task AdvanceAsync(CancellationToken cancellationToken)
        {
            await WaitForDelayAsync(cancellationToken);
            TaskCompletionSource? pending;
            lock (_sync)
            {
                pending = _pending;
                _pending = null;
                _requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                UtcNow += _delays[^1];
            }

            _ = pending?.TrySetResult();
        }

        public void ReleaseAll()
        {
            TaskCompletionSource? pending;
            lock (_sync)
            {
                pending = _pending;
                _pending = null;
            }

            _ = pending?.TrySetResult();
        }
    }

    private sealed class RecordingWatchStateRepository : IWatchStateRepository
    {
        private readonly Lock _sync = new();
        private readonly Dictionary<string, WatchState> _stored = [];
        private readonly List<WatchState> _writes = [];
        private TaskCompletionSource _written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;
        private int _inFlight;

        public TimeSpan? SaveDelay { get; init; }

        public bool ObservedOverlap { get; private set; }

        public int CallCount => Volatile.Read(ref _callCount);

        public IReadOnlyList<WatchState> Writes
        {
            get
            {
                lock (_sync)
                {
                    return [.. _writes];
                }
            }
        }

        public Task<WatchState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _callCount);
            lock (_sync)
            {
                return Task.FromResult(_stored.TryGetValue(content.Value, out var state) ? state : null);
            }
        }

        public Task<IReadOnlyList<WatchState>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _callCount);
            lock (_sync)
            {
                return Task.FromResult<IReadOnlyList<WatchState>>([.. _stored.Values]);
            }
        }

        public async Task SaveAsync(WatchState state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            _ = Interlocked.Increment(ref _callCount);
            if (Interlocked.Increment(ref _inFlight) > 1)
            {
                ObservedOverlap = true;
            }

            try
            {
                if (SaveDelay is { } delay)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                TaskCompletionSource written;
                lock (_sync)
                {
                    _stored[state.Content.Value] = state;
                    _writes.Add(state);
                    written = _written;
                    _written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                written.TrySetResult();
            }
            finally
            {
                _ = Interlocked.Decrement(ref _inFlight);
            }
        }

        public void ResetCounters() => Volatile.Write(ref _callCount, 0);

        public async Task WaitForWritesAsync(int count, CancellationToken cancellationToken)
        {
            while (true)
            {
                TaskCompletionSource written;
                lock (_sync)
                {
                    if (_writes.Count >= count)
                    {
                        return;
                    }

                    written = _written;
                }

                await written.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
