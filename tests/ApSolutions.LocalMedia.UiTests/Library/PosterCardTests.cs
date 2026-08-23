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

    /// <summary>The title gets two lines and the caption is set apart from it.</summary>
    /// <remarks>
    /// The caption's colour is compared against the title's as well as against the token: a card
    /// where both were secondary would satisfy "the year is <c>TextSecondaryBrush</c>" and lose the
    /// contrast the rule is about.
    /// </remarks>
    [AvaloniaFact]
    public void The_title_gets_two_lines_and_the_caption_is_a_quieter_colour()
    {
        var card = Mount(new PosterCardStub(
            "Ocho Cartas para un Invierno Muy Largo",
            "2023",
            false,
            0));

        var blocks = card.GetVisualDescendants().OfType<TextBlock>().ToArray();
        var title = Assert.Single(blocks, block => block.Text!.StartsWith("Ocho", StringComparison.Ordinal));
        Assert.Equal(2, title.MaxLines);
        Assert.Equal(TextWrapping.Wrap, title.TextWrapping);

        var caption = Assert.Single(blocks, block => block.Text == "2023");
        var captionColour = Assert.IsAssignableFrom<ISolidColorBrush>(caption.Foreground).Color;
        Assert.Equal(ThemeColour("TextSecondaryBrush"), captionColour);
        Assert.NotEqual(
            captionColour,
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
        Assert.Equal("2019", catalogued.CaptionText);
        Assert.True(catalogued.HasCaption);
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
        Assert.Equal(string.Empty, undated.CaptionText);
        Assert.False(undated.HasCaption);
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
        Assert.False(started.HasCaption);

        var added = new RecentlyAddedItemViewModel(new RecentlyAddedItem(
            new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000004")),
            CatalogTitleKind.Movie,
            "La Ciudad Dormida",
            2021,
            IsAvailable: true,
            DateTimeOffset.UnixEpoch));
        Assert.Equal("LC", added.Initials);
        Assert.Equal("2021", added.CaptionText);
        Assert.True(added.HasCaption);
        Assert.False(added.HasKnownProgress);
        Assert.Equal(0, added.CompletedFraction);

        var suggested = new RecommendationItemViewModel(
            new Recommendation(new TitleId(Guid.Parse("00000000-0000-0000-0000-000000000005")), 0.9, []),
            "Ocho Cartas");
        Assert.Equal("OC", suggested.Initials);
        Assert.Equal(string.Empty, suggested.CaptionText);
        Assert.False(suggested.HasCaption);
        Assert.False(suggested.HasKnownProgress);
        Assert.Equal(0, suggested.CompletedFraction);
    }

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

    private sealed record PosterCardStub(
        string Title,
        string CaptionText,
        bool HasKnownProgress,
        double CompletedFraction) : IPosterCard
    {
        public string Initials => PosterInitials.From(Title);

        public bool HasCaption => CaptionText.Length > 0;
    }
}
