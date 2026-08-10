// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Continuity;

/// <summary>
/// The countdown that plays the next episode. It is cancelable from any input method, it checks that
/// the file is still there at the very last moment, and it can never leave two sessions open.
/// </summary>
public sealed class NextEpisodeCountdownTests
{
    private static readonly TitleId Show = new(Guid.Parse("94ab0001-0000-4000-8000-000000000001"));

    [Fact]
    public async Task The_countdown_announces_every_second_and_then_opens_the_next_episode()
    {
        var harness = Harness.WithSeason();
        var run = harness.Countdown.ExecuteAsync(
            Show,
            harness.Episodes[0].Id,
            TestContext.Current.CancellationToken);

        for (var tick = 0; tick < StartNextEpisodeCountdown.DefaultCountdownSeconds; tick++)
        {
            await harness.Clock.AdvanceAsync(TestContext.Current.CancellationToken);
        }

        var result = await run;

        Assert.Equal(NextEpisodeOutcome.Started, result.Outcome);
        Assert.Equal(harness.Episodes[1].Id, result.Episode!.Id);
        Assert.Equal([10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0], harness.Announced);
        Assert.Equal(harness.Episodes[1].Path, Assert.Single(harness.Coordinator.Requests).Path);
    }

    [Fact]
    public async Task A_countdown_of_zero_seconds_means_the_chain_is_switched_off()
    {
        var harness = Harness.WithSeason();
        harness.Countdown.ConfigureCountdown(0);

        var result = await harness.Countdown.ExecuteAsync(
            Show,
            harness.Episodes[0].Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Disabled, result.Outcome);
        Assert.Empty(harness.Coordinator.Requests);
    }

    [Theory]
    [InlineData(InputOrigin.Keyboard)]
    [InlineData(InputOrigin.Mouse)]
    [InlineData(InputOrigin.MediaKey)]
    public async Task Any_input_method_cancels_the_countdown(InputOrigin origin)
    {
        var harness = Harness.WithSeason();
        using var router = new InputCommandRouter((_, _) =>
        {
            harness.Countdown.Cancel();
            return Task.CompletedTask;
        });
        var run = harness.Countdown.ExecuteAsync(
            Show,
            harness.Episodes[0].Id,
            TestContext.Current.CancellationToken);
        await harness.Clock.AdvanceAsync(TestContext.Current.CancellationToken);

        _ = await router.DispatchAsync(
            PlaybackInputCommand.Stop,
            origin,
            TestContext.Current.CancellationToken);
        await harness.Clock.AdvanceAsync(TestContext.Current.CancellationToken);
        var result = await run;

        Assert.Equal(NextEpisodeOutcome.Cancelled, result.Outcome);
        Assert.Empty(harness.Coordinator.Requests);
    }

    [Fact]
    public async Task An_episode_that_disappears_during_the_countdown_is_never_opened()
    {
        var harness = Harness.WithSeason();
        var run = harness.Countdown.ExecuteAsync(
            Show,
            harness.Episodes[0].Id,
            TestContext.Current.CancellationToken);

        for (var tick = 0; tick < StartNextEpisodeCountdown.DefaultCountdownSeconds - 1; tick++)
        {
            await harness.Clock.AdvanceAsync(TestContext.Current.CancellationToken);
        }

        // The drive is pulled out with one second left on the clock.
        harness.Repository.MakeUnavailable(harness.Episodes[1].Id);
        await harness.Clock.AdvanceAsync(TestContext.Current.CancellationToken);
        var result = await run;

        Assert.Equal(NextEpisodeOutcome.Unavailable, result.Outcome);
        Assert.Empty(harness.Coordinator.Requests);
    }

    [Fact]
    public async Task A_series_with_nothing_playable_left_reports_it_instead_of_opening_anything()
    {
        var harness = Harness.WithSeason();

        var result = await harness.Countdown.ExecuteAsync(
            Show,
            harness.Episodes[^1].Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.NoNextEpisode, result.Outcome);
        Assert.Null(result.Episode);
        Assert.Empty(harness.Coordinator.Requests);
    }

    [Fact]
    public async Task Three_episodes_chain_without_two_sessions_ever_being_open()
    {
        var harness = Harness.WithSeason();
        var current = harness.Episodes[0].Id;

        for (var chained = 0; chained < 3; chained++)
        {
            var run = harness.Countdown.ExecuteAsync(Show, current, TestContext.Current.CancellationToken);
            for (var tick = 0; tick < StartNextEpisodeCountdown.DefaultCountdownSeconds; tick++)
            {
                await harness.Clock.AdvanceAsync(TestContext.Current.CancellationToken);
            }

            var result = await run;
            Assert.Equal(NextEpisodeOutcome.Started, result.Outcome);
            current = result.Episode!.Id;
        }

        Assert.Equal(3, harness.Coordinator.Requests.Count);
        Assert.Equal(1, harness.Coordinator.MaximumConcurrentSessions);
        Assert.Equal(harness.Episodes[3].Id.Value, current.Value);
    }

