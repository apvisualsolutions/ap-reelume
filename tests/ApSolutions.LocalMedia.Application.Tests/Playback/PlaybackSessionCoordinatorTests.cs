using System.Collections.Concurrent;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Playback;

public sealed class PlaybackSessionCoordinatorTests
{
    [Fact]
    public async Task Session_walks_the_full_lifecycle_and_publishes_every_state()
    {
        var engine = new FakeMediaPlayerEngine();
        var publisher = new RecordingPublisher();
        await using var coordinator = new PlaybackSessionCoordinator(engine, publisher);
        var request = Request(@"C:\Media\episode.mkv");

        var session = await coordinator.StartAsync(request, TestContext.Current.CancellationToken);
        await coordinator.PauseAsync(TestContext.Current.CancellationToken);
        await coordinator.ResumeAsync(TestContext.Current.CancellationToken);
        await coordinator.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(request.MediaFileId, session.MediaFileId);
        Assert.Equal(request.Path, session.Path);
        Assert.Null(coordinator.ActiveSession);
        Assert.Equal(1, engine.InitializeCount);
        Assert.Equal(0, engine.LiveNativeHandles);
        Assert.Equal(
            [
                PlaybackState.Opening,
                PlaybackState.Playing,
                PlaybackState.Paused,
                PlaybackState.Playing,
                PlaybackState.Stopped,
            ],
            publisher.States);
        Assert.All(publisher.Sessions, published => Assert.Equal(session.Id, published));
    }

    [Fact]
    public async Task Cancelling_while_opening_releases_the_engine_and_leaves_no_active_session()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new FakeMediaPlayerEngine { OpenGate = gate.Task };
        var publisher = new RecordingPublisher();
        await using var coordinator = new PlaybackSessionCoordinator(engine, publisher);
        using var cancellation = new CancellationTokenSource();

