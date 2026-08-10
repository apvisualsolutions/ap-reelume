// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Continuity;

/// <summary>
/// Detected rows must survive a restart, stay inside their own series and file, and be replaceable
/// as one atomic set, because a re-detection writes the whole series at once.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DetectedMarkerRepositoryTests
{
    private static readonly SeriesId Series = new(Guid.Parse("e8b10003-0000-4000-8000-000000000001"));

    private static readonly SeriesId OtherSeries = new(Guid.Parse("e8b10003-0000-4000-8000-000000000002"));

    private static readonly MediaFileId FileA = new(Guid.Parse("e8b10003-0000-4000-8000-00000000000a"));

    private static readonly MediaFileId FileB = new(Guid.Parse("e8b10003-0000-4000-8000-00000000000b"));

    [Fact]
    public async Task A_detected_row_round_trips_through_a_new_repository_instance()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var row = Row(FileA, MarkerKind.Intro, 12, 37, corrected: true);

        await new DetectedMarkerRepository(factory).SaveAsync(row, TestContext.Current.CancellationToken);
        var byFile = await new DetectedMarkerRepository(factory).GetForFileAsync(
            FileA,
            TestContext.Current.CancellationToken);
        var bySeries = await new DetectedMarkerRepository(factory).GetForSeriesAsync(
            Series,
            TestContext.Current.CancellationToken);

        Assert.Equal([row], byFile);
        Assert.Equal([row], bySeries);
        Assert.Equal(row, await new DetectedMarkerRepository(factory).GetAsync(
            row.Id,
            TestContext.Current.CancellationToken));
        Assert.Null(await new DetectedMarkerRepository(factory).GetAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Saving_the_same_identifier_edits_the_row_instead_of_duplicating_it()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new DetectedMarkerRepository(factory);
        var row = Row(FileA, MarkerKind.Intro, 12, 37, corrected: false);

        await repository.SaveAsync(row, TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            row with { Start = TimeSpan.FromSeconds(14), UserCorrected = true },
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(
            await repository.GetForFileAsync(FileA, TestContext.Current.CancellationToken));
        Assert.Equal(TimeSpan.FromSeconds(14), stored.Start);
        Assert.True(stored.UserCorrected);
    }

    [Fact]
    public async Task Replacing_a_series_swaps_its_rows_atomically_and_leaves_other_series_alone()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new DetectedMarkerRepository(factory);
        var stale = Row(FileA, MarkerKind.Intro, 10, 35, corrected: false);
        var kept = Row(FileB, MarkerKind.Credits, 150, 180, corrected: true);
        var foreign = Row(FileB, MarkerKind.Intro, 9, 30, corrected: false, series: OtherSeries);
        await repository.SaveAsync(stale, TestContext.Current.CancellationToken);
        await repository.SaveAsync(kept, TestContext.Current.CancellationToken);
        await repository.SaveAsync(foreign, TestContext.Current.CancellationToken);

        var replacement = Row(FileA, MarkerKind.Intro, 11, 36, corrected: false);
        await repository.ReplaceForSeriesAsync(
            Series,
            [kept, replacement],
            TestContext.Current.CancellationToken);

        var series = await repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken);
        Assert.Equal(2, series.Count);
        Assert.Contains(kept, series);
        Assert.Contains(replacement, series);
        Assert.DoesNotContain(series, row => row.Id == stale.Id);
        Assert.Equal(
            [foreign],
            await repository.GetForSeriesAsync(OtherSeries, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deleting_a_row_removes_it_and_nothing_else()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new DetectedMarkerRepository(factory);
        var intro = Row(FileA, MarkerKind.Intro, 10, 35, corrected: false);
        var credits = Row(FileA, MarkerKind.Credits, 150, 180, corrected: false);
        await repository.SaveAsync(intro, TestContext.Current.CancellationToken);
        await repository.SaveAsync(credits, TestContext.Current.CancellationToken);

        await repository.DeleteAsync(intro.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            [credits],
            await repository.GetForFileAsync(FileA, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_repository_refuses_to_exist_or_write_half_armed()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new DetectedMarkerRepository(null!));

        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new DetectedMarkerRepository(factory);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => repository.SaveAsync(null!, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => repository.ReplaceForSeriesAsync(Series, null!, TestContext.Current.CancellationToken));
    }

    private static DetectedMarker Row(
        MediaFileId file,
        MarkerKind kind,
        double startSeconds,
        double endSeconds,
        bool corrected,
        SeriesId? series = null) =>
        new(
            Guid.NewGuid(),
            series ?? Series,
            file,
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            Confidence: 0.8,
            DetectorVersion: 1,
            UserCorrected: corrected);
}
