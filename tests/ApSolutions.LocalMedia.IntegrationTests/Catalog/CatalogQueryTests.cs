// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Catalog;

public sealed class CatalogQueryTests
{
    [Fact]
    public void Scanned_media_projection_contract_is_explicit_and_keeps_identification_out_of_I1()
    {
        Assert.True(Enum.GetNames<CatalogTitleKind>().Contains("Unidentified", StringComparer.Ordinal));
    }

    [Fact]
    public async Task Scanned_media_batch_is_searchable_by_filename_without_indexing_its_private_path()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var rootId = new LibraryRootId(Guid.NewGuid());
        var mediaId = new MediaFileId(Guid.NewGuid());
        var media = new MediaFile(
            mediaId,
            rootId,
            @"C:\Users\Alice\Videos\Cronicas del Norte - S01E01.mkv",
            1_024,
            DateTimeOffset.UnixEpoch,
            new TechnicalMetadata(TimeSpan.FromMinutes(62), "matroska", ["h264"], ["aac"], 1920, 1080));

        var mediaRepository = new MediaFileRepository(fixture.Factory);
        await mediaRepository.UpsertBatchAsync(
            [media],
            TestContext.Current.CancellationToken);

        var page = await fixture.Repository.QueryAsync(
            new CatalogQuery(Search: "norte", PageSize: 10),
            TestContext.Current.CancellationToken);
        var item = Assert.Single(page.Items);
        Assert.Equal(new TitleId(mediaId.Value), item.Id);
        Assert.Equal(CatalogTitleKind.Unidentified, item.Kind);
        Assert.Equal("Cronicas del Norte - S01E01", item.Title);

        await using var connection = await fixture.Factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT group_concat(primary_title, ' ') FROM catalog_fts;";
        var indexedText = Convert.ToString(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.DoesNotContain(@"C:\Users\Alice\Videos", indexedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".mkv", indexedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Catalog_slice_owns_hierarchy_query_contract_repository_and_FTS5_schema()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));

        Assert.NotNull(RequireType(
            "ApSolutions.LocalMedia.Domain.Catalog.ICatalogRepository",
            "ApSolutions.LocalMedia.Domain"));
        Assert.NotNull(RequireType(
            "ApSolutions.LocalMedia.Application.Catalog.ICatalogQueryService",
            "ApSolutions.LocalMedia.Application"));
        Assert.NotNull(RequireType(
            "ApSolutions.LocalMedia.Infrastructure.Data.Repositories.CatalogRepository",
            "ApSolutions.LocalMedia.Infrastructure"));

