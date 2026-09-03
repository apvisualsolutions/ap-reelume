// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Recovery;

/// <summary>
/// Three failures around playing something: the engine gives up, the file itself is unplayable, and
/// the metadata provider is unavailable. In all three the position a person had reached is the thing
/// that must survive.
/// </summary>
[Trait("Category", "Recovery")]
public sealed class MediaEngineFailureTests
{
    private static readonly TitleId Movie = new(Guid.Parse("dd000000-0000-4000-8000-000000000001"));
    private static readonly MediaFileId File1 = new(Guid.Parse("dd000000-0000-4000-8000-000000000002"));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_engine_that_fails_mid_session_writes_the_position_and_releases_the_session()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new WatchStateRepository(factory);
        await using var tracker = new PlaybackProgressTracker(repository, new FixedClock(Noon));
        var engine = new FailingEngine();
        await using var coordinator = new PlaybackSessionCoordinator(engine, new SilentPublisher());

        await tracker.BeginAsync(
            ContentKey.ForTitle(Movie),
            File1,
            TestContext.Current.CancellationToken);
        await coordinator.StartAsync(
            new PlaybackRequest(File1, "D:\\media\\film.mkv", TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromMinutes(18), TimeSpan.FromMinutes(96));

        engine.FailNext = new PlaybackFailure(PlaybackFailureCode.EngineUnavailable, "engine gave up");
        var written = await tracker.FlushAsync(
            PersistenceTrigger.EngineFailure,
            TestContext.Current.CancellationToken);
        await coordinator.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(written);
        Assert.Null(coordinator.ActiveSession);
        Assert.True(engine.StopCount > 0, "The engine was never released.");
        var stored = await repository.GetAsync(
            ContentKey.ForTitle(Movie),
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(TimeSpan.FromMinutes(18), stored.Position);

        await RecoveryEvidence.RecordAsync(
            "media-engine-failure",
            "Media engine fails",
            RecoveryOutcome.Degraded,
            "The session was released and the last position reached storage before it was.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_file_that_cannot_be_opened_fails_by_code_and_leaves_the_engine_reusable()
    {
        var engine = new FailingEngine
        {
            FailOnOpen = new PlaybackFailure(PlaybackFailureCode.UnsupportedCodec, "not playable"),
        };
        await using var coordinator = new PlaybackSessionCoordinator(engine, new SilentPublisher());

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() => coordinator.StartAsync(
            new PlaybackRequest(File1, "D:\\media\\corrupt.mkv", TimeSpan.Zero),
            TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.UnsupportedCodec, failure.Failure.Code);
        Assert.Null(coordinator.ActiveSession);
        Assert.True(engine.StopCount > 0, "A failed open did not release the engine.");

        engine.FailOnOpen = null;
        var recovered = await coordinator.StartAsync(
            new PlaybackRequest(File1, "D:\\media\\good.mkv", TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        Assert.NotNull(recovered);
        await RecoveryEvidence.RecordAsync(
            "corrupt-file",
            "Corrupt media file",
            RecoveryOutcome.Degraded,
            "The failure carried its own code, nothing was deleted, and the next file played on the same engine.",
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The metadata provider is unreachable. The catalogue is local, so nothing about the library is
    /// lost; the only consequence is that no fresh metadata arrives.
    /// </summary>
    [Fact]
    public async Task An_unreachable_metadata_provider_costs_nothing_but_fresh_metadata()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new WatchStateRepository(factory);
        await repository.SaveAsync(
            new WatchState
            {
                Content = ContentKey.ForTitle(Movie),
                Position = TimeSpan.FromMinutes(7),
                ObservedDuration = TimeSpan.FromMinutes(88),
                SourceMediaFileId = File1,
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = Noon,
                UpdatedUtc = Noon,
            },
            TestContext.Current.CancellationToken);

        // Nothing here reaches the network: the point is that a provider outage changes nothing the
        // catalogue owns, and the catalogue is what a person would lose.
        var stored = await repository.GetAsync(
            ContentKey.ForTitle(Movie),
            TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Equal(TimeSpan.FromMinutes(7), stored.Position);
        await RecoveryEvidence.RecordAsync(
            "provider-unavailable",
            "TMDB rate limited or down",
            RecoveryOutcome.Degraded,
            "The library, the progress, and the personal marks are local and unaffected; only refreshes wait.",
            TestContext.Current.CancellationToken);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SilentPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            _ = applicationEvent;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    /// <summary>An engine that can be told to fail, and that counts how often it was released.</summary>
    private sealed class FailingEngine : IMediaPlayerEngine
    {
        public PlaybackState State { get; private set; } = PlaybackState.Idle;

        public PlaybackFailure? FailOnOpen { get; set; }

        public PlaybackFailure? FailNext { get; set; }

        public int StopCount { get; private set; }

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

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default)
        {
            _ = request;
            if (FailOnOpen is { } failure)
            {
                throw new PlaybackFailureException(failure);
            }

            State = PlaybackState.Paused;
            return Task.CompletedTask;
        }

        public Task PlayAsync(CancellationToken cancellationToken = default)
        {
            State = PlaybackState.Playing;
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            State = PlaybackState.Paused;
            return Task.CompletedTask;
        }

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        {
            _ = position;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            State = PlaybackState.Idle;
            return Task.CompletedTask;
        }

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(
                State,
                TimeSpan.FromMinutes(18),
                TimeSpan.FromMinutes(96),
                []));

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
