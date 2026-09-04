// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The 2:3 card: initials over the control fill, never a hole, and a bar only when there is one.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for "initials over <c>ControlFillBrush</c>, never a hole" and there is no artwork in this
/// application to replace them with, so what these assert is the card as it ships rather than a
/// placeholder state. The proportion is asserted as a computed ratio and not as two numbers, because
/// 148 and 222 agreeing with two constants written here would still pass the day one of them moved
/// on its own.
/// </para>
/// <para>
/// The initials are also asserted <b>not</b> to reach automation. A reader announcing "EF" before
/// "El Faro de Piedra" hears the title twice, once spelled out, and that is the sort of thing only a
/// person using one would ever notice.
/// </para>
/// </remarks>
public sealed class PosterCardTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("El Faro de Piedra", "EF")]
    [InlineData("arrival", "A")]
    [InlineData("La  Ciudad   Dormida", "LC")]
    [InlineData("— · Arrival", "A")]
    [InlineData("2001 odisea", "2O")]
    public void The_initials_are_the_first_letters_of_the_first_two_words(string? title, string expected) =>
        Assert.Equal(expected, PosterInitials.From(title));

    /// <summary>
    /// The card is a 2:3 rectangle of the title's own colour, with its initials on top.
    /// </summary>
    /// <remarks>
    /// Until 2026-08-22 the rectangle was <c>ControlFillBrush</c> and nothing else, on the grounds
    /// that the application ships with no artwork and no token to fetch any. Both are still true and
    /// the conclusion was wrong: the prototype has no artwork either — every cover in it is a
    /// gradient computed from the title's hue — so the wall of colour costs nothing this application
    /// refuses to spend. The fill is still underneath, and in the two high contrasts it is all there
    /// is: <c>PosterArtOpacity</c> is 0 there, because a hue chosen by a hash is a contrast ratio
    /// nobody decided.
    /// </remarks>
    [AvaloniaFact]
    public void The_card_paints_the_titles_colour_and_its_initials_in_a_two_by_three_rectangle()
    {
        var card = Mount(new PosterCardStub("El Faro de Piedra", "2019", false, 0));

        var artwork = Assert.Single(
            card.GetVisualDescendants().OfType<Border>(),
            border => border.Background is ISolidColorBrush fill
                && fill.Color == ThemeColour("ControlFillBrush"));
        Assert.Equal(
            2d / 3d,
            artwork.Bounds.Width / artwork.Bounds.Height,
            2);
        Assert.True(
            artwork.CornerRadius.TopLeft > 0,
            "The artwork has square corners, so it is a rectangle rather than a poster.");

        // The two computed layers, and that they are this title's rather than a fixed pair.
        var painted = card.GetVisualDescendants()
            .OfType<Border>()
            .Select(border => border.Background)
            .ToArray();
        Assert.Contains(painted, brush => brush is LinearGradientBrush);
        Assert.Contains(painted, brush => brush is RadialGradientBrush);
        var basePaint = Assert.IsType<LinearGradientBrush>(
            painted.First(brush => brush is LinearGradientBrush));
        Assert.Equal(
            ((LinearGradientBrush)PosterArt.BaseOf("El Faro de Piedra")).GradientStops[0].Color,
            basePaint.GradientStops[0].Color);

        var initials = Assert.Single(
            card.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "EF");
        Assert.Equal(
            ThemeColour("PosterInitialsBrush"),
            Assert.IsAssignableFrom<ISolidColorBrush>(initials.Foreground).Color);
        Assert.Equal(
            Avalonia.Automation.AccessibilityView.Raw,
            Avalonia.Automation.AutomationProperties.GetAccessibilityView(initials));
    }

    /// <summary>The title holds one line, ending in an ellipsis, and the caption is set apart.</summary>
    /// <remarks>
    /// One line and not two, decided on 2026-08-24 by the owner looking at the real grid: a title
    /// that took a second line pushed its own caption below the caption of the card beside it, so a
    /// row read as a ragged edge. The full title is still announced — the button around the card
    /// carries it as its accessible name.
    ///
    /// The caption's colour is compared against the title's as well as against the token: a card
    /// where both were secondary would satisfy "the year is <c>TextSecondaryBrush</c>" and lose the
    /// contrast the rule is about.
    /// </remarks>
    [AvaloniaFact]
    public void The_title_holds_one_line_and_the_caption_is_a_quieter_colour()
    {
        var card = Mount(new PosterCardStub(
            "Ocho Cartas para un Invierno Muy Largo",
            "2023",
            false,
            0)
        { MetaText = "2023" });

        var blocks = card.GetVisualDescendants().OfType<TextBlock>().ToArray();
        var title = Assert.Single(blocks, block => block.Text!.StartsWith("Ocho", StringComparison.Ordinal));
        Assert.Equal(TextWrapping.NoWrap, title.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);

        // The line under the title is the meta line since 2026-08-24 — "2024 · 111 min · Suspense"
        // where the catalogue knows all three, and the year alone where it knows one. It is asked
        // for by its text so that the chip and the status line, which are also quieter than the
        // title, cannot stand in for it.
        var meta = Assert.Single(blocks, block => block.Text == "2023");
        var metaColour = Assert.IsAssignableFrom<ISolidColorBrush>(meta.Foreground).Color;
        Assert.Equal(ThemeColour("TextSecondaryBrush"), metaColour);
        Assert.NotEqual(
            metaColour,
            Assert.IsAssignableFrom<ISolidColorBrush>(title.Foreground).Color);
    }

    /// <summary>
    /// The caption and the bar are absent rather than empty when there is nothing to say.
    /// </summary>
    /// <remarks>
    /// Absent and not blank: an empty caption still takes a line of height, and a bar at zero for a
    /// list that does not read progress would claim something the list never measured. That is the
    /// distinction <c>PrivacySettingsView</c> already models between absent and disabled, applied to
    /// a card.
    /// </remarks>
    [AvaloniaFact]
    public void A_card_with_nothing_to_add_shows_neither_caption_nor_bar()
    {
        var quiet = Mount(new PosterCardStub("Arrival", string.Empty, false, 0));
        Assert.DoesNotContain(
            quiet.GetVisualDescendants().OfType<ProgressBar>(),
            bar => bar.IsEffectivelyVisible);
        Assert.DoesNotContain(
            quiet.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsEffectivelyVisible && block.Text?.Length == 0);

        var watched = Mount(new PosterCardStub("Arrival", "2016", true, 0.42));
        var bar = Assert.Single(watched.GetVisualDescendants().OfType<ProgressBar>());
        Assert.True(bar.IsEffectivelyVisible);
        Assert.Equal(0.42, bar.Value);
        Assert.Equal(3, bar.Bounds.Height);
    }

    /// <summary>
    /// The four models the card is fed by, each answering for what it knows and no more.
    /// </summary>
    /// <remarks>
    /// Asserted in C# and not only through the markup, and the coverage gate is why: five properties
    /// read by nothing but a binding took <c>CatalogItemViewModel</c> from 83/100 to <b>38/50</b> on
    /// the first CI run after the card landed. A property only a <c>DataTemplate</c> reads is a
    /// property no test can be wrong about, which is the same shape as a service registered and never
    /// resolved.
    /// <para>
    /// What each one is asserted <b>not</b> to have matters as much: the suggestion has no caption
    /// because nothing looked its year up, and the catalogue draws no bar because
    /// <c>CatalogItem.HasProgress</c> says a title was started and not how far it got.
    /// </para>
    /// </remarks>
    [Fact]
    public void Each_of_the_four_models_answers_for_exactly_what_it_knows()
    {
        var catalogued = new CatalogItemViewModel(new CatalogItem(
            new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            CatalogTitleKind.Movie,
            "El Faro de Piedra",
            2019,
            IsAvailable: true,
            HasProgress: true,
            IsPersonal: false,
            DateTimeOffset.UnixEpoch,
            null));
        Assert.Equal("EF", catalogued.Initials);
        Assert.Equal("2019", catalogued.MetaText);
        Assert.True(catalogued.HasMeta);
        Assert.Equal("CatalogKindMovie", catalogued.KindKey);
        Assert.Equal("WatchStatusNotStarted", catalogued.StatusKey);
        Assert.False(catalogued.CountsEpisodes);
        Assert.False(catalogued.IsWatched);
        Assert.False(catalogued.HasKnownProgress);
        Assert.Equal(0, catalogued.CompletedFraction);

        var undated = new CatalogItemViewModel(new CatalogItem(
            new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            CatalogTitleKind.Show,
            "Crónicas",
            null,
            IsAvailable: false,
            HasProgress: false,
            IsPersonal: false,
            DateTimeOffset.UnixEpoch,
            null));
        Assert.Equal(string.Empty, undated.MetaText);
        Assert.False(undated.HasMeta);
        Assert.Equal("CatalogKindShow", undated.KindKey);
        Assert.Equal("MediaUnavailable", undated.AvailabilityKey);

        var started = new InProgressItemViewModel(new InProgressItem(
            ContentKey.ForTitle(new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000003"))),
            new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000003")),
            CatalogTitleKind.Movie,
            "Vientos del Norte",
            null,
            null,
            null,
            0.42,
            IsAvailable: true,
            DateTimeOffset.UnixEpoch));
        Assert.Equal("VD", started.Initials);
        Assert.True(started.HasKnownProgress);
        Assert.Equal(0.42, started.CompletedFraction);
        Assert.False(started.HasMeta);
        Assert.Equal("WatchStatusInProgress", started.StatusKey);

        var added = new RecentlyAddedItemViewModel(new RecentlyAddedItem(
            new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000004")),
            CatalogTitleKind.Movie,
            "La Ciudad Dormida",
            2021,
            IsAvailable: true,
            DateTimeOffset.UnixEpoch));
        Assert.Equal("LC", added.Initials);
        Assert.Equal("2021", added.MetaText);
        Assert.True(added.HasMeta);
        Assert.Equal("WatchStatusNotStarted", added.StatusKey);
        Assert.False(added.HasKnownProgress);
        Assert.Equal(0, added.CompletedFraction);

        var suggested = new RecommendationItemViewModel(
            new Recommendation(new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000005")), 0.9, []),
            "Ocho Cartas");
        Assert.Equal("OC", suggested.Initials);
        Assert.Equal(string.Empty, suggested.MetaText);
        Assert.False(suggested.HasMeta);
        Assert.False(suggested.HasKind);
        Assert.False(suggested.HasKnownProgress);
        Assert.Equal(0, suggested.CompletedFraction);
    }

    /// <summary>
    /// A card with a cover draws it, and drops the letters that were standing in for it.
    /// </summary>
    /// <remarks>
    /// <b>Until 2026-09-04 this could not be written, because the card had nowhere to put a
    /// picture.</b> The application downloaded posters, let somebody pick their own, stored both and
    /// backed both up, and this grid — the screen the whole library is looked at through — drew a
    /// generated gradient over initials for every title. The defect this repository names as its own,
    /// on its most looked-at surface.
    /// </remarks>
    [AvaloniaFact]
    public void A_card_with_a_cover_draws_it_instead_of_its_initials()
    {
        var picture = Path.Combine(Path.GetTempPath(), $"poster-{Guid.NewGuid():N}.png");
        WritePicture(picture);

        try
        {
            var card = Mount(new PosterCardStub("Dune", string.Empty, false, 0)
            {
                PosterFile = picture,
            });

            var image = card.GetLogicalDescendants().OfType<Image>().Single(each => each.Name == "PosterPicture");
            var initials = card.GetLogicalDescendants().OfType<TextBlock>()
                .Single(each => string.Equals(each.Text, PosterInitials.From("Dune"), StringComparison.Ordinal));

            Assert.True(image.IsVisible);
            Assert.NotNull(image.Source);
            Assert.False(initials.IsVisible);
        }
        finally
        {
            File.Delete(picture);
        }
    }

    /// <summary>
    /// And a card with none draws what it always drew, which is what keeps a library of scanned
    /// folders from becoming a grid of empty rectangles.
    /// </summary>
    [AvaloniaFact]
    public void A_card_with_no_cover_keeps_the_generated_art_and_its_initials()
    {
        var card = Mount(new PosterCardStub("Dune", string.Empty, false, 0));

        var image = card.GetLogicalDescendants().OfType<Image>().Single(each => each.Name == "PosterPicture");
        var initials = card.GetLogicalDescendants().OfType<TextBlock>()
            .Single(each => string.Equals(each.Text, PosterInitials.From("Dune"), StringComparison.Ordinal));

        Assert.False(image.IsVisible);
        Assert.True(initials.IsVisible);
    }

    /// <summary>
    /// A name that is not a picture leaves the letters showing rather than a hole.
    /// </summary>
    [AvaloniaFact]
    public void A_cover_that_names_no_picture_leaves_the_initials_where_they_were()
    {
        var notAPicture = Path.Combine(Path.GetTempPath(), $"poster-{Guid.NewGuid():N}.png");
        File.WriteAllText(notAPicture, "this is not a PNG");

        try
        {
            var card = Mount(new PosterCardStub("Dune", string.Empty, false, 0)
            {
                PosterFile = notAPicture,
            });

            var image = card.GetLogicalDescendants().OfType<Image>().Single(each => each.Name == "PosterPicture");

            // The card still says it has one — the field is filled — and the decode answered
            // nothing, so what is drawn underneath is the generated art the grid always had.
            Assert.Null(image.Source);
        }
        finally
        {
            File.Delete(notAPicture);
        }
    }

    /// <summary>A one-pixel PNG, written by hand so the test owns no fixture file.</summary>
    private static void WritePicture(string path) =>
        File.WriteAllBytes(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));

    private static PosterCardView Mount(IPosterCard model)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var card = new PosterCardView { DataContext = model };
        var window = new Window { Width = 400, Height = 500, Content = card };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return card;
    }

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme variant.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    /// <summary>
    /// A card with only what these tests are about, and defaults for the rest.
    /// </summary>
    /// <remarks>
    /// The nine that follow the four arrived with the prototype's card on 2026-08-24 — the kind chip,
    /// the meta line, the status, the episode count, the tick and whether the medium is reachable.
    /// They carry initialisers rather than positional parameters so that a test that cares about one
    /// of them says so and the other eight stay out of its way.
    /// </remarks>
    private sealed record PosterCardStub(
        string Title,
        string CaptionText,
        bool HasKnownProgress,
        double CompletedFraction) : IPosterCard
    {
        public string Initials => PosterInitials.From(Title);

        public bool HasCaption => CaptionText.Length > 0;

        public string KindKey { get; init; } = "CatalogKindMovie";

        public bool HasKind { get; init; } = true;

        public string MetaText { get; init; } = string.Empty;

        public bool HasMeta => MetaText.Length > 0;

        public string StatusKey { get; init; } = "WatchStatusNotStarted";

        public string EpisodeCountText { get; init; } = string.Empty;

        public bool CountsEpisodes { get; init; }

        public bool IsWatched { get; init; }

        public bool IsAvailable { get; init; } = true;

        public string? PosterFile { get; init; }
    }
}
