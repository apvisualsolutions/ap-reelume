// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Continuity;

/// <summary>
/// Switching to another version of the same content. The previous session is saved before anything is
/// opened, and a version that refuses to open leaves the stored progress exactly as it was.
/// </summary>
public sealed class SwitchMediaVersionTests
{
    private static readonly ContentKey Content =
        ContentKey.ForTitle(new TitleId(Guid.Parse("5c220001-0000-4000-8000-000000000001")));

    private static readonly MediaFileId Current = new(Guid.Parse("5c220001-0000-4000-8000-0000000000a1"));

    private static readonly MediaFileId Other = new(Guid.Parse("5c220001-0000-4000-8000-0000000000b2"));

    private static readonly TimeSpan Feature = TimeSpan.FromMinutes(100);

    [Fact]
    public async Task An_equivalent_version_opens_at_the_very_same_second()
    {
        var harness = await Harness.WithProgressAsync(TimeSpan.FromMinutes(50), Feature);

        var result = await harness.SwitchAsync(Version(Other, Feature));

        Assert.Equal(ProgressTransferKind.Exact, result.Decision.Kind);
        Assert.True(result.Opened);
        var request = Assert.Single(harness.Coordinator.Requests);
        Assert.Equal(Other, request.MediaFileId);
        Assert.Equal(TimeSpan.FromMinutes(50), request.StartPosition);
    }

    [Fact]
    public async Task The_previous_session_is_written_before_the_new_version_is_opened()
    {
        var harness = await Harness.WithProgressAsync(TimeSpan.FromMinutes(50), Feature);
        harness.Tracker.Observe(TimeSpan.FromMinutes(51), Feature);

        _ = await harness.SwitchAsync(Version(Other, Feature));

        Assert.Equal("save", harness.Repository.Journal[0]);
        Assert.Contains("open", harness.Repository.Journal);
        Assert.True(
            harness.Repository.Journal.IndexOf("save") < harness.Repository.Journal.IndexOf("open"),
            "The previous session must reach storage before the new version is opened.");
    }

