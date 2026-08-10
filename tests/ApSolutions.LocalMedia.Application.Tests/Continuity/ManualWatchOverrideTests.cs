// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Continuity;

/// <summary>
/// A decision the person made by hand outranks everything the player computes, and changing the
/// watched threshold may only move the states that were computed automatically.
/// </summary>
public sealed class ManualWatchOverrideTests
{
    private static readonly ContentKey Content =
        ContentKey.ForTitle(new TitleId(Guid.Parse("3ad10001-0000-4000-8000-000000000001")));

    private static readonly MediaFileId Source = new(Guid.Parse("3ad10001-0000-4000-8000-0000000000f1"));

    private static readonly TimeSpan Episode = TimeSpan.FromMinutes(50);

    [Fact]
    public async Task Marking_content_watched_stores_the_decision_as_a_manual_override()
    {
        var repository = new InMemoryWatchStateRepository();
        var command = new SetWatchStatus(repository, new FixedClock());

        var state = await command.MarkAsync(
            Content,
            Source,
            WatchStatus.Watched,
            TestContext.Current.CancellationToken);

        Assert.Equal(WatchStatus.Watched, state.Status);
        Assert.True(state.IsManualOverride);
        var stored = await repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(WatchStatus.Watched, stored!.Status);
        Assert.True(stored.IsManualOverride);
    }

    [Fact]
    public async Task Marking_content_unwatched_keeps_the_stored_position_so_a_resume_is_still_possible()
    {
        var repository = new InMemoryWatchStateRepository();
        await repository.SaveAsync(
            Stored(TimeSpan.FromMinutes(48), WatchStatus.Watched, isManualOverride: false),
            TestContext.Current.CancellationToken);
        var command = new SetWatchStatus(repository, new FixedClock());

        var state = await command.MarkAsync(
            Content,
            Source,
            WatchStatus.NotStarted,
            TestContext.Current.CancellationToken);

        Assert.Equal(WatchStatus.NotStarted, state.Status);
        Assert.True(state.IsManualOverride);
        Assert.Equal(TimeSpan.FromMinutes(48), state.Position);
    }

    [Fact]
    public async Task Later_playback_never_overrules_a_manual_decision()
    {
        var repository = new InMemoryWatchStateRepository();
        var clock = new FixedClock();
        _ = await new SetWatchStatus(repository, clock).MarkAsync(
            Content,
            Source,
            WatchStatus.Watched,
            TestContext.Current.CancellationToken);
        await using var tracker = new PlaybackProgressTracker(repository, clock);
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);

        tracker.Observe(TimeSpan.FromMinutes(2), Episode);
        _ = await tracker.FlushAsync(PersistenceTrigger.Pause, TestContext.Current.CancellationToken);

