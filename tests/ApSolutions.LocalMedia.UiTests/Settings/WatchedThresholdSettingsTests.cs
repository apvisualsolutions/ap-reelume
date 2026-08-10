// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The watched threshold is a continuity rule, and the recommendation settings are where a person
/// decides what counts as watched (CNT-A01). The surface shows the threshold in force, applies a
/// new one through the use case that clamps and recalculates, and says how many states moved.
/// </summary>
public sealed class WatchedThresholdSettingsTests
{
    [Fact]
    public void The_threshold_section_only_exists_when_the_application_hands_the_use_case_over()
    {
        var without = new RecommendationSettingsViewModel(new StubRecommendationSettings());
        var with = new RecommendationSettingsViewModel(
            new StubRecommendationSettings(),
            Threshold(new InMemorySettingsStore(), new InMemoryWatchStateRepository()));

        Assert.False(without.HasWatchedThreshold);
        Assert.True(with.HasWatchedThreshold);
    }

    [Fact]
    public void The_slider_arrives_showing_the_threshold_in_force()
    {
        var settings = new InMemorySettingsStore();
        settings.Write(ConfigureWatchedThreshold.SettingKey, 0.75);

        var viewModel = new RecommendationSettingsViewModel(
            new StubRecommendationSettings(),
            Threshold(settings, new InMemoryWatchStateRepository()));

        Assert.Equal(75, viewModel.WatchedThresholdPercent);
        Assert.Equal(50, RecommendationSettingsViewModel.MinimumWatchedThresholdPercent);
        Assert.Equal(100, RecommendationSettingsViewModel.MaximumWatchedThresholdPercent);
    }

    [Fact]
    public async Task Applying_the_threshold_persists_it_recalculates_states_and_reports_the_count()
    {
        var settings = new InMemorySettingsStore();
        var repository = new InMemoryWatchStateRepository();
        var automatic = ContentKey.ForTitle(new TitleId(Guid.Parse("57a70001-0000-4000-8000-000000000001")));
        await repository.SaveAsync(
            new WatchState
            {
                Content = automatic,
                Position = TimeSpan.FromMinutes(30),
                ObservedDuration = TimeSpan.FromMinutes(50),
                SourceMediaFileId = new MediaFileId(Guid.Parse("57a70001-0000-4000-8000-0000000000f1")),
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = DateTimeOffset.UnixEpoch,
                UpdatedUtc = DateTimeOffset.UnixEpoch,
            },
            TestContext.Current.CancellationToken);
        var threshold = Threshold(settings, repository);
        var viewModel = new RecommendationSettingsViewModel(new StubRecommendationSettings(), threshold);

        // Thirty of fifty minutes is sixty per cent: below the default, above the new threshold.
        viewModel.WatchedThresholdPercent = 55;
        viewModel.ApplyWatchedThresholdCommand.Execute(null);
        await WaitForAsync(() => viewModel.HasThresholdResult);

        Assert.Equal(1, viewModel.RecalculatedCount);
        Assert.Equal(0.55, threshold.Current);
        var moved = await repository.GetAsync(automatic, TestContext.Current.CancellationToken);
        Assert.Equal(WatchStatus.Watched, moved!.Status);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static ConfigureWatchedThreshold Threshold(
        ISettingsStore settings,
        IWatchStateRepository repository) =>
        new(settings, repository, new FixedClock());

    private sealed class StubRecommendationSettings : IRecommendationSettings
    {
        private bool _enabled = true;

        public bool IsEnabled => _enabled;

        public void SetEnabled(bool enabled) => _enabled = enabled;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

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
        private readonly Dictionary<string, WatchState> _stored = [];

        public Task<WatchState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default) =>
            Task.FromResult(_stored.TryGetValue(content.Value, out var state) ? state : null);

        public Task<IReadOnlyList<WatchState>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WatchState>>([.. _stored.Values]);

        public Task SaveAsync(WatchState state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            _stored[state.Content.Value] = state;
            return Task.CompletedTask;
        }
    }
}