        var start = coordinator.StartAsync(Request(@"C:\Media\slow.mkv"), cancellation.Token);
        await engine.WaitForOpeningAsync();
        await cancellation.CancelAsync();
        gate.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        Assert.Null(coordinator.ActiveSession);
        Assert.Equal(0, engine.LiveNativeHandles);
        Assert.Equal(1, engine.StopCount);
        Assert.DoesNotContain(PlaybackState.Playing, publisher.States);
    }

    [Fact]
    public async Task A_failed_open_releases_resources_and_reports_an_actionable_failure()
    {
        var engine = new FakeMediaPlayerEngine
        {
            OpenFailure = new PlaybackFailure(PlaybackFailureCode.FileNotFound, "missing.mkv"),
        };
        var publisher = new RecordingPublisher();
        await using var coordinator = new PlaybackSessionCoordinator(engine, publisher);

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(
            () => coordinator.StartAsync(Request(@"C:\Media\missing.mkv"), TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.FileNotFound, failure.Failure.Code);
        Assert.Null(coordinator.ActiveSession);
        Assert.Equal(0, engine.LiveNativeHandles);
        Assert.Equal(1, engine.StopCount);
        Assert.Equal([PlaybackState.Opening, PlaybackState.Failed], publisher.States);
        Assert.Equal(
            PlaybackFailureCode.FileNotFound,
            Assert.Single(publisher.Events, published => published.Failure is not null).Failure!.Code);
    }

    [Fact]
    public async Task Starting_a_second_media_stops_the_first_and_keeps_exactly_one_active_session()
    {
        var engine = new FakeMediaPlayerEngine();
        var publisher = new RecordingPublisher();
        await using var coordinator = new PlaybackSessionCoordinator(engine, publisher);

        var first = await coordinator.StartAsync(Request(@"C:\Media\first.mkv"), TestContext.Current.CancellationToken);
        var second = await coordinator.StartAsync(Request(@"C:\Media\second.mkv"), TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.Id, coordinator.ActiveSession?.Id);
        Assert.Equal(1, engine.InitializeCount);
        Assert.Equal(1, engine.MaxConcurrentOpenMedia);
        Assert.Equal(1, engine.LiveNativeHandles);
        Assert.Equal(@"C:\Media\second.mkv", engine.LastPath);
        Assert.Equal(
            [
                PlaybackState.Opening,
                PlaybackState.Playing,
                PlaybackState.Stopped,
                PlaybackState.Opening,
                PlaybackState.Playing,
            ],
            publisher.States);
    }

    [Fact]
    public async Task Forced_disposal_stops_the_active_session_and_releases_every_handle()
    {
        var engine = new FakeMediaPlayerEngine();
        var publisher = new RecordingPublisher();
        var coordinator = new PlaybackSessionCoordinator(engine, publisher);
        await coordinator.StartAsync(Request(@"C:\Media\playing.mkv"), TestContext.Current.CancellationToken);

        await coordinator.DisposeAsync();

        Assert.Null(coordinator.ActiveSession);
        Assert.Equal(0, engine.LiveNativeHandles);
        Assert.Equal(1, engine.StopCount);
        Assert.Equal(1, engine.DisposeCount);
        Assert.Equal(PlaybackState.Stopped, publisher.States[^1]);
    }

    [Fact]
    public async Task Use_cases_route_start_and_stop_through_the_single_session_coordinator()
    {
        var engine = new FakeMediaPlayerEngine();
        var publisher = new RecordingPublisher();
        await using var coordinator = new PlaybackSessionCoordinator(engine, publisher);
        var start = new StartPlayback(coordinator);
        var stop = new StopPlayback(coordinator);

        var session = await start.ExecuteAsync(Request(@"C:\Media\route.mkv"), TestContext.Current.CancellationToken);
        Assert.Equal(session.Id, coordinator.ActiveSession?.Id);

        await stop.ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.Null(coordinator.ActiveSession);
    }

    private static PlaybackRequest Request(string path) =>
        new(new MediaFileId(Guid.NewGuid()), path);

    private sealed class RecordingPublisher : IApplicationEventPublisher
    {
        private readonly List<PlaybackSessionChanged> _events = [];

        public IReadOnlyList<PlaybackSessionChanged> Events => _events;

        public IReadOnlyList<PlaybackState> States => _events.Select(published => published.State).ToArray();

        public IReadOnlyList<Guid> Sessions => _events.Select(published => published.SessionId).ToArray();

        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            if (applicationEvent is PlaybackSessionChanged sessionChanged)
            {
                _events.Add(sessionChanged);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeMediaPlayerEngine : IMediaPlayerEngine
    {
        private readonly ConcurrentQueue<TaskCompletionSource> _openingWaiters = new();
        private int _liveNativeHandles;

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

        public event EventHandler<PlaybackFailureEventArgs>? Failure;

        public PlaybackState State { get; private set; } = PlaybackState.Idle;

        public Task? OpenGate { get; init; }

        public PlaybackFailure? OpenFailure { get; init; }

        public int InitializeCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public int MaxConcurrentOpenMedia { get; private set; }

        public int LiveNativeHandles => _liveNativeHandles;

        public string? LastPath { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCount++;
            return Task.CompletedTask;
        }

        public async Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            LastPath = request.Path;
            Transition(PlaybackState.Opening);
            while (_openingWaiters.TryDequeue(out var waiter))
            {
                waiter.TrySetResult();
            }

            if (OpenGate is not null)
            {
                await OpenGate.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (OpenFailure is not null)
            {
                Failure?.Invoke(this, new PlaybackFailureEventArgs(OpenFailure));
                throw new PlaybackFailureException(OpenFailure);
            }

            _liveNativeHandles++;
            MaxConcurrentOpenMedia = Math.Max(MaxConcurrentOpenMedia, _liveNativeHandles);
        }

        public Task PlayAsync(CancellationToken cancellationToken = default)
        {
            Transition(PlaybackState.Playing);
            PositionChanged?.Invoke(this, new PlaybackPositionChangedEventArgs(TimeSpan.Zero, TimeSpan.FromMinutes(1)));
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            Transition(PlaybackState.Paused);
            return Task.CompletedTask;
        }

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        {
            PositionChanged?.Invoke(this, new PlaybackPositionChangedEventArgs(position, TimeSpan.FromMinutes(1)));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            if (_liveNativeHandles > 0)
            {
                _liveNativeHandles--;
            }

            Transition(PlaybackState.Stopped);
            return Task.CompletedTask;
        }

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(State, TimeSpan.Zero, TimeSpan.FromMinutes(1), []));

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
            _liveNativeHandles = 0;
            return ValueTask.CompletedTask;
        }

        public Task WaitForOpeningAsync()
        {
            if (State == PlaybackState.Opening)
            {
                return Task.CompletedTask;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _openingWaiters.Enqueue(waiter);
            return waiter.Task;
        }

        private void Transition(PlaybackState next)
        {
            var previous = State;
            State = next;
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(previous, next));
        }
    }
}
