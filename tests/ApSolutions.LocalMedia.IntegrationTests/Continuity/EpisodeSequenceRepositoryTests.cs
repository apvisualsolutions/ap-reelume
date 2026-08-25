// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Continuity;

/// <summary>
/// The episode sequence and the Home projection are the two reads the complete details need. Both are
/// answered by SQLite so a ten-thousand-file catalogue never has to be loaded into memory.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EpisodeSequenceRepositoryTests
{
    private static readonly TitleId Show = new(Guid.Parse("a1000000-0000-4000-8000-000000000001"));
    private static readonly TitleId OtherShow = new(Guid.Parse("a1000000-0000-4000-8000-000000000002"));
    private static readonly TitleId Movie = new(Guid.Parse("a1000000-0000-4000-8000-000000000003"));
    private static readonly LibraryRootId Root = new(Guid.Parse("b1000000-0000-4000-8000-000000000001"));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Episodes_are_read_in_viewing_order_with_specials_last_and_their_real_availability()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        await SeedAsync(factory);

        var episodes = await new EpisodeSequenceRepository(factory).GetSeriesAsync(
            Show,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [(1, 1), (1, 2), (1, 3), (2, 1), (0, 1)],
            NextEpisodePolicy.Order(episodes)
                .Select(entry => (entry.SeasonNumber, entry.EpisodeNumber)));
        var linked = episodes.Single(entry => entry is { SeasonNumber: 1, EpisodeNumber: 1 });
        Assert.NotNull(linked.MediaFileId);
        Assert.False(string.IsNullOrWhiteSpace(linked.Path));
        Assert.True(linked.IsPlayable);

        var offline = episodes.Single(entry => entry is { SeasonNumber: 1, EpisodeNumber: 2 });
        Assert.False(offline.IsAvailable);
        Assert.False(offline.IsPlayable);

        var withoutFile = episodes.Single(entry => entry is { SeasonNumber: 1, EpisodeNumber: 3 });
        Assert.Null(withoutFile.MediaFileId);
        Assert.False(withoutFile.IsPlayable);

        // The name and the length, which the series card writes under the number. They joined the
        // projection on 2026-08-25 and both halves are here: the episode with a file carries the
        // file's running time, and the one with none carries no running time at all — which is a
        // different absence from a running time of zero, and the card draws them differently.
        Assert.Equal("Crónicas S01E01", linked.Title);
        Assert.Equal(TimeSpan.FromMinutes(48), linked.Runtime);
        Assert.Equal("Crónicas S01E03", withoutFile.Title);
        Assert.Null(withoutFile.Runtime);
        Assert.Null(offline.Runtime);
    }

    /// <summary>
    /// Every one of these repositories refuses to exist without a store, which is the guard that
    /// turns a composition mistake into a failure at start-up rather than into a query at midnight.
    /// </summary>
    [Fact]
    public void A_repository_over_no_store_refuses_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new EpisodeSequenceRepository(null!));
        Assert.Throws<ArgumentNullException>(() => new IntroMarkerRepository(null!));
    }

    [Fact]
    public async Task One_series_never_returns_the_episodes_of_another()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        await SeedAsync(factory);
        var repository = new EpisodeSequenceRepository(factory);

        var other = await repository.GetSeriesAsync(OtherShow, TestContext.Current.CancellationToken);

        Assert.Single(other);
        Assert.All(other, entry => Assert.Equal(OtherShow, entry.ShowId));
        Assert.Empty(await repository.GetSeriesAsync(Movie, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_single_episode_can_be_revalidated_and_an_unknown_one_returns_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        await SeedAsync(factory);
        var repository = new EpisodeSequenceRepository(factory);
        var known = new EpisodeId(EpisodeGuid(1, 1));

        var entry = await repository.GetAsync(known, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        Assert.Equal(known, entry.Id);
        Assert.True(entry.IsPlayable);

        Assert.Null(await repository.GetAsync(
            new EpisodeId(Guid.Parse("c1000000-0000-4000-8000-00000000ffff")),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_home_projection_reads_progress_recent_additions_and_the_summary()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        await SeedAsync(factory);
        var watchStates = new WatchStateRepository(factory);
        await watchStates.SaveAsync(
            Progress(ContentKey.ForEpisode(Show, new EpisodeId(EpisodeGuid(1, 1))), Noon),
            TestContext.Current.CancellationToken);
        await watchStates.SaveAsync(
            Progress(ContentKey.ForTitle(Movie), Noon.AddHours(-4)),
            TestContext.Current.CancellationToken);
        var readModel = new HomeReadModel(factory);

        var progress = await readModel.ReadProgressAsync(10, TestContext.Current.CancellationToken);
        Assert.Equal(2, progress.Count);
        var first = progress[0];
        Assert.Equal(ContentKey.ForEpisode(Show, new EpisodeId(EpisodeGuid(1, 1))), first.Content);
        Assert.Equal(CatalogTitleKind.Show, first.Kind);
        Assert.Equal(1, first.SeasonNumber);
        Assert.Equal(1, first.EpisodeNumber);
        Assert.Equal("Crónicas S01E01", first.EpisodeTitle);
        Assert.True(first.IsAvailable);
        Assert.Equal(CatalogTitleKind.Movie, progress[1].Kind);
        Assert.Null(progress[1].SeasonNumber);

        // What the hero's line is written from, and both absences: the series has genres and no
        // year, the film has a year and no genres.
        Assert.Null(first.Year);
        Assert.Equal(["Drama", "Intriga"], first.Genres);
        Assert.Equal(2016, progress[1].Year);
        Assert.Empty(progress[1].Genres ?? []);

        var recent = await readModel.ReadRecentlyAddedAsync(2, TestContext.Current.CancellationToken);
        Assert.Equal(2, recent.Count);
        Assert.True(recent[0].AddedUtc >= recent[1].AddedUtc);

        var summary = await readModel.ReadLibrarySummaryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, summary.MovieCount);
        Assert.Equal(2, summary.ShowCount);
        Assert.Equal(0, summary.UnavailableCount);
    }

    [Fact]
    public async Task The_home_projection_honours_its_limits_and_survives_an_empty_catalogue()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var readModel = new HomeReadModel(factory);

        Assert.Empty(await readModel.ReadProgressAsync(10, TestContext.Current.CancellationToken));
        Assert.Empty(await readModel.ReadRecentlyAddedAsync(10, TestContext.Current.CancellationToken));
        Assert.Equal(
            new LibrarySummary(0, 0, 0),
            await readModel.ReadLibrarySummaryAsync(TestContext.Current.CancellationToken));

        await SeedAsync(factory);
        Assert.Single(await readModel.ReadRecentlyAddedAsync(1, TestContext.Current.CancellationToken));
    }

    private static WatchState Progress(ContentKey content, DateTimeOffset updatedUtc) => new()
    {
        Content = content,
        Position = TimeSpan.FromMinutes(20),
        ObservedDuration = TimeSpan.FromMinutes(50),
        SourceMediaFileId = new MediaFileId(Guid.Parse("d1000000-0000-4000-8000-000000000001")),
        Status = WatchStatus.InProgress,
        IsManualOverride = false,
        StartedUtc = updatedUtc.AddHours(-1),
        UpdatedUtc = updatedUtc,
    };

    private static async Task SeedAsync(SqliteConnectionFactory factory)
    {
        var catalog = new CatalogRepository(factory);
        // The series carries genres and no year; the film carries a year and no genres. Both halves
        // of both absences, in one seed, because Home's projection reads them as two nullable
        // columns and a fixture that filled them in would only ever measure one side of each.
        await catalog.UpsertTitleAsync(Title(
            Show,
            CatalogTitleKind.Show,
            "Crónicas",
            Noon.AddDays(-1),
            year: null,
            genres: ["Drama", "Intriga"]));
        await catalog.UpsertTitleAsync(Title(OtherShow, CatalogTitleKind.Show, "Otra serie", Noon.AddDays(-2)));
        await catalog.UpsertTitleAsync(Title(Movie, CatalogTitleKind.Movie, "Arrival", Noon));
        await catalog.UpsertSeasonAsync(new CatalogSeason(Show, 0, "Especiales"));
        await catalog.UpsertSeasonAsync(new CatalogSeason(Show, 1, "Temporada 1"));
        await catalog.UpsertSeasonAsync(new CatalogSeason(Show, 2, "Temporada 2"));
        await catalog.UpsertSeasonAsync(new CatalogSeason(OtherShow, 1, "Temporada 1"));

        await catalog.UpsertEpisodeAsync(Episode(Show, 1, 1, true));
        await catalog.UpsertEpisodeAsync(Episode(Show, 1, 2, false));
        await catalog.UpsertEpisodeAsync(Episode(Show, 1, 3, true));
        await catalog.UpsertEpisodeAsync(Episode(Show, 2, 1, true));
        await catalog.UpsertEpisodeAsync(Episode(Show, 0, 1, true));
        await catalog.UpsertEpisodeAsync(Episode(OtherShow, 1, 1, true));

        await using var connection = await factory.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
            VALUES ($root, 'root', 0, 0, 7)
            ON CONFLICT(id) DO NOTHING;
            """,
            ("$root", Root.Value.ToString("D")));

        // Season one episodes one and two have a file; episode three deliberately has none. Only the
        // first carries a running time, because a file whose duration was never read is the ordinary
        // state of a library that has not been scanned deeply — and the card has to survive it.
        await LinkAsync(connection, Show, 1, 1, isAvailable: true, duration: TimeSpan.FromMinutes(48));
        await LinkAsync(connection, Show, 1, 2, isAvailable: false);
        await LinkAsync(connection, Show, 2, 1, isAvailable: true);
        await LinkAsync(connection, Show, 0, 1, isAvailable: true);
        await LinkAsync(connection, OtherShow, 1, 1, isAvailable: true);
    }

    private static async Task LinkAsync(
        SqliteConnection connection,
        TitleId show,
        int season,
        int number,
        bool isAvailable,
        TimeSpan? duration = null)
    {
        var mediaFileId = MediaGuid(show, season, number);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO media_files (
                id, library_root_id, normalized_path, size_bytes, last_write_utc,
                duration_ticks, container, video_codecs, audio_codecs, width, height, is_available)
            VALUES (
                $id, $root, $path, 1000, $written,
                $duration, 'matroska', '["h264"]', '["aac"]', 1920, 1080, $available);
            """,
            ("$id", mediaFileId.ToString("D")),
            ("$root", Root.Value.ToString("D")),
            ("$path", $@"root\{show.Value:N}-s{season:D2}e{number:D2}.mkv"),
            ("$written", Noon.ToString("O", CultureInfo.InvariantCulture)),
            ("$available", isAvailable ? 1 : 0),
            ("$duration", duration is { } span ? span.Ticks : DBNull.Value));
        await ExecuteAsync(
            connection,
            """
            INSERT INTO episode_media (episode_id, media_file_id)
            VALUES ($episode, $media);
            """,
            ("$episode", EpisodeGuid(season, number, show).ToString("D")),
            ("$media", mediaFileId.ToString("D")));
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// A title as the catalogue stores it. The year and the genres are arguments because Home's
    /// projection reads both since 2026-08-24 and both can be absent: a title nobody identified has
    /// no year, and one identified from a file name has no genres. The hero writes «2019 · Drama ·
    /// quedan 44:00» out of them, so each absence is a line that comes out shorter rather than a
    /// line that comes out wrong.
    /// </summary>
    private static CatalogTitle Title(
        TitleId id,
        CatalogTitleKind kind,
        string title,
        DateTimeOffset addedUtc,
        int? year = 2016,
        IReadOnlyList<string>? genres = null) => new(
        id,
        kind,
        title,
        title,
        year,
        [],
        [],
        genres ?? [],
        addedUtc,
        null,
        HasProgress: false,
        IsPersonal: false,
        IsAvailable: true);

    private static CatalogEpisode Episode(TitleId show, int season, int number, bool isAvailable) => new(
        new EpisodeId(EpisodeGuid(season, number, show)),
        show,
        season,
        number,
        (season * 100) + number,
        $"Crónicas S{season:D2}E{number:D2}",
        (season * 100) + number,
        isAvailable);

    private static Guid EpisodeGuid(int season, int number) => EpisodeGuid(season, number, Show);

    private static Guid EpisodeGuid(int season, int number, TitleId show) =>
        Derive(show, 0xE0, season, number);

    private static Guid MediaGuid(TitleId show, int season, int number) =>
        Derive(show, 0xF0, season, number);

    /// <summary>
    /// Keeps the show's own discriminator, so two series never derive the same episode or file
    /// identifier from the same season and number.
    /// </summary>
    private static Guid Derive(TitleId show, byte marker, int season, int number)
    {
        var bytes = show.Value.ToByteArray();
        bytes[13] = bytes[15];
        bytes[14] = marker;
        bytes[15] = (byte)((season * 10) + number);
        return new Guid(bytes);
    }
}
