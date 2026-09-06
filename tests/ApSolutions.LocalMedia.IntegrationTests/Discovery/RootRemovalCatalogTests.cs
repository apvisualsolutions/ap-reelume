// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

/// <summary>
/// Removing a folder deletes its catalogue, and the owner decided so on 2026-09-06 against the
/// prototype, which promises the opposite. What these tests defend is the boundary: the rows that
/// belong to the folder go, and nothing else does.
///
/// The pair that matters is <see cref="A_title_whose_only_files_were_in_the_removed_root_leaves_with_its_marks_and_progress"/>
/// and <see cref="A_show_with_episodes_in_two_roots_survives_removing_one"/>: one probe for what must
/// go, one for what must stay. The second was expected to pass today for the wrong reason — today
/// nothing is deleted at all — and measuring said otherwise: it fails too, because it also asserts
/// that the leaving folder's own file and episode are gone. So neither half is a gate that checks
/// nothing, and both reds are archived.
/// </summary>
public sealed class RootRemovalCatalogTests
{
    [Fact]
    public async Task A_title_whose_only_files_were_in_the_removed_root_leaves_with_its_marks_and_progress()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(
            directory.DatabasePath,
            TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var root = await AddRootAsync(roots, @"C:\Only");

        var film = Guid.NewGuid();
        await SeedFilmAsync(factory, root.Id, film, "El Faro de Piedra", @"C:\Only\faro.mkv");
        await SeedWatchStateAsync(factory, film, film, TimeSpan.FromMinutes(52));
        await SeedPersonalStateAsync(factory, film, film);
        await SeedIntroMarkerAsync(factory, film);
        await SeedCatalogMetadataAsync(factory, film, "El Faro de Piedra");

        await new RemoveLibraryRoot(roots)
            .ExecuteAsync(new RemoveLibraryRootCommand(root.Id), TestContext.Current.CancellationToken);

        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM library_roots;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM media_files;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM titles;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM scanned_titles;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM watch_state;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM personal_state;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM intro_markers;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM catalog_metadata;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM scan_checkpoints;"));
    }