        var stored = await repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(WatchStatus.Watched, stored!.Status);
        Assert.True(stored.IsManualOverride);
        Assert.Equal(TimeSpan.FromMinutes(2), stored.Position);
    }

    [Fact]
    public async Task Clearing_the_override_returns_the_state_to_what_the_position_says()
    {
        var repository = new InMemoryWatchStateRepository();
        await repository.SaveAsync(
            Stored(TimeSpan.FromMinutes(10), WatchStatus.Watched, isManualOverride: true),
            TestContext.Current.CancellationToken);
        var command = new SetWatchStatus(repository, new FixedClock());

        var state = await command.ClearOverrideAsync(
            Content,
            WatchStatePolicy.DefaultWatchedThreshold,
            TestContext.Current.CancellationToken);

        Assert.NotNull(state);
        Assert.False(state!.IsManualOverride);
        Assert.Equal(WatchStatus.InProgress, state.Status);
    }

    [Fact]
    public async Task Clearing_an_override_that_was_never_set_changes_nothing()
    {
        var repository = new InMemoryWatchStateRepository();
        var command = new SetWatchStatus(repository, new FixedClock());

        var state = await command.ClearOverrideAsync(
            Content,
            WatchStatePolicy.DefaultWatchedThreshold,
            TestContext.Current.CancellationToken);

        Assert.Null(state);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task The_threshold_is_clamped_and_persisted_between_sessions()
    {
        var settings = new InMemorySettingsStore();
        var repository = new InMemoryWatchStateRepository();
        var command = new ConfigureWatchedThreshold(settings, repository, new FixedClock());

        Assert.Equal(WatchStatePolicy.DefaultWatchedThreshold, command.Current);

        _ = await command.ExecuteAsync(0.20, TestContext.Current.CancellationToken);
        Assert.Equal(WatchStatePolicy.MinimumWatchedThreshold, command.Current);

        _ = await command.ExecuteAsync(0.75, TestContext.Current.CancellationToken);
        Assert.Equal(0.75, command.Current);
        Assert.Equal(
            0.75,
            new ConfigureWatchedThreshold(settings, repository, new FixedClock()).Current);
    }

    [Fact]
    public async Task Changing_the_threshold_recalculates_automatic_states_and_leaves_overrides_alone()
    {
        var repository = new InMemoryWatchStateRepository();
        var automatic = ContentKey.ForTitle(new TitleId(Guid.Parse("3ad10002-0000-4000-8000-000000000002")));
        var manual = ContentKey.ForTitle(new TitleId(Guid.Parse("3ad10003-0000-4000-8000-000000000003")));
        await repository.SaveAsync(
            Stored(TimeSpan.FromMinutes(30), WatchStatus.InProgress, isManualOverride: false, automatic),
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            Stored(TimeSpan.FromMinutes(30), WatchStatus.NotStarted, isManualOverride: true, manual),
            TestContext.Current.CancellationToken);
        var command = new ConfigureWatchedThreshold(
            new InMemorySettingsStore(),
            repository,
            new FixedClock());

        var recalculated = await command.ExecuteAsync(0.55, TestContext.Current.CancellationToken);

        Assert.Equal(1, recalculated);
        var automaticState = await repository.GetAsync(automatic, TestContext.Current.CancellationToken);
        var manualState = await repository.GetAsync(manual, TestContext.Current.CancellationToken);
        Assert.Equal(WatchStatus.Watched, automaticState!.Status);
        Assert.Equal(WatchStatus.NotStarted, manualState!.Status);
        Assert.True(manualState.IsManualOverride);
    }

    [Fact]
    public async Task A_threshold_change_that_moves_nothing_writes_nothing()
    {
        var repository = new InMemoryWatchStateRepository();
        await repository.SaveAsync(
            Stored(TimeSpan.FromMinutes(30), WatchStatus.InProgress, isManualOverride: false),
            TestContext.Current.CancellationToken);
        repository.Saved.Clear();
        var command = new ConfigureWatchedThreshold(
            new InMemorySettingsStore(),
            repository,
            new FixedClock());

        var recalculated = await command.ExecuteAsync(0.85, TestContext.Current.CancellationToken);

        Assert.Equal(0, recalculated);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task Progress_and_a_manual_decision_racing_each_other_still_end_on_the_manual_one()
    {
        var repository = new InMemoryWatchStateRepository();
        var clock = new FixedClock();
        await using var tracker = new PlaybackProgressTracker(repository, clock);
        _ = await tracker.BeginAsync(Content, Source, TestContext.Current.CancellationToken);
        var command = new SetWatchStatus(repository, clock);

        var work = new List<Task>();
        for (var index = 1; index <= 25; index++)
        {
            tracker.Observe(TimeSpan.FromSeconds(index * 10), Episode);
            work.Add(tracker.FlushAsync(PersistenceTrigger.Seek, TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(work);
        _ = await command.MarkAsync(Content, Source, WatchStatus.Watched, TestContext.Current.CancellationToken);
        tracker.Observe(TimeSpan.FromSeconds(300), Episode);
        _ = await tracker.FlushAsync(PersistenceTrigger.Close, TestContext.Current.CancellationToken);

        var stored = await repository.GetAsync(Content, TestContext.Current.CancellationToken);
        Assert.Equal(WatchStatus.Watched, stored!.Status);
        Assert.True(stored.IsManualOverride);
    }

    [Fact]
    public void Both_commands_refuse_to_exist_without_their_collaborators()
    {
        Assert.Throws<ArgumentNullException>(() => new SetWatchStatus(null!, new FixedClock()));
        Assert.Throws<ArgumentNullException>(
            () => new SetWatchStatus(new InMemoryWatchStateRepository(), null!));
        Assert.Throws<ArgumentNullException>(
            () => new ConfigureWatchedThreshold(null!, new InMemoryWatchStateRepository(), new FixedClock()));
        Assert.Throws<ArgumentNullException>(
            () => new ConfigureWatchedThreshold(new InMemorySettingsStore(), null!, new FixedClock()));
    }

    private static WatchState Stored(
        TimeSpan position,
        WatchStatus status,
        bool isManualOverride,
        ContentKey? content = null) => new()
        {
            Content = content ?? Content,
            Position = position,
            ObservedDuration = Episode,
            SourceMediaFileId = Source,
            Status = status,
            IsManualOverride = isManualOverride,
            StartedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        };

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 2, 18, 0, 0, TimeSpan.Zero);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    private sealed class InMemoryWatchStateRepository : IWatchStateRepository
    {
        private readonly Lock _sync = new();
        private readonly Dictionary<string, WatchState> _stored = [];

        public List<WatchState> Saved { get; } = [];

        public Task<WatchState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                return Task.FromResult(_stored.TryGetValue(content.Value, out var state) ? state : null);
            }
        }

        public Task<IReadOnlyList<WatchState>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                return Task.FromResult<IReadOnlyList<WatchState>>([.. _stored.Values]);
            }
        }

        public Task SaveAsync(WatchState state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            lock (_sync)
            {
                _stored[state.Content.Value] = state;
                Saved.Add(state);
            }

            return Task.CompletedTask;
        }
    }
}
