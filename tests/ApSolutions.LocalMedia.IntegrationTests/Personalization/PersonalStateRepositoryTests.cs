// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Personalization;

/// <summary>
/// Personal data must survive a restart, filter the catalogue, and hold up under a randomized
/// workload run against a reference model.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PersonalStateRepositoryTests
{
    private static readonly LibraryRootId Root = new(Guid.Parse("c2000000-0000-4000-8000-000000000001"));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_marked_item_round_trips_through_a_new_repository_instance()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var state = PersonalState.Empty(Content(1)).WithFavorite(true).WithWatchLater(true).WithRating(5);

        await new PersonalStateRepository(factory).SaveAsync(state, Noon, TestContext.Current.CancellationToken);
        var restored = await new PersonalStateRepository(factory).GetAsync(
            Content(1),
            TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.Equal(state, restored);
    }

    [Fact]
    public async Task Saving_the_same_content_twice_edits_the_row_instead_of_duplicating_it()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PersonalStateRepository(factory);

        await repository.SaveAsync(
            PersonalState.Empty(Content(2)).WithFavorite(true),
            Noon,
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            PersonalState.Empty(Content(2)).WithRating(2),
            Noon,
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(await repository.GetAllAsync(TestContext.Current.CancellationToken));
        Assert.False(stored.IsFavorite);
        Assert.Equal(2, stored.Rating);
    }

    [Fact]
    public async Task Deleting_removes_the_row_and_deleting_an_unknown_key_is_not_a_failure()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PersonalStateRepository(factory);
        await repository.SaveAsync(
            PersonalState.Empty(Content(3)).WithFavorite(true),
            Noon,
            TestContext.Current.CancellationToken);

        await repository.DeleteAsync(Content(3), TestContext.Current.CancellationToken);
        await repository.DeleteAsync(Content(99), TestContext.Current.CancellationToken);

        Assert.Empty(await repository.GetAllAsync(TestContext.Current.CancellationToken));
        Assert.Null(await repository.GetAsync(Content(3), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_episode_and_its_show_hold_separate_rows()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PersonalStateRepository(factory);
        var show = new TitleId(Guid.Parse("c2000000-0000-4000-8000-000000000010"));
        var episode = new EpisodeId(Guid.Parse("c2000000-0000-4000-8000-000000000011"));

        await repository.SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(show)).WithFavorite(true),
            Noon,
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            PersonalState.Empty(ContentKey.ForEpisode(show, episode)).WithRating(5),
            Noon,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, (await repository.GetAllAsync(TestContext.Current.CancellationToken)).Count);
        var stored = await repository.GetAsync(
            ContentKey.ForEpisode(show, episode),
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(episode, stored.Content.EpisodeId);
        Assert.Equal(5, stored.Rating);
    }

    [Fact]
    public async Task The_schema_refuses_a_rating_outside_one_to_ten()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO personal_state (
                content_key, title_id, episode_id, is_favorite, is_watch_later, rating, updated_utc)
            VALUES ('title:bad', 'bad', NULL, 0, 0, 42, '2026-08-03T12:00:00.0000000+00:00');
            """;

        await Assert.ThrowsAsync<SqliteException>(
            () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_catalog_filters_by_favorite_watch_later_and_rated_without_hiding_anything_else()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var catalog = new CatalogRepository(factory);
        var favorite = new TitleId(Guid.Parse("c2000000-0000-4000-8000-000000000021"));
        var later = new TitleId(Guid.Parse("c2000000-0000-4000-8000-000000000022"));
        var rated = new TitleId(Guid.Parse("c2000000-0000-4000-8000-000000000023"));
        var plain = new TitleId(Guid.Parse("c2000000-0000-4000-8000-000000000024"));
        foreach (var (id, title) in new[]
        {
            (favorite, "Alfa"), (later, "Beta"), (rated, "Gamma"), (plain, "Delta"),
        })
        {
            await catalog.UpsertTitleAsync(Title(id, title), TestContext.Current.CancellationToken);
        }

        var personal = new PersonalStateRepository(factory);
        await personal.SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(favorite)).WithFavorite(true),
            Noon,
            TestContext.Current.CancellationToken);
        await personal.SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(later)).WithWatchLater(true),
            Noon,
            TestContext.Current.CancellationToken);
        await personal.SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(rated)).WithRating(4),
            Noon,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [favorite],
            await QueryIdsAsync(catalog, CatalogFilter.Favorite));
        Assert.Equal(
            [later],
            await QueryIdsAsync(catalog, CatalogFilter.WatchLater));
        Assert.Equal(
            [rated],
            await QueryIdsAsync(catalog, CatalogFilter.Rated));
        Assert.Equal(4, (await QueryIdsAsync(catalog, CatalogFilter.None)).Count);
    }

    [Fact]
    public async Task An_episode_mark_never_makes_its_show_appear_under_a_title_filter()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var catalog = new CatalogRepository(factory);
        var show = new TitleId(Guid.Parse("c2000000-0000-4000-8000-000000000031"));
        await catalog.UpsertTitleAsync(Title(show, "Crónicas"), TestContext.Current.CancellationToken);
        await new PersonalStateRepository(factory).SaveAsync(
            PersonalState.Empty(ContentKey.ForEpisode(
                show,
                new EpisodeId(Guid.Parse("c2000000-0000-4000-8000-000000000032")))).WithFavorite(true),
            Noon,
            TestContext.Current.CancellationToken);

        Assert.Empty(await QueryIdsAsync(catalog, CatalogFilter.Favorite));
    }

    [Fact]
    public async Task A_thousand_random_changes_survive_a_restart_and_match_a_reference_model()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PersonalStateRepository(factory);
        var reference = new Dictionary<string, PersonalState>(StringComparer.Ordinal);
        var random = new Random(20260803);

        for (var index = 0; index < 1_000; index++)
        {
            var content = Content(random.Next(1, 60));
            var current = reference.GetValueOrDefault(content.Value, PersonalState.Empty(content));
            var next = random.Next(4) switch
            {
                0 => current.WithFavorite(random.Next(2) == 1),
                1 => current.WithWatchLater(random.Next(2) == 1),
                2 => current.WithRating(random.Next(
                    PersonalStatePolicy.MinimumRating,
                    PersonalStatePolicy.MaximumRating + 1)),
                _ => current.WithRating(null),
            };

            if (next.IsEmpty)
            {
                await repository.DeleteAsync(content, TestContext.Current.CancellationToken);
                reference.Remove(content.Value);
            }
            else
            {
                await repository.SaveAsync(next, Noon, TestContext.Current.CancellationToken);
                reference[content.Value] = next;
            }
        }

        SqliteConnection.ClearAllPools();
        var restarted = new PersonalStateRepository(new SqliteConnectionFactory(directory.DatabasePath));
        var stored = (await restarted.GetAllAsync(TestContext.Current.CancellationToken))
            .ToDictionary(state => state.Content.Value, state => state, StringComparer.Ordinal);

        Assert.Equal(reference.Count, stored.Count);
        foreach (var (key, expected) in reference)
        {
            Assert.Equal(expected, stored[key]);
        }
    }

    [Fact]
    public async Task Concurrent_marking_and_searching_leave_the_stored_state_intact()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var catalog = new CatalogRepository(factory);
        var titles = Enumerable.Range(1, 20)
            .Select(index => new TitleId(Guid.Parse(
                $"c2000000-0000-4000-8000-0000000004{index:D2}")))
            .ToArray();
        foreach (var (id, index) in titles.Select((id, index) => (id, index)))
        {
            await catalog.UpsertTitleAsync(Title(id, $"Título {index:D2}"), TestContext.Current.CancellationToken);
        }

        var repository = new PersonalStateRepository(factory);
        var marking = Task.Run(
            async () =>
        {
            foreach (var id in titles)
            {
                await repository.SaveAsync(
                    PersonalState.Empty(ContentKey.ForTitle(id)).WithFavorite(true),
                    Noon,
                    TestContext.Current.CancellationToken);
            }
        },
            TestContext.Current.CancellationToken);
        var searching = Task.Run(
            async () =>
        {
            for (var round = 0; round < 20; round++)
            {
                await catalog.QueryAsync(
                    new CatalogQuery("Título", CatalogFilter.None),
                    TestContext.Current.CancellationToken);
            }
        },
            TestContext.Current.CancellationToken);

        await Task.WhenAll(marking, searching);

        var stored = await repository.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Equal(titles.Length, stored.Count);
        Assert.All(stored, state => Assert.True(state.IsFavorite));
        Assert.Equal(titles.Length, (await QueryIdsAsync(catalog, CatalogFilter.Favorite)).Count);
    }

    private static async Task<IReadOnlyList<TitleId>> QueryIdsAsync(
        CatalogRepository catalog,
        CatalogFilter filters)
    {
        var page = await catalog.QueryAsync(
            new CatalogQuery(Filters: filters),
            TestContext.Current.CancellationToken);
        return [.. page.Items.Select(item => item.Id)];
    }

    private static CatalogTitle Title(TitleId id, string title) => new(
        id,
        CatalogTitleKind.Movie,
        title,
        title,
        2016,
        [],
        [],
        [],
        Noon,
        null,
        HasProgress: false,
        IsPersonal: false,
        IsAvailable: true);

    private static ContentKey Content(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return ContentKey.ForTitle(new TitleId(new Guid(bytes)));
    }
}