        await using var connection = await DatabaseTestHarness.OpenAsync(factory);
        var objects = await ReadSchemaObjectsAsync(connection);
        Assert.Contains("titles", objects);
        Assert.Contains("seasons", objects);
        Assert.Contains("episodes", objects);
        Assert.Contains("catalog_fts", objects);
    }

    [Fact]
    public async Task Movie_show_season_and_episode_are_persisted_as_a_normalized_hierarchy()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var movie = Title(1, CatalogTitleKind.Movie, "Arrival", 2016);
        var show = Title(2, CatalogTitleKind.Show, "Dark", 2017);

        await fixture.Repository.UpsertTitleAsync(movie, TestContext.Current.CancellationToken);
        await fixture.Repository.UpsertTitleAsync(show, TestContext.Current.CancellationToken);
        await fixture.Repository.UpsertSeasonAsync(
            new CatalogSeason(show.Id, 1, "Temporada 1"),
            TestContext.Current.CancellationToken);
        await fixture.Repository.UpsertEpisodeAsync(
            new CatalogEpisode(
                new EpisodeId(Guid.NewGuid()),
                show.Id,
                SeasonNumber: 1,
                EpisodeNumber: 1,
                AbsoluteNumber: 1,
                Title: "Secretos",
                SortOrder: 1,
                IsAvailable: true),
            TestContext.Current.CancellationToken);

        var page = await fixture.Repository.QueryAsync(
            new CatalogQuery(PageSize: 10),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Arrival", "Dark"], page.Items.Select(item => item.Title));
        Assert.Equal(1, await CountAsync(fixture.Factory, "seasons"));
        Assert.Equal(1, await CountAsync(fixture.Factory, "episodes"));
    }

    [Theory]
    [InlineData("amelie")]
    [InlineData("fabuloso")]
    [InlineData("tautou")]
    [InlineData("romantica")]
    public async Task FTS_finds_Unicode_alternate_titles_cast_and_genres_without_private_paths(string search)
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var title = Title(10, CatalogTitleKind.Movie, "Amélie", 2001) with
        {
            AlternateTitles = ["El fabuloso destino de Amélie Poulain"],
            Cast = ["Audrey Tautou"],
            Genres = ["Comedia romántica"],
        };
        await fixture.Repository.UpsertTitleAsync(title, TestContext.Current.CancellationToken);

        var page = await fixture.Repository.QueryAsync(
            new CatalogQuery(Search: search),
            TestContext.Current.CancellationToken);

        Assert.Equal(title.Id, Assert.Single(page.Items).Id);
        await using var connection = await fixture.Factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT group_concat(
                primary_title || ' ' || alternate_titles || ' ' || cast_names || ' ' || genres,
                ' ')
            FROM catalog_fts;
            """;
        var indexedText = Convert.ToString(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.DoesNotContain(@"C:\Users\Alice\Videos", indexedText, StringComparison.OrdinalIgnoreCase);
        var columns = await ReadFtsColumnsAsync(connection);
        Assert.DoesNotContain(columns, column => column.Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Filters_and_all_sorts_are_explicit_and_composable()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var titles = new[]
        {
            Title(20, CatalogTitleKind.Movie, "Zulu", 2020) with
            {
                HasProgress = true,
                IsPersonal = true,
                LastPlayedUtc = DateTimeOffset.UnixEpoch.AddDays(5),
            },
            Title(21, CatalogTitleKind.Movie, "Alpha", 1990) with { IsAvailable = false },
            Title(22, CatalogTitleKind.Show, "Élite", 2018) with
            {
                SortTitle = "Elite",
                HasProgress = true,
                LastPlayedUtc = DateTimeOffset.UnixEpoch.AddDays(2),
            },
            Title(23, CatalogTitleKind.Show, "Beta", 2024) with { IsPersonal = true },
        };
        foreach (var title in titles)
        {
            await fixture.Repository.UpsertTitleAsync(title, TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            ["Zulu"],
            (await QueryAsync(CatalogFilter.Movie | CatalogFilter.Available | CatalogFilter.Progress)).Select(ItemTitle));
        Assert.Equal(
            ["Beta"],
            (await QueryAsync(CatalogFilter.Show | CatalogFilter.Personal)).Select(ItemTitle));
        Assert.Equal(
            ["Alpha", "Beta", "Élite", "Zulu"],
            (await fixture.Repository.QueryAsync(
                new CatalogQuery(Sort: CatalogSort.Title, PageSize: 10),
                TestContext.Current.CancellationToken)).Items.Select(ItemTitle));
        Assert.Equal(
            ["Beta", "Zulu", "Élite", "Alpha"],
            (await fixture.Repository.QueryAsync(
                new CatalogQuery(Sort: CatalogSort.Year, PageSize: 10, Descending: true),
                TestContext.Current.CancellationToken)).Items.Select(ItemTitle));
        Assert.Equal(
            ["Zulu", "Alpha", "Élite", "Beta"],
            (await fixture.Repository.QueryAsync(
                new CatalogQuery(Sort: CatalogSort.Added, PageSize: 10),
                TestContext.Current.CancellationToken)).Items.Select(ItemTitle));
        Assert.Equal(
            ["Zulu", "Élite", "Beta", "Alpha"],
            (await fixture.Repository.QueryAsync(
                new CatalogQuery(Sort: CatalogSort.LastPlayed, PageSize: 10, Descending: true),
                TestContext.Current.CancellationToken)).Items.Select(ItemTitle));

        async Task<IReadOnlyList<CatalogItem>> QueryAsync(CatalogFilter filters) =>
            (await fixture.Repository.QueryAsync(
                new CatalogQuery(Filters: filters, PageSize: 10),
                TestContext.Current.CancellationToken)).Items;

        static string ItemTitle(CatalogItem item) => item.Title;
    }

    [Fact]
    public async Task Keyset_cursor_returns_one_hundred_pages_without_duplicates()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        for (var index = 0; index < 300; index++)
        {
            await fixture.Repository.UpsertTitleAsync(
                Title(index + 1_000, CatalogTitleKind.Movie, $"Title {index:D3}", 2000 + (index % 20)),
                TestContext.Current.CancellationToken);
        }

        var ids = new HashSet<TitleId>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 100; pageNumber++)
        {
            var page = await fixture.Repository.QueryAsync(
                new CatalogQuery(Sort: CatalogSort.Title, PageSize: 3, Cursor: cursor),
                TestContext.Current.CancellationToken);
            Assert.Equal(3, page.Items.Count);
            Assert.All(page.Items, item => Assert.True(ids.Add(item.Id), $"Duplicate title {item.Id.Value:D}"));
            cursor = page.NextCursor;
        }

        Assert.Equal(300, ids.Count);
        Assert.Null(cursor);
    }

    [Fact]
    public async Task Query_honors_cancellation_before_opening_SQLite()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Repository.QueryAsync(new CatalogQuery(), cancellation.Token));
    }

    [Fact]
    public async Task Query_plan_uses_the_title_index_and_the_FTS5_virtual_index()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        await fixture.Repository.UpsertTitleAsync(
            Title(50, CatalogTitleKind.Movie, "Amélie", 2001),
            TestContext.Current.CancellationToken);
        await using var connection = await fixture.Factory.OpenAsync(TestContext.Current.CancellationToken);

        var browsePlan = await ReadQueryPlanAsync(
            connection,
            """
            EXPLAIN QUERY PLAN
            SELECT id FROM titles
            ORDER BY sort_title COLLATE NOCASE, id
            LIMIT 50;
            """);
        var searchPlan = await ReadQueryPlanAsync(
            connection,
            """
            EXPLAIN QUERY PLAN
            SELECT t.id
            FROM titles t
            WHERE t.id IN (
                SELECT title_id FROM catalog_fts WHERE catalog_fts MATCH 'amelie'
            );
            """);

        Assert.Contains(browsePlan, detail => detail.Contains("ix_titles_title", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            searchPlan,
            detail => detail.Contains("VIRTUAL TABLE INDEX", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<CatalogFixture> CreateFixtureAsync(string databasePath)
    {
        var factory = new SqliteConnectionFactory(databasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        return new CatalogFixture(factory, new CatalogRepository(factory));
    }

    private static CatalogTitle Title(int seed, CatalogTitleKind kind, string title, int year) => new(
        new TitleId(CreateGuid(seed)),
        kind,
        title,
        title,
        year,
        [],
        [],
        [],
        DateTimeOffset.UnixEpoch.AddDays(seed),
        LastPlayedUtc: null,
        HasProgress: false,
        IsPersonal: false,
        IsAvailable: true);

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static async Task<long> CountAsync(SqliteConnectionFactory factory, string table)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = table switch
        {
            "seasons" => "SELECT COUNT(*) FROM seasons;",
            "episodes" => "SELECT COUNT(*) FROM episodes;",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private static async Task<string[]> ReadFtsColumnsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(catalog_fts);";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columns = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return [.. columns];
    }

    private static async Task<string[]> ReadQueryPlanAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var details = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            details.Add(reader.GetString(3));
        }

        return [.. details];
    }

    private sealed record CatalogFixture(
        SqliteConnectionFactory Factory,
        CatalogRepository Repository);

    private static Type? RequireType(string fullName, string assemblyName) =>
        Assembly.Load(assemblyName).GetType(fullName, throwOnError: false);

    private static async Task<string[]> ReadSchemaObjectsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_schema
            WHERE name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return [.. names];
    }
}
