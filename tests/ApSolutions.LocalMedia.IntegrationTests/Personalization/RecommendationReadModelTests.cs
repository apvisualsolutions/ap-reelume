// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Personalization;

/// <summary>
/// The recommender's two inputs come out of SQLite, and its switch is remembered on disk. Both are
/// local reads; neither opens a connection to anything.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RecommendationReadModelTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Taste_is_summarized_from_watched_titles_and_their_personal_ratings()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var catalog = new CatalogRepository(factory);
        var loved = Title(1);
        var hated = Title(2);
        var unseen = Title(3);
        await catalog.UpsertTitleAsync(
            Entry(loved, "Alfa", 2010, ["Drama"], ["Ada"]),
            TestContext.Current.CancellationToken);
        await catalog.UpsertTitleAsync(
            Entry(hated, "Beta", 2020, ["Terror"], ["Bruno"]),
            TestContext.Current.CancellationToken);
        await catalog.UpsertTitleAsync(
            Entry(unseen, "Gamma", 2000, ["Drama"], ["Ada"]),
            TestContext.Current.CancellationToken);

        var watchStates = new WatchStateRepository(factory);
        await watchStates.SaveAsync(Watched(loved), TestContext.Current.CancellationToken);
        await watchStates.SaveAsync(Watched(hated), TestContext.Current.CancellationToken);
        var personal = new PersonalStateRepository(factory);
        await personal.SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(loved)).WithRating(10),
            Noon,
            TestContext.Current.CancellationToken);
        await personal.SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(hated)).WithRating(1),
            Noon,
            TestContext.Current.CancellationToken);

        var taste = await new RecommendationReadModel(factory).ReadTasteAsync(
            TestContext.Current.CancellationToken);

        Assert.True(taste.Genres["Drama"] > taste.Genres["Terror"]);
        Assert.True(taste.Cast["Ada"] > taste.Cast["Bruno"]);
        Assert.Equal(5.5, taste.AverageRating!.Value, 6);
        Assert.Equal(2015, taste.PreferredYear);
    }

    [Fact]
    public async Task Candidates_carry_genres_cast_availability_watched_state_and_rating()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var catalog = new CatalogRepository(factory);
        var watched = Title(11);
        var offline = Title(12);
        await catalog.UpsertTitleAsync(
            Entry(watched, "Alfa", 2010, ["Drama", "Comedia"], ["Ada", "Bruno"]),
            TestContext.Current.CancellationToken);
        await catalog.UpsertTitleAsync(
            Entry(offline, "Beta", null, [], [], isAvailable: false),
            TestContext.Current.CancellationToken);
        await new WatchStateRepository(factory).SaveAsync(
            Watched(watched),
            TestContext.Current.CancellationToken);
        await new PersonalStateRepository(factory).SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(watched)).WithRating(8),
            Noon,
            TestContext.Current.CancellationToken);

        var candidates = await new RecommendationReadModel(factory).ReadCandidatesAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(2, candidates.Count);
        var first = candidates.Single(candidate => candidate.Id == watched);
        Assert.Equal(["Comedia", "Drama"], first.Genres.Order(StringComparer.Ordinal));
        Assert.Equal(["Ada", "Bruno"], first.Cast.Order(StringComparer.Ordinal));
        Assert.True(first.IsAvailable);
        Assert.True(first.IsWatched);
        Assert.Equal(8, first.Rating);
        Assert.Equal(2010, first.Year);

        var second = candidates.Single(candidate => candidate.Id == offline);
        Assert.False(second.IsAvailable);
        Assert.False(second.IsWatched);
        Assert.Null(second.Rating);
        Assert.Null(second.Year);
        Assert.Empty(second.Genres);
        Assert.Empty(second.Cast);
    }

    [Fact]
    public async Task An_empty_catalog_gives_the_empty_taste_and_no_candidates()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var readModel = new RecommendationReadModel(factory);

        Assert.Equal(
            RecommendationTaste.Empty,
            await readModel.ReadTasteAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await readModel.ReadCandidatesAsync(TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => new RecommendationReadModel(null!));
    }

    [Fact]
    public async Task The_whole_pipeline_ranks_a_real_catalog_and_stays_deterministic()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var catalog = new CatalogRepository(factory);
        for (var index = 1; index <= 20; index++)
        {
            await catalog.UpsertTitleAsync(
                Entry(
                    Title(100 + index),
                    $"Título {index:D2}",
                    2000 + index,
                    [index % 2 == 0 ? "Drama" : "Terror"],
                    ["Ada"]),
                TestContext.Current.CancellationToken);
        }

        await new WatchStateRepository(factory).SaveAsync(
            Watched(Title(102)),
            TestContext.Current.CancellationToken);
        await new PersonalStateRepository(factory).SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(Title(102))).WithRating(10),
            Noon,
            TestContext.Current.CancellationToken);
        var useCase = new GetRecommendations(new RecommendationReadModel(factory));

        var first = await useCase.ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(20, first.Count);
        Assert.Equal(
            first.Select(item => (item.ContentId, item.Score)),
            second.Select(item => (item.ContentId, item.Score)));
        Assert.All(first, item => Assert.NotEmpty(item.ReasonCodes));
    }

    [Fact]
    public void The_switch_defaults_to_on_persists_when_turned_off_and_is_read_back()
    {
        using var directory = new DatabaseTestDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        var settings = new StoredRecommendationSettings(new JsonSettingsStore(settingsPath));

        Assert.True(settings.IsEnabled);

        settings.SetEnabled(false);
        Assert.False(settings.IsEnabled);
        Assert.False(new StoredRecommendationSettings(new JsonSettingsStore(settingsPath)).IsEnabled);

        settings.SetEnabled(true);
        Assert.True(new StoredRecommendationSettings(new JsonSettingsStore(settingsPath)).IsEnabled);
        Assert.Throws<ArgumentNullException>(() => new StoredRecommendationSettings(null!));
    }

    private static WatchState Watched(TitleId title) => new()
    {
        Content = ContentKey.ForTitle(title),
        Position = TimeSpan.FromMinutes(100),
        ObservedDuration = TimeSpan.FromMinutes(100),
        SourceMediaFileId = new MediaFileId(Guid.Parse("d3000000-0000-4000-8000-000000000001")),
        Status = WatchStatus.Watched,
        IsManualOverride = false,
        StartedUtc = Noon.AddHours(-2),
        UpdatedUtc = Noon,
    };

    private static CatalogTitle Entry(
        TitleId id,
        string title,
        int? year,
        string[] genres,
        string[] cast,
        bool isAvailable = true) => new(
        id,
        CatalogTitleKind.Movie,
        title,
        title,
        year,
        [],
        cast,
        genres,
        Noon,
        null,
        HasProgress: false,
        IsPersonal: false,
        isAvailable);

    private static TitleId Title(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new TitleId(new Guid(bytes));
    }
}
