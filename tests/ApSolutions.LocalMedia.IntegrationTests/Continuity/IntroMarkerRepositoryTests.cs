using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Continuity;

/// <summary>
/// Markers must survive a restart, stay inside their own series, and keep the origin and confidence
/// columns a later release will need.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IntroMarkerRepositoryTests
{
    private static readonly SeriesId Series = new(Guid.Parse("e8b10001-0000-4000-8000-000000000001"));

    private static readonly SeriesId OtherSeries = new(Guid.Parse("e8b10002-0000-4000-8000-000000000002"));

    [Fact]
    public async Task A_marker_round_trips_through_a_new_repository_instance()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var marker = Marker(MarkerKind.Credits, 2_800, 3_000);

        await new IntroMarkerRepository(factory).SaveAsync(marker, TestContext.Current.CancellationToken);
        var restored = await new IntroMarkerRepository(factory).GetAsync(
            marker.Id,
            TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.Equal(marker, restored);
    }

    [Fact]
    public async Task Saving_the_same_identifier_edits_the_marker_instead_of_duplicating_it()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new IntroMarkerRepository(factory);
        var marker = Marker(MarkerKind.Intro, 30, 120);

        await repository.SaveAsync(marker, TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            marker with { Start = TimeSpan.FromSeconds(40), End = TimeSpan.FromSeconds(130) },
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(
            await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
        Assert.Equal(TimeSpan.FromSeconds(40), stored.Start);
        Assert.Equal(TimeSpan.FromSeconds(130), stored.End);
    }

    [Fact]
    public async Task Markers_are_read_per_series_in_ascending_order_and_can_be_deleted()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new IntroMarkerRepository(factory);
        var credits = Marker(MarkerKind.Credits, 2_800, 3_000);
        var intro = Marker(MarkerKind.Intro, 30, 120);
        await repository.SaveAsync(credits, TestContext.Current.CancellationToken);
        await repository.SaveAsync(intro, TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            Marker(MarkerKind.Intro, 30, 120, OtherSeries),
            TestContext.Current.CancellationToken);

        var series = await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken);
        Assert.Equal([intro.Id, credits.Id], series.Select(marker => marker.Id));
        Assert.Single(await repository.GetForSeriesAsync(OtherSeries, TestContext.Current.CancellationToken));

        await repository.DeleteAsync(intro.Id, TestContext.Current.CancellationToken);
        Assert.Single(await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
        Assert.Null(await repository.GetAsync(intro.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_origin_and_confidence_columns_hold_what_a_later_release_will_write()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new IntroMarkerRepository(factory);
        var detected = Marker(MarkerKind.Recap, 10, 60) with
        {
            Origin = MarkerOrigin.Detected,
            Confidence = 0.72,
            UserCorrected = true,
        };

        await repository.SaveAsync(detected, TestContext.Current.CancellationToken);
        var restored = await repository.GetAsync(detected.Id, TestContext.Current.CancellationToken);

        Assert.Equal(MarkerOrigin.Detected, restored!.Origin);
        Assert.Equal(0.72, restored.Confidence);
        Assert.True(restored.UserCorrected);
    }

    [Fact]
    public async Task An_inverted_range_is_refused_by_the_schema_itself()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new IntroMarkerRepository(factory);
        var inverted = Marker(MarkerKind.Intro, 30, 120) with
        {
            Start = TimeSpan.FromSeconds(120),
            End = TimeSpan.FromSeconds(30),
        };

        _ = await Assert.ThrowsAnyAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => repository.SaveAsync(inverted, TestContext.Current.CancellationToken));
    }

    private static IntroMarker Marker(
        MarkerKind kind,
        double startSeconds,
        double endSeconds,
        SeriesId? series = null) =>
        new(
            Guid.Parse($"e8b1{(int)kind:D4}-{(int)startSeconds % 10000:D4}-4000-8000-{(series is null ? 1 : 2):D12}"),
            series ?? Series,
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            MarkerOrigin.Manual,
            Confidence: null,
            UserCorrected: false);
}
