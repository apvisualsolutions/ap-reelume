// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// Every line and every badge the library's card paints, including the shapes it paints for a file
/// nobody has identified.
/// </summary>
/// <remarks>
/// The card gained a kind chip, a data line, a status line, a tick and an episode count on
/// 2026-08-24, and each of those is a small switch with an arm for the case a screenshot never
/// shows: a scanned file that is neither film nor series, a title with no year and no length, a
/// series with no episodes counted. The card is what the whole library is read through, so the arm
/// nobody looks at is the one that will be wrong.
/// </remarks>
public sealed class CatalogCardTextTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void A_card_says_what_kind_of_thing_it_is_including_neither()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        Assert.Equal("CatalogKindMovie", Card(CatalogTitleKind.Movie).KindKey);
        Assert.Equal("CatalogKindShow", Card(CatalogTitleKind.Show).KindKey);

        // The third arm: a scanned file the catalogue projects with no identification behind it.
        Assert.Equal("CatalogKindFile", Card((CatalogTitleKind)7).KindKey);
        Assert.True(Card(CatalogTitleKind.Movie).HasKind);
    }

    /// <summary>
    /// «2024 · 111 min · Suspense», and what is left of it when the catalogue knows less.
    /// </summary>
    [AvaloniaFact]
    public void The_data_line_drops_each_piece_the_catalogue_cannot_answer()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var full = Card(
            CatalogTitleKind.Movie,
            year: 2024,
            runtime: TimeSpan.FromMinutes(111),
            genres: ["Suspense", "Drama", "Thriller"]);
        Assert.True(full.HasMeta);
        Assert.StartsWith("2024 · 111 min · Suspense · Drama", full.MetaText, StringComparison.Ordinal);
        Assert.DoesNotContain("Thriller", full.MetaText, StringComparison.Ordinal);

        var bare = Card(CatalogTitleKind.Movie, year: null, runtime: null, genres: null);
        Assert.Equal(string.Empty, bare.MetaText);
        Assert.False(bare.HasMeta);

        // A running time of zero is a length nobody read, not a film of no length.
        var unread = Card(CatalogTitleKind.Movie, year: 2024, runtime: TimeSpan.Zero, genres: null);
        Assert.Equal("2024", unread.MetaText);

        // A genre list that exists and is empty is a different absence from having none at all, and
        // it reaches the line the same way: nothing after the year.
        var noGenres = Card(CatalogTitleKind.Movie, year: 2024, runtime: null, genres: []);
        Assert.Equal("2024", noGenres.MetaText);
    }

    /// <summary>The status line, the tick, and the count only a series carries.</summary>
    [AvaloniaFact]
    public void A_card_says_how_far_through_it_is_and_counts_episodes_only_for_a_series()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        Assert.Equal("WatchStatusNotStarted", Card(CatalogTitleKind.Movie).StatusKey);
        Assert.Equal(
            "WatchStatusInProgress",
            Card(CatalogTitleKind.Movie, status: WatchStatus.InProgress).StatusKey);

        var watched = Card(CatalogTitleKind.Movie, status: WatchStatus.Watched);
        Assert.Equal("WatchStatusWatched", watched.StatusKey);
        Assert.True(watched.IsWatched);
        Assert.False(Card(CatalogTitleKind.Movie).IsWatched);

        var series = Card(CatalogTitleKind.Show, episodeCount: 16, episodesWatched: 10);
        Assert.True(series.CountsEpisodes);
        Assert.Equal("10/16", series.EpisodeCountText);

        // A series the catalogue knows no episodes of counts nothing rather than «0/0», and a film
        // never counts at all.
        Assert.False(Card(CatalogTitleKind.Show).CountsEpisodes);
        Assert.Equal(string.Empty, Card(CatalogTitleKind.Show).EpisodeCountText);
        Assert.False(Card(CatalogTitleKind.Movie, episodeCount: 16).CountsEpisodes);

        // And the bar, which is drawn from a fraction rather than from the started flag.
        Assert.False(Card(CatalogTitleKind.Movie).HasKnownProgress);
        Assert.True(Card(CatalogTitleKind.Movie, completed: 0.5).HasKnownProgress);
        Assert.Equal(0.5, Card(CatalogTitleKind.Movie, completed: 0.5).CompletedFraction);
    }

    /// <summary>
    /// The shape a kind is drawn as, and the direction that conversion does not go.
    /// </summary>
    /// <remarks>
    /// The converter turns a key into one of the theme's own geometries. Asked for a key it does not
    /// know it answers with nothing rather than with a guess, and asked to run backwards it refuses:
    /// a shape does not become a kind, and a converter that silently returned one would be inventing
    /// a catalogue entry out of a drawing.
    /// </remarks>
    [AvaloniaFact]
    public void The_kind_shape_is_looked_up_and_never_converted_back()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var converter = new KindShapeConverter();
        var film = converter.Convert("CatalogKindMovie", typeof(Geometry), null, CultureInfo.CurrentCulture);
        var show = converter.Convert("CatalogKindShow", typeof(Geometry), null, CultureInfo.CurrentCulture);

        Assert.IsAssignableFrom<Geometry>(film);
        Assert.IsAssignableFrom<Geometry>(show);
        Assert.NotSame(film, show);

        // Anything that is not the series key is drawn as a film, which is what an unidentified file
        // gets: a frame of celluloid rather than a blank where a chip should be.
        Assert.Same(film, converter.Convert(null, typeof(Geometry), null, CultureInfo.CurrentCulture));
        Assert.Same(
            film,
            converter.Convert("NotAKeyAnybodyDeclared", typeof(Geometry), null, CultureInfo.CurrentCulture));

        // And it never runs backwards: a shape does not become a kind, and a converter that
        // answered would be inventing a catalogue entry out of a drawing.
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(null, typeof(string), null, CultureInfo.CurrentCulture));
    }

    /// <summary>
    /// The cover's own property, read and written the way code reads it rather than the way a
    /// binding does.
    /// </summary>
    /// <remarks>
    /// Five surfaces mount this view and every one of them binds <c>Title</c>, which reaches the
    /// styled property without ever going through the wrapper — so the two lines a person would
    /// write to use it from code are the two nothing measured.
    /// </remarks>
    [AvaloniaFact]
    public void The_cover_takes_the_title_it_is_given()
    {
        var art = new PosterArtView { Title = "Puerto Sombra" };

        Assert.Equal("Puerto Sombra", art.Title);
        Assert.Equal("Puerto Sombra", art.GetValue(PosterArtView.TitleProperty));

        art.Title = "Otra cosa";
        Assert.Equal("Otra cosa", art.Title);
    }

    /// <summary>
    /// The kind chip keeps its word in the grid and drops it in a rail.
    /// </summary>
    /// <remarks>
    /// The prototype writes «Película» only where a card has the width for it. In the rails it draws
    /// the glyph alone, and the same card class serves both — so what decides is a class on the host,
    /// which is exactly the kind of thing that gets copied onto a third rail and forgotten. Mounted
    /// rather than grepped: a style that stopped matching would leave the markup saying the right
    /// thing and the screen saying the other one.
    /// </remarks>
    [AvaloniaFact]
    public void The_kind_chip_keeps_its_word_in_the_grid_and_drops_it_in_a_rail()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        Assert.True(WordIsVisible(inRail: false));
        Assert.False(WordIsVisible(inRail: true));
    }

    /// <summary>
    /// Whether the chip's word is drawn, on a card mounted the way a grid or a rail mounts it.
    /// </summary>
    /// <remarks>
    /// Read before the window closes, and that is not a detail: closing it detaches the control, the
    /// style stops applying, and <c>IsVisible</c> answers with the local value — which is true for
    /// both, so a test that returned the control would pass whatever the style did.
    /// </remarks>
    private static bool WordIsVisible(bool inRail)
    {
        var card = new PosterCardView { DataContext = Card(CatalogTitleKind.Movie) };
        if (inRail)
        {
            card.Classes.Add("glyph-chip");
        }

        var window = new Avalonia.Controls.Window { Width = 400, Height = 600, Content = card };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var word = Assert.Single(
            card.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>(),
            block => block.Name == "KindWord");
        var visible = word.IsVisible;
        window.Close();
        return visible;
    }

    private static CatalogItemViewModel Card(
        CatalogTitleKind kind,
        int? year = 2024,
        TimeSpan? runtime = null,
        IReadOnlyList<string>? genres = null,
        WatchStatus status = WatchStatus.NotStarted,
        double completed = 0,
        int episodeCount = 0,
        int episodesWatched = 0) => new(new CatalogItem(
        new TitleId(Guid.Parse("f1000000-0000-4000-8000-000000000001")),
        kind,
        "Vidrio Templado",
        year,
        IsAvailable: true,
        HasProgress: completed > 0,
        IsPersonal: false,
        Noon,
        LastPlayedUtc: null,
        runtime,
        genres,
        status,
        completed,
        episodeCount,
        episodesWatched));
}