    [Fact]
    public async Task A_show_with_episodes_in_two_roots_survives_removing_one()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(
            directory.DatabasePath,
            TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var leaving = await AddRootAsync(roots, @"C:\Leaving");
        var staying = await AddRootAsync(roots, @"D:\Staying");

        // A show is one title with episodes in two folders. Only the leaving folder's episode may go:
        // the other one is still on a disk the person kept, so the show is still watchable.
        var show = Guid.NewGuid();
        await SeedShowAsync(factory, show, "Historias del Muelle");
        var goneFile = Guid.NewGuid();
        var keptFile = Guid.NewGuid();
        var goneEpisode = Guid.NewGuid();
        var keptEpisode = Guid.NewGuid();
        await SeedMediaFileAsync(factory, leaving.Id, goneFile, @"C:\Leaving\s01e01.mkv");
        await SeedMediaFileAsync(factory, staying.Id, keptFile, @"D:\Staying\s01e02.mkv");
        await SeedEpisodeAsync(factory, show, goneEpisode, 1, goneFile);
        await SeedEpisodeAsync(factory, show, keptEpisode, 2, keptFile);
        await SeedWatchStateAsync(factory, show, keptEpisode, TimeSpan.FromMinutes(31));
        await SeedPersonalStateAsync(factory, show, keptEpisode);

        await new RemoveLibraryRoot(roots)
            .ExecuteAsync(new RemoveLibraryRootCommand(leaving.Id), TestContext.Current.CancellationToken);

        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM titles;"));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM media_files;"));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM episodes;"));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM episode_media;"));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM seasons;"));

        // The surviving episode keeps what the person had on it, untouched.
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM watch_state;"));
        Assert.Equal(1, await CountAsync(factory, "SELECT COUNT(*) FROM personal_state;"));
        Assert.Equal(
            (long)TimeSpan.FromMinutes(31).Ticks,
            await CountAsync(factory, "SELECT position_ticks FROM watch_state;"));
    }

    [Fact]
    public async Task A_removed_root_stops_matching_a_search()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(
            directory.DatabasePath,
            TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var root = await AddRootAsync(roots, @"C:\Only");

        var film = Guid.NewGuid();
        await SeedFilmAsync(factory, root.Id, film, "La Ciudad Sumergida", @"C:\Only\ciudad.mkv");

        await new RemoveLibraryRoot(roots)
            .ExecuteAsync(new RemoveLibraryRootCommand(root.Id), TestContext.Current.CancellationToken);

        // The search index is kept by AFTER DELETE triggers on titles and scanned_titles, and those
        // do not fire for rows a cascade removed: recursive_triggers is off. So a removal that leaned
        // on the cascade would empty the tables and still answer this search with a ghost. Counting
        // rows would never notice.
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM catalog_fts;"));
        Assert.Equal(0, await CountAsync(factory, "SELECT COUNT(*) FROM scanned_catalog_fts;"));
    }

    [Fact]
    public async Task The_notice_counts_exactly_what_the_removal_then_deletes()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(
            directory.DatabasePath,
            TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var leaving = await AddRootAsync(roots, @"C:\Leaving");
        var staying = await AddRootAsync(roots, @"D:\Staying");

        var firstFilm = Guid.NewGuid();
        var secondFilm = Guid.NewGuid();
        await SeedFilmAsync(factory, leaving.Id, firstFilm, "Cartas desde Antares", @"C:\Leaving\cartas.mkv");
        await SeedFilmAsync(factory, leaving.Id, secondFilm, "La Sonda", @"C:\Leaving\sonda.mkv");
        await SeedWatchStateAsync(factory, firstFilm, firstFilm, TimeSpan.FromMinutes(110));
        await SeedPersonalStateAsync(factory, secondFilm, secondFilm);

        var show = Guid.NewGuid();
        await SeedShowAsync(factory, show, "Historias del Muelle");
        var firstEpisodeFile = Guid.NewGuid();
        var secondEpisodeFile = Guid.NewGuid();
        await SeedMediaFileAsync(factory, leaving.Id, firstEpisodeFile, @"C:\Leaving\s01e01.mkv");
        await SeedMediaFileAsync(factory, leaving.Id, secondEpisodeFile, @"C:\Leaving\s01e02.mkv");
        await SeedEpisodeAsync(factory, show, Guid.NewGuid(), 1, firstEpisodeFile);
        await SeedEpisodeAsync(factory, show, Guid.NewGuid(), 2, secondEpisodeFile);
        await SeedWatchStateAsync(factory, show, Guid.NewGuid(), TimeSpan.FromMinutes(48));
        await SeedIntroMarkerAsync(factory, show);

        // The folder that stays, so the counts cannot pass by simply totalling the database.
        var keptFilm = Guid.NewGuid();
        await SeedFilmAsync(factory, staying.Id, keptFilm, "Volver al Mar", @"D:\Staying\volver.mkv");
        await SeedWatchStateAsync(factory, keptFilm, keptFilm, TimeSpan.FromMinutes(90));

        var summary = await new SummarizeLibraryRootRemoval(new LibraryRootRemovalReader(factory))
            .ExecuteAsync(leaving.Id, TestContext.Current.CancellationToken);

        var cardsBefore = await CountAsync(factory, CatalogueCardsSql);
        var marksBefore = await CountAsync(factory, MarksSql);
        var progressBefore = await CountAsync(factory, ProgressSql);

        await new RemoveLibraryRoot(roots)
            .ExecuteAsync(new RemoveLibraryRootCommand(leaving.Id), TestContext.Current.CancellationToken);

        // The whole point: the warning's three numbers are the three the removal then takes. Comparing
        // the reader's SQL against the deleter's SQL would prove nothing — this compares the reader
        // against the catalogue before and after.
        Assert.Equal(cardsBefore - await CountAsync(factory, CatalogueCardsSql), summary.TitleCount);
        Assert.Equal(marksBefore - await CountAsync(factory, MarksSql), summary.MarkCount);
        Assert.Equal(progressBefore - await CountAsync(factory, ProgressSql), summary.Progress.Ticks);

        // And the numbers are the ones a person would recognise: two films and one show.
        Assert.Equal(3, summary.TitleCount);
        Assert.Equal(2, summary.MarkCount);
        Assert.Equal(TimeSpan.FromMinutes(158), summary.Progress);
    }

    /// <summary>
    /// What the library draws: identified titles, plus the scanned files that are neither an
    /// identified title nor an episode of one. Written out here rather than shared with the reader,
    /// so the assertion is not the reader agreeing with itself.
    /// </summary>
    private const string CatalogueCardsSql = """
        SELECT (SELECT COUNT(*) FROM titles)
             + (SELECT COUNT(*) FROM scanned_titles
                WHERE media_file_id NOT IN (SELECT id FROM titles)
                  AND media_file_id NOT IN (SELECT media_file_id FROM episode_media));
        """;

    private const string MarksSql =
        "SELECT (SELECT COUNT(*) FROM personal_state) + (SELECT COUNT(*) FROM intro_markers);";

    private const string ProgressSql = "SELECT COALESCE(SUM(position_ticks), 0) FROM watch_state;";

    private static async Task<LibraryRoot> AddRootAsync(LibraryRootRepository roots, string path)
    {
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            path,
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);
        await roots.AddAsync(root, TestContext.Current.CancellationToken);
        return root;
    }

    private static async Task SeedFilmAsync(
        SqliteConnectionFactory factory,
        LibraryRootId rootId,
        Guid id,
        string title,
        string path)
    {
        // A film's title id is its media file's id, which is what CatalogRepository writes in its own
        // comment. Seeding both shelves keeps the fixture honest about that.
        await SeedMediaFileAsync(factory, rootId, id, path);
        await ExecuteAsync(
            factory,
            """
            INSERT INTO titles (id, kind, primary_title, sort_title, release_year, added_utc,
                                last_played_utc, has_progress, is_personal, is_available)
            VALUES ($id, 0, $title, $title, 2019, '2026-01-01T00:00:00Z', NULL, 1, 0, 1);
            INSERT INTO scanned_titles (media_file_id, display_title, sort_title, added_utc)
            VALUES ($id, $title, $title, '2026-01-01T00:00:00Z');
            """,
            ("$id", id.ToString("D")),
            ("$title", title));
    }

    private static Task SeedShowAsync(SqliteConnectionFactory factory, Guid id, string title) => ExecuteAsync(
        factory,
        """
        INSERT INTO titles (id, kind, primary_title, sort_title, release_year, added_utc,
                            last_played_utc, has_progress, is_personal, is_available)
        VALUES ($id, 1, $title, $title, 2020, '2026-01-01T00:00:00Z', NULL, 1, 0, 1);
        INSERT INTO seasons (show_id, season_number, title) VALUES ($id, 1, 'Temporada 1');
        """,
        ("$id", id.ToString("D")),
        ("$title", title));

    private static Task SeedEpisodeAsync(
        SqliteConnectionFactory factory,
        Guid showId,
        Guid episodeId,
        int number,
        Guid mediaFileId) => ExecuteAsync(
        factory,
        """
        INSERT INTO episodes (id, show_id, season_number, episode_number, absolute_number, title,
                              sort_order, is_available)
        VALUES ($episodeId, $showId, 1, $number, $number, 'Episodio', $number, 1);
        INSERT INTO episode_media (episode_id, media_file_id) VALUES ($episodeId, $mediaFileId);
        """,
        ("$episodeId", episodeId.ToString("D")),
        ("$showId", showId.ToString("D")),
        ("$number", number),
        ("$mediaFileId", mediaFileId.ToString("D")));

    private static Task SeedMediaFileAsync(
        SqliteConnectionFactory factory,
        LibraryRootId rootId,
        Guid id,
        string path) => ExecuteAsync(
        factory,
        """
        INSERT INTO media_files (id, library_root_id, normalized_path, size_bytes, last_write_utc,
                                 duration_ticks, container, video_codecs, audio_codecs, width, height,
                                 is_available)
        VALUES ($id, $rootId, $path, 1024, '2026-01-01T00:00:00Z', NULL, 'mkv', 'h264', 'aac',
                NULL, NULL, 1);
        """,
        ("$id", id.ToString("D")),
        ("$rootId", rootId.Value.ToString("D")),
        ("$path", path));

    private static Task SeedWatchStateAsync(
        SqliteConnectionFactory factory,
        Guid titleId,
        Guid contentId,
        TimeSpan position) => ExecuteAsync(
        factory,
        """
        INSERT INTO watch_state (content_key, title_id, episode_id, position_ticks,
                                 observed_duration_ticks, source_media_file_id, status,
                                 is_manual_override, started_utc, updated_utc)
        VALUES ($contentKey, $titleId, NULL, $position, NULL, $contentKey, 1, 0,
                '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
        """,
        ("$contentKey", contentId.ToString("D")),
        ("$titleId", titleId.ToString("D")),
        ("$position", position.Ticks));

    private static Task SeedPersonalStateAsync(
        SqliteConnectionFactory factory,
        Guid titleId,
        Guid contentId) => ExecuteAsync(
        factory,
        """
        INSERT INTO personal_state (content_key, title_id, episode_id, is_favorite, is_watch_later,
                                    rating, updated_utc)
        VALUES ($contentKey, $titleId, NULL, 1, 0, 8, '2026-01-01T00:00:00Z');
        """,
        ("$contentKey", contentId.ToString("D")),
        ("$titleId", titleId.ToString("D")));

    private static Task SeedIntroMarkerAsync(SqliteConnectionFactory factory, Guid seriesId) => ExecuteAsync(
        factory,
        """
        INSERT INTO intro_markers (id, series_id, kind, start_ticks, end_ticks, origin, confidence,
                                   user_corrected, updated_utc)
        VALUES ($id, $seriesId, 0, 0, 6000000000, 1, NULL, 1, '2026-01-01T00:00:00Z');
        """,
        ("$id", Guid.NewGuid().ToString("D")),
        ("$seriesId", seriesId.ToString("D")));

    private static Task SeedCatalogMetadataAsync(
        SqliteConnectionFactory factory,
        Guid titleId,
        string title) => ExecuteAsync(
        factory,
        """
        INSERT INTO catalog_metadata (title_id, title, original_title, overview, release_year, genres,
                                      poster_path, backdrop_path, locked_fields, revision)
        VALUES ($titleId, $title, NULL, NULL, 2019, '', 'posters/faro.jpg', NULL, '', 1);
        """,
        ("$titleId", titleId.ToString("D")),
        ("$title", title));

    private static async Task ExecuteAsync(
        SqliteConnectionFactory factory,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountAsync(SqliteConnectionFactory factory, string sql)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