    [Fact]
    public void The_countdown_length_is_clamped_to_the_approved_range_and_persisted()
    {
        var harness = Harness.WithSeason();

        Assert.Equal(StartNextEpisodeCountdown.DefaultCountdownSeconds, harness.Countdown.CountdownSeconds);

        harness.Countdown.ConfigureCountdown(-5);
        Assert.Equal(0, harness.Countdown.CountdownSeconds);

        harness.Countdown.ConfigureCountdown(600);
        Assert.Equal(StartNextEpisodeCountdown.MaximumCountdownSeconds, harness.Countdown.CountdownSeconds);

        harness.Countdown.ConfigureCountdown(25);
        Assert.Equal(25, harness.Countdown.CountdownSeconds);
        Assert.Equal(25, harness.Rebuild().CountdownSeconds);
    }

    [Fact]
    public async Task A_countdown_that_fails_to_open_reports_the_failure_rather_than_pretending()
    {
        var harness = Harness.WithSeason();
        harness.Coordinator.FailureOnStart = new PlaybackFailure(PlaybackFailureCode.OpenFailed, "no");
        var run = harness.Countdown.ExecuteAsync(
            Show,
            harness.Episodes[0].Id,
            TestContext.Current.CancellationToken);

        for (var tick = 0; tick < StartNextEpisodeCountdown.DefaultCountdownSeconds; tick++)
        {
            await harness.Clock.AdvanceAsync(TestContext.Current.CancellationToken);
        }

        var result = await run;

        Assert.Equal(NextEpisodeOutcome.Unavailable, result.Outcome);
    }

    private sealed class Harness
    {
        private readonly InMemorySettingsStore _settings = new();

        private Harness(IReadOnlyList<EpisodeSequenceEntry> episodes)
        {
            Episodes = episodes;
            Repository = new FakeEpisodeSequenceRepository(episodes);
            Coordinator = new CountingCoordinator();
            Clock = new ManualClock();
            Countdown = Build();
        }

        public IReadOnlyList<EpisodeSequenceEntry> Episodes { get; }

        public FakeEpisodeSequenceRepository Repository { get; }

        public CountingCoordinator Coordinator { get; }

        public ManualClock Clock { get; }

        public StartNextEpisodeCountdown Countdown { get; }

        public List<int> Announced { get; } = [];

        public static Harness WithSeason()
        {
            var harness = new Harness(
            [
                Episode(1, 1),
                Episode(1, 2),
                Episode(1, 3),
                Episode(1, 4),
            ]);
            harness.Countdown.Ticked += (_, remaining) => harness.Announced.Add(remaining);
            return harness;
        }

        public StartNextEpisodeCountdown Rebuild() => Build();

        private StartNextEpisodeCountdown Build() => new(
            new GetNextEpisode(Repository),
            Repository,
            Coordinator,
            _settings,
            Clock);

        private static EpisodeSequenceEntry Episode(int season, int number)
        {
            var id = new EpisodeId(Guid.Parse($"94ab{season:D4}-{number:D4}-4000-8000-000000000001"));
            return new EpisodeSequenceEntry(
                id,
                Show,
                season,
                number,
                new MediaFileId(id.Value),
                $@"D:\Media\S{season:D2}E{number:D2}.mkv",
                IsAvailable: true);
        }
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    private sealed class FakeEpisodeSequenceRepository(IReadOnlyList<EpisodeSequenceEntry> episodes)
        : IEpisodeSequenceRepository
    {
        private readonly Dictionary<Guid, EpisodeSequenceEntry> _entries =
            episodes.ToDictionary(entry => entry.Id.Value, entry => entry);

        public Task<IReadOnlyList<EpisodeSequenceEntry>> GetSeriesAsync(
            TitleId showId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EpisodeSequenceEntry>>(
                [.. _entries.Values.Where(entry => entry.ShowId == showId)]);

        public Task<EpisodeSequenceEntry?> GetAsync(
            EpisodeId episodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.TryGetValue(episodeId.Value, out var entry) ? entry : null);

        public Task<EpisodeSequenceEntry?> FindByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.Values.FirstOrDefault(entry => entry.MediaFileId == fileId));

        public void MakeUnavailable(EpisodeId episodeId) =>
            _entries[episodeId.Value] = _entries[episodeId.Value] with { IsAvailable = false };
    }

    private sealed class CountingCoordinator : IPlaybackSessionCoordinator
    {
        private int _active;

        public PlaybackSession? ActiveSession { get; private set; }

        public PlaybackFailure? FailureOnStart { get; set; }

        public List<PlaybackRequest> Requests { get; } = [];

        public int MaximumConcurrentSessions { get; private set; }

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (FailureOnStart is { } failure)
            {
                throw new PlaybackFailureException(failure);
            }

            // Starting replaces the previous session, exactly as the coordinator contract promises.
            _active = 1;
            MaximumConcurrentSessions = Math.Max(MaximumConcurrentSessions, _active);
            Requests.Add(request);
            ActiveSession = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            return Task.FromResult(ActiveSession);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _active = 0;
            ActiveSession = null;
            return Task.CompletedTask;
        }
    }

    private sealed class ManualClock : IClock
    {
        private readonly Lock _sync = new();
        private readonly List<TimeSpan> _delays = [];
        private TaskCompletionSource? _pending;
        private TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DateTimeOffset UtcNow { get; private set; } = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

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

        public async Task AdvanceAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource requested;
            lock (_sync)
            {
                requested = _requested;
            }

            await requested.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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
    }
}
