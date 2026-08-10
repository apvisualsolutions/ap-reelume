// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Continuity;

/// <summary>
/// Creating, editing, and deleting markers by hand. The MVP creates manual markers only: the model
/// keeps the fields a future detector will need, but nothing here detects anything.
/// </summary>
public sealed class ManualMarkerTests
{
    private static readonly SeriesId Series = new(Guid.Parse("c5e10001-0000-4000-8000-000000000001"));

    private static readonly SeriesId OtherSeries = new(Guid.Parse("c5e10002-0000-4000-8000-000000000002"));

    private static readonly TimeSpan Episode = TimeSpan.FromMinutes(50);

    [Fact]
    public async Task A_valid_range_is_stored_as_a_manual_marker()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);

        var result = await command.ExecuteAsync(
            new SaveManualMarkerCommand(
                Series,
                MarkerKind.Intro,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(120),
                Episode),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaveMarkerOutcome.Saved, result.Outcome);
        Assert.Equal(MarkerOrigin.Manual, result.Marker!.Origin);
        Assert.Null(result.Marker.Confidence);
        Assert.False(result.Marker.UserCorrected);
        var stored = await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken);
        Assert.Single(stored);
    }

    [Fact]
    public async Task An_invalid_range_is_refused_and_nothing_is_stored()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);

        var result = await command.ExecuteAsync(
            new SaveManualMarkerCommand(
                Series,
                MarkerKind.Intro,
                TimeSpan.FromSeconds(200),
                TimeSpan.FromSeconds(100),
                Episode),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaveMarkerOutcome.InvalidRange, result.Outcome);
        Assert.Null(result.Marker);
        Assert.Empty(await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_range_past_the_end_of_the_episode_is_refused()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);

        var result = await command.ExecuteAsync(
            new SaveManualMarkerCommand(
                Series,
                MarkerKind.Credits,
                TimeSpan.FromMinutes(49),
                TimeSpan.FromMinutes(51),
                Episode),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaveMarkerOutcome.InvalidRange, result.Outcome);
    }

    [Fact]
    public async Task An_overlap_with_the_same_kind_is_refused_and_names_the_marker_it_hits()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);
        var first = await command.ExecuteAsync(
            new SaveManualMarkerCommand(Series, MarkerKind.Intro, Seconds(30), Seconds(120), Episode),
            TestContext.Current.CancellationToken);

        var second = await command.ExecuteAsync(
            new SaveManualMarkerCommand(Series, MarkerKind.Intro, Seconds(100), Seconds(180), Episode),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaveMarkerOutcome.Overlaps, second.Outcome);
        Assert.Equal(first.Marker!.Id, second.Conflict!.Id);
        Assert.Single(await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Another_kind_may_share_the_same_seconds()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);
        _ = await command.ExecuteAsync(
            new SaveManualMarkerCommand(Series, MarkerKind.Intro, Seconds(30), Seconds(120), Episode),
            TestContext.Current.CancellationToken);

        var recap = await command.ExecuteAsync(
            new SaveManualMarkerCommand(Series, MarkerKind.Recap, Seconds(30), Seconds(120), Episode),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaveMarkerOutcome.Saved, recap.Outcome);
        Assert.Equal(2, (await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Editing_a_marker_replaces_it_instead_of_colliding_with_itself()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);
        var created = await command.ExecuteAsync(
            new SaveManualMarkerCommand(Series, MarkerKind.Intro, Seconds(30), Seconds(120), Episode),
            TestContext.Current.CancellationToken);

        var edited = await command.ExecuteAsync(
            new SaveManualMarkerCommand(
                Series,
                MarkerKind.Intro,
                Seconds(40),
                Seconds(130),
                Episode,
                created.Marker!.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaveMarkerOutcome.Saved, edited.Outcome);
        var stored = Assert.Single(await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
        Assert.Equal(created.Marker.Id, stored.Id);
        Assert.Equal(Seconds(40), stored.Start);
        Assert.Equal(Seconds(130), stored.End);
    }

    [Fact]
    public async Task Markers_belong_to_one_series_and_never_leak_into_another()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);
        _ = await command.ExecuteAsync(
            new SaveManualMarkerCommand(Series, MarkerKind.Intro, Seconds(30), Seconds(120), Episode),
            TestContext.Current.CancellationToken);

        var other = await command.ExecuteAsync(
            new SaveManualMarkerCommand(OtherSeries, MarkerKind.Intro, Seconds(30), Seconds(120), Episode),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaveMarkerOutcome.Saved, other.Outcome);
        Assert.Single(await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
        Assert.Single(await repository.GetForSeriesAsync(OtherSeries, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_manual_marker_can_be_deleted_and_an_unknown_one_is_not_an_error()
    {
        var repository = new InMemoryMarkerRepository();
        var save = new SaveManualMarker(repository);
        var delete = new DeleteManualMarker(repository);
        var created = await save.ExecuteAsync(
            new SaveManualMarkerCommand(Series, MarkerKind.Intro, Seconds(30), Seconds(120), Episode),
            TestContext.Current.CancellationToken);

        Assert.True(await delete.ExecuteAsync(created.Marker!.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
        Assert.False(await delete.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_detected_marker_is_never_deleted_by_the_manual_command()
    {
        var repository = new InMemoryMarkerRepository();
        var detected = new IntroMarker(
            Guid.Parse("c5e10003-0000-4000-8000-000000000003"),
            Series,
            MarkerKind.Intro,
            Seconds(30),
            Seconds(120),
            MarkerOrigin.Detected,
            Confidence: 0.8,
            UserCorrected: false);
        await repository.SaveAsync(detected, TestContext.Current.CancellationToken);

        var deleted = await new DeleteManualMarker(repository)
            .ExecuteAsync(detected.Id, TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Single(await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_command_only_ever_produces_manual_markers()
    {
        var repository = new InMemoryMarkerRepository();
        var command = new SaveManualMarker(repository);

        foreach (var kind in Enum.GetValues<MarkerKind>())
        {
            var result = await command.ExecuteAsync(
                new SaveManualMarkerCommand(
                    Series,
                    kind,
                    Seconds(30 + ((int)kind * 300)),
                    Seconds(120 + ((int)kind * 300)),
                    Episode),
                TestContext.Current.CancellationToken);
            Assert.Equal(MarkerOrigin.Manual, result.Marker!.Origin);
        }

        var stored = await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken);
        Assert.All(stored, marker => Assert.Equal(MarkerOrigin.Manual, marker.Origin));
    }

    [Fact]
    public void The_manual_path_cannot_fabricate_a_detected_origin()
    {
        // Until T43 this test asserted that no detection type existed anywhere. Detection exists
        // now, by plan; what must stay true forever is that the manual path is manual: the command
        // exposes no origin for a caller to set, and the shared model keeps the confidence field
        // detection writes.
        Assert.DoesNotContain(
            typeof(SaveManualMarkerCommand).GetProperties(),
            property => property.PropertyType == typeof(MarkerOrigin));
        Assert.Contains(
            typeof(IntroMarker).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == nameof(IntroMarker.Confidence));
    }

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);

    private sealed class InMemoryMarkerRepository : IIntroMarkerRepository
    {
        private readonly Dictionary<Guid, IntroMarker> _markers = [];

        public Task<IReadOnlyList<IntroMarker>> GetForSeriesAsync(
            SeriesId seriesId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IntroMarker>>(
                [.. _markers.Values.Where(marker => marker.SeriesId == seriesId).OrderBy(marker => marker.Start)]);

        public Task<IntroMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_markers.TryGetValue(markerId, out var marker) ? marker : null);

        public Task SaveAsync(IntroMarker marker, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(marker);
            _markers[marker.Id] = marker;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default)
        {
            _ = _markers.Remove(markerId);
            return Task.CompletedTask;
        }
    }
}
