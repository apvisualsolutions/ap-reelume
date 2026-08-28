// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Catalog;

/// <summary>
/// A folder of episodes scanned into a real catalogue comes out as a series, not as a wall of loose
/// cards.
/// </summary>
/// <remarks>
/// <para>
/// Written against the layout the owner actually put on the disk on 2026-08-25 — two shows, one with
/// several seasons and dozens of episodes, under folders named the way anybody names them — because
/// that is the report: «se muestran todos los capítulos sueltos en la biblioteca». Ninety-nine files
/// went in and ninety-nine cards came out.
/// </para>
/// <para>
/// The whole chain is here rather than mocked, and that is the point: the scan writes the media
/// files, the use case reads them back through the same repository the application uses, the
/// catalogue is a real SQLite file with the real migrations on it, and what is asserted is the same
/// query the library grid runs.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ScanSeriesGroupingTests
{
    [Fact]
    public async Task Two_folders_of_episodes_become_two_cards_and_not_ninety_nine()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaRoot = Path.Combine(directory.Path, "media");

        // Eight seasons of one and three of the other, at the sizes reported.
        await SeedShowAsync(mediaRoot, "Juego de Tronos", seasons: 8, episodesPerSeason: 9);
        await SeedShowAsync(mediaRoot, "La Casa del Dragon", seasons: 3, episodesPerSeason: 9);

        // And one film in the same root, because a series rule that swallowed films would be a worse
        // defect than the one it fixes.
        Directory.CreateDirectory(Path.Combine(mediaRoot, "Cine"));
        await File.WriteAllBytesAsync(
            Path.Combine(mediaRoot, "Cine", "El Faro de Piedra 2019.mkv"),
            [0x41],
            TestContext.Current.CancellationToken);

        var (roots, root, mediaFiles, catalog, summary) = await ScanAsync(directory, mediaRoot);
        var grouping = new GroupScannedEpisodes(roots, mediaFiles, catalog, new MediaNameParser());

        var result = await grouping.ExecuteAsync(summary, TestContext.Current.CancellationToken);

        // And the naming pass that follows it in the real chain, which is what gives the film its
        // name. Run here rather than assumed, because what this test is about is what comes out of a
        // scan of a real folder — and in the assembled application these two run one after the other.
        var naming = new NameScannedTitles(roots, mediaFiles, new MediaNameParser());
        var named = await naming.ExecuteAsync(summary, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.SeriesCount);
        Assert.Equal((8 * 9) + (3 * 9), result.EpisodeCount);

        // One file of the ninety-nine is not an episode, and it is the only one this pass renames.
        Assert.Equal(new NameScannedTitlesResult(1, 1), named);

        // The grid's own query, which is what the report was about.
        var page = await catalog.QueryAsync(
            new CatalogQuery(PageSize: 200),
            TestContext.Current.CancellationToken);
        var shows = page.Items.Where(item => item.Kind == CatalogTitleKind.Show).ToArray();
        Assert.Equal(2, shows.Length);
        Assert.Equal(
            ["Juego de Tronos", "La Casa del Dragon"],
            shows.Select(show => show.Title).Order(StringComparer.Ordinal));

        // Three cards in total: the two shows and the film. Not ninety-nine.
        //
        // The film is called «El Faro de Piedra» and its year is 2019, which is the correction this
        // assertion was written to wait for. It used to read `item.Title == "El Faro de Piedra 2019"`
        // — the file name verbatim, year and all, with the year column empty beside it — asserted on
        // purpose so that changing it would be a decision rather than a surprise. This is that
        // decision, taken on 2026-08-28: the parser that already reads these names for the review
        // inbox, for version grouping and for the series above now reads them for the card too.
        Assert.Equal(3, page.Items.Count);
        var film = Assert.Single(page.Items, item => item.Kind == CatalogTitleKind.Unidentified);
        Assert.Equal("El Faro de Piedra", film.Title);
        Assert.Equal(2019, film.Year);

        // And the count each card writes under its cover.
        var thrones = shows.Single(show => show.Title == "Juego de Tronos");
        Assert.Equal(8 * 9, thrones.EpisodeCount);

        // The seasons and the file behind every episode, which is what the series card lists and
        // what the next-episode countdown chains through. An episode with no file is returned marked
        // as not playable, so this is also the assertion that episode_media was written at all.
        var episodes = await new EpisodeSequenceRepository(new SqliteConnectionFactory(directory.DatabasePath))
            .GetSeriesAsync(thrones.Id, TestContext.Current.CancellationToken);
        Assert.Equal(8 * 9, episodes.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], episodes.Select(entry => entry.SeasonNumber).Distinct());
        Assert.All(episodes, entry => Assert.True(entry.IsPlayable, "an episode came back with no file behind it"));

        _ = root;
    }

    /// <summary>
    /// Scanned twice, the same folder writes the same rows: it does not double a season.
    /// </summary>
    /// <remarks>
    /// Every identifier is derived from the series key and the numbers rather than made up, which is
    /// what makes a second scan an update instead of a second show. It is asserted rather than argued
    /// because a scan runs on a watcher and this folder will be scanned many times.
    /// </remarks>
    [Fact]
    public async Task A_second_scan_of_the_same_folder_adds_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaRoot = Path.Combine(directory.Path, "media");
        await SeedShowAsync(mediaRoot, "Puerto Sombra", seasons: 2, episodesPerSeason: 4);

        var (roots, root, mediaFiles, catalog, summary) = await ScanAsync(directory, mediaRoot);
        var grouping = new GroupScannedEpisodes(roots, mediaFiles, catalog, new MediaNameParser());
        _ = await grouping.ExecuteAsync(summary, TestContext.Current.CancellationToken);

        var rescan = await new ScanCoordinator(
            roots,
            mediaFiles,
            new MediaFileEnumerator(),
            new StubProbe(),
            new InProcessApplicationEventPublisher()).StartAsync(
                new StartScanCommand(root.Id, ScanTrigger.Manual, 16),
                TestContext.Current.CancellationToken);
        var second = await grouping.ExecuteAsync(rescan, TestContext.Current.CancellationToken);

        Assert.Equal(new GroupScannedEpisodesResult(1, 8), second);
        var page = await catalog.QueryAsync(
            new CatalogQuery(PageSize: 200),
            TestContext.Current.CancellationToken);
        Assert.Single(page.Items);
        Assert.Equal(8, page.Items[0].EpisodeCount);
    }

    /// <summary>
    /// A cancelled scan writes nothing, which is the arm every one of these use cases carries.
    /// </summary>
    [Fact]
    public async Task A_cancelled_scan_assembles_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaRoot = Path.Combine(directory.Path, "media");
        await SeedShowAsync(mediaRoot, "Astillero", seasons: 1, episodesPerSeason: 2);

        var (roots, root, mediaFiles, catalog, summary) = await ScanAsync(directory, mediaRoot);
        var grouping = new GroupScannedEpisodes(roots, mediaFiles, catalog, new MediaNameParser());

        var cancelled = summary with { IsCancelled = true };
        Assert.Equal(
            new GroupScannedEpisodesResult(0, 0),
            await grouping.ExecuteAsync(cancelled, TestContext.Current.CancellationToken));

        // And a summary about a root nobody has: the other early return, which is what a scan of a
        // folder somebody removed mid-run looks like.
        Assert.Equal(
            new GroupScannedEpisodesResult(0, 0),
            await grouping.ExecuteAsync(
                summary with { RootId = new LibraryRootId(Guid.NewGuid()) },
                TestContext.Current.CancellationToken));
        _ = root;
    }

    private static async Task SeedShowAsync(
        string mediaRoot,
        string show,
        int seasons,
        int episodesPerSeason)
    {
        for (var season = 1; season <= seasons; season++)
        {
            var folder = Path.Combine(
                mediaRoot,
                show,
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Temporada {season}"));
            Directory.CreateDirectory(folder);
            for (var episode = 1; episode <= episodesPerSeason; episode++)
            {
                var name = string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{show}.S{season:D2}E{episode:D2}.1080p.mkv");
                await File.WriteAllBytesAsync(
                    Path.Combine(folder, name),
                    [(byte)season, (byte)episode]);
            }
        }
    }

    private static async Task<(
        LibraryRootRepository Roots,
        LibraryRoot Root,
        MediaFileRepository MediaFiles,
        CatalogRepository Catalog,
        ScanSummary Summary)> ScanAsync(DatabaseTestDirectory directory, string mediaRoot)
    {
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            mediaRoot,
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);
        await roots.AddAsync(root, TestContext.Current.CancellationToken);
        var mediaFiles = new MediaFileRepository(factory);
        var summary = await new ScanCoordinator(
            roots,
            mediaFiles,
            new MediaFileEnumerator(),
            new StubProbe(),
            new InProcessApplicationEventPublisher()).StartAsync(
                new StartScanCommand(root.Id, ScanTrigger.Initial, 16),
                TestContext.Current.CancellationToken);
        return (roots, root, mediaFiles, new CatalogRepository(factory), summary);
    }

    private sealed class StubProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = path;
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(53),
                "mkv",
                ["h264"],
                ["aac"],
                1920,
                1080));
        }
    }
}