    [Fact]
    public async Task A_confirmation_is_requested_without_opening_or_changing_anything()
    {
        var harness = await Harness.WithProgressAsync(TimeSpan.FromMinutes(50), Feature);

        var result = await harness.SwitchAsync(Version(Other, TimeSpan.FromMinutes(130)));

        Assert.Equal(ProgressTransferKind.Confirm, result.Decision.Kind);
        Assert.False(result.Opened);
        Assert.Empty(harness.Coordinator.Requests);
        var stored = await harness.Repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromMinutes(50), stored!.Position);
        Assert.Equal(Current, stored.SourceMediaFileId);
    }

    [Fact]
    public async Task A_confirmed_switch_opens_at_the_suggested_second_and_records_the_new_source()
    {
        var harness = await Harness.WithProgressAsync(TimeSpan.FromMinutes(50), Feature);
        var target = Version(Other, TimeSpan.FromMinutes(130));

        var result = await harness.SwitchAsync(target, confirmed: true);

        Assert.True(result.Opened);
        Assert.Equal(TimeSpan.FromMinutes(65), Assert.Single(harness.Coordinator.Requests).StartPosition);
        var stored = await harness.Repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromMinutes(65), stored!.Position);
        Assert.Equal(Other, stored.SourceMediaFileId);
        Assert.Equal(TimeSpan.FromMinutes(130), stored.ObservedDuration);
    }

    /// <summary>
    /// "Start again" is the dialog's second answer: the person switches without carrying the
    /// progress across, so the new version opens at zero and zero is what gets recorded.
    /// </summary>
    [Fact]
    public async Task Choosing_to_start_again_opens_the_new_version_at_zero_and_records_zero()
    {
        var harness = await Harness.WithProgressAsync(TimeSpan.FromMinutes(50), Feature);
        var target = Version(Other, TimeSpan.FromMinutes(130));

        var result = await harness.SwitchAsync(target, confirmed: true, restartFromZero: true);

        Assert.True(result.Opened);
        Assert.Equal(TimeSpan.Zero, Assert.Single(harness.Coordinator.Requests).StartPosition);
        var stored = await harness.Repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.Zero, stored!.Position);
        Assert.Equal(Other, stored.SourceMediaFileId);
    }

    [Fact]
    public async Task A_version_that_refuses_to_open_leaves_the_progress_and_its_source_untouched()
    {
        var harness = await Harness.WithProgressAsync(TimeSpan.FromMinutes(50), Feature);
        harness.Coordinator.FailureOnStart = new PlaybackFailure(PlaybackFailureCode.FileNotFound, "gone");

        var result = await harness.SwitchAsync(Version(Other, Feature));

        Assert.False(result.Opened);
        Assert.Equal(PlaybackFailureCode.FileNotFound, result.Failure!.Code);
        var stored = await harness.Repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromMinutes(50), stored!.Position);
        Assert.Equal(Current, stored.SourceMediaFileId);
        Assert.Equal(Feature, stored.ObservedDuration);
    }

    [Fact]
    public async Task A_proportional_switch_moves_the_second_and_keeps_a_manual_decision()
    {
        var harness = await Harness.WithProgressAsync(
            TimeSpan.FromMinutes(50),
            Feature,
            status: WatchStatus.Watched,
            isManualOverride: true);

        var result = await harness.SwitchAsync(Version(Other, TimeSpan.FromMinutes(105)));

        Assert.Equal(ProgressTransferKind.Proportional, result.Decision.Kind);
        Assert.True(result.Opened);
        var stored = await harness.Repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromMinutes(52.5), stored!.Position);
        Assert.True(stored.IsManualOverride);
        Assert.Equal(WatchStatus.Watched, stored.Status);
    }

    [Fact]
    public async Task Content_without_usable_progress_simply_restarts_the_new_version()
    {
        var harness = await Harness.EmptyAsync();

        var result = await harness.SwitchAsync(Version(Other, Feature));

        Assert.Equal(ProgressTransferKind.Restart, result.Decision.Kind);
        Assert.True(result.Opened);
        Assert.Equal(TimeSpan.Zero, Assert.Single(harness.Coordinator.Requests).StartPosition);
    }

    [Fact]
    public async Task An_unavailable_version_is_refused_before_the_engine_is_asked()
    {
        var harness = await Harness.WithProgressAsync(TimeSpan.FromMinutes(50), Feature);

        var result = await harness.SwitchAsync(Version(Other, Feature, isAvailable: false));

        Assert.False(result.Opened);
        Assert.Equal(PlaybackFailureCode.FileNotFound, result.Failure!.Code);
        Assert.Empty(harness.Coordinator.Requests);
        var stored = await harness.Repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(Current, stored!.SourceMediaFileId);
    }

    private static MediaVersion Version(MediaFileId id, TimeSpan? duration, bool isAvailable = true) =>
        new(id, $@"D:\Media\{id.Value:N}.mkv", isAvailable, duration, 1920, 1080, false, "H264", 4_000_000_000);

    private sealed class Harness
    {
        private Harness(JournalWatchStateRepository repository, RecordingCoordinator coordinator, PlaybackProgressTracker tracker, SwitchMediaVersion command)
        {
            Repository = repository;
            Coordinator = coordinator;
            Tracker = tracker;
            Command = command;
        }

        public JournalWatchStateRepository Repository { get; }

        public RecordingCoordinator Coordinator { get; }

        public PlaybackProgressTracker Tracker { get; }

        public SwitchMediaVersion Command { get; }

        public static async Task<Harness> EmptyAsync() => await CreateAsync(null);

        public static async Task<Harness> WithProgressAsync(
            TimeSpan position,
            TimeSpan? duration,
            WatchStatus status = WatchStatus.InProgress,
            bool isManualOverride = false) =>
            await CreateAsync(new WatchState
            {
                Content = Content,
                Position = position,
                ObservedDuration = duration,
                SourceMediaFileId = Current,
                Status = status,
                IsManualOverride = isManualOverride,
                StartedUtc = DateTimeOffset.UnixEpoch,
                UpdatedUtc = DateTimeOffset.UnixEpoch,
            });

        public Task<SwitchMediaVersionResult> SwitchAsync(
            MediaVersion target,
            bool confirmed = false,
            bool restartFromZero = false) =>
            Command.ExecuteAsync(
                new SwitchMediaVersionCommand(
                    Content,
                    target,
                    StructureIsCompatible: true,
                    Confirmed: confirmed,
                    RestartFromZero: restartFromZero),
                TestContext.Current.CancellationToken);

        private static async Task<Harness> CreateAsync(WatchState? initial)
        {
            var repository = new JournalWatchStateRepository();
            if (initial is not null)
            {
                await repository.SaveAsync(initial, TestContext.Current.CancellationToken);
                repository.Journal.Clear();
            }

            var coordinator = new RecordingCoordinator(repository.Journal);
            var clock = new FixedClock();
            var tracker = new PlaybackProgressTracker(repository, clock);
            _ = await tracker.BeginAsync(Content, Current, TestContext.Current.CancellationToken);
            repository.Journal.Clear();
            return new Harness(
                repository,
                coordinator,
                tracker,
                new SwitchMediaVersion(repository, coordinator, tracker, clock));
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 2, 19, 0, 0, TimeSpan.Zero);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class JournalWatchStateRepository : IWatchStateRepository
    {
        private readonly Dictionary<string, WatchState> _stored = [];

        public List<string> Journal { get; } = [];

        public Task<WatchState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default) =>
            Task.FromResult(_stored.TryGetValue(content.Value, out var state) ? state : null);

        public Task<IReadOnlyList<WatchState>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WatchState>>([.. _stored.Values]);

        public Task SaveAsync(WatchState state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            _stored[state.Content.Value] = state;
            Journal.Add("save");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCoordinator(List<string> journal) : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession { get; private set; }

        public PlaybackFailure? FailureOnStart { get; set; }

        public List<PlaybackRequest> Requests { get; } = [];

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            journal.Add("open");
            if (FailureOnStart is { } failure)
            {
                throw new PlaybackFailureException(failure);
            }

            Requests.Add(request);
            ActiveSession = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            return Task.FromResult(ActiveSession);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            ActiveSession = null;
            return Task.CompletedTask;
        }
    }
}
