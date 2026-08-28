// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Details;

/// <summary>
/// The poster reaches the film card, and the generated art stays underneath it.
/// </summary>
/// <remarks>
/// <c>PosterPath</c> was produced, merged and persisted from the beginning and no surface read it —
/// a value with no reader, which is this repository's characteristic defect seen from the far end.
/// It closed on 2026-08-28, and what closed it is a path handed in like every other value here:
/// nothing on this card queries anything, and nothing on it opens a connection.
/// </remarks>
public sealed class PosterTests
{
    private static readonly TitleId MovieId = new(new Guid("00000000-0000-0000-0000-0000000000e1"));

    [Fact]
    public void A_film_card_states_the_poster_it_was_given()
    {
        var viewModel = new MovieDetailsViewModel();

        viewModel.Apply(Item(), null, null, posterFile: @"C:\cache\artwork\poster.jpg");

        Assert.Equal(@"C:\cache\artwork\poster.jpg", viewModel.PosterFile);
        Assert.True(viewModel.HasPoster);
    }

    /// <summary>
    /// A library nobody identified has no posters, and that is the ordinary state rather than a gap.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_absent_or_blank_poster_is_no_poster(string? posterFile)
    {
        var viewModel = new MovieDetailsViewModel();

        viewModel.Apply(Item(), null, null, posterFile: posterFile);

        Assert.False(viewModel.HasPoster);

        // And the initials are still there, because they are what says which title this is when
        // there is no picture to say it.
        Assert.Equal("A", viewModel.Initials);
    }

    /// <summary>
    /// Both places the card draws a poster are wired, and the generated art is under both.
    /// </summary>
    /// <remarks>
    /// Asserted over the visual tree and by effective visibility rather than by counting
    /// <c>&lt;Image&gt;</c> elements in the markup: an image declared inside a collapsed panel is an
    /// image nobody sees, and the whole point of this card is that something is drawn.
    /// </remarks>
    [AvaloniaFact]
    public void With_no_poster_the_card_draws_the_generated_art_and_its_initials()
    {
        var viewModel = new MovieDetailsViewModel();
        viewModel.Apply(Item(), null, null);
        var (window, view) = Show(viewModel);

        Assert.Equal(2, view.GetVisualDescendants().OfType<PosterArtView>().Count());
        Assert.DoesNotContain(
            view.GetVisualDescendants().OfType<Image>(),
            image => image.IsEffectivelyVisible);
        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == "A");
        window.Close();
    }

    /// <summary>
    /// With a poster, both images are drawn and the initials go.
    /// </summary>
    /// <remarks>
    /// The file is a real one written for this test, because the converter decodes it: a path to
    /// nothing would answer null and prove that the binding exists rather than that a picture
    /// arrives. Two letters over the actual poster would be a second answer to a question the
    /// picture has already answered.
    /// </remarks>
    [AvaloniaFact]
    public void With_a_poster_both_places_draw_it_and_the_initials_go()
    {
        var poster = WriteOnePixelPng();
        var viewModel = new MovieDetailsViewModel();
        viewModel.Apply(Item(), null, null, posterFile: poster);
        var (window, view) = Show(viewModel);

        var drawn = view.GetVisualDescendants()
            .OfType<Image>()
            .Where(image => image.IsEffectivelyVisible)
            .ToArray();

        Assert.Equal(2, drawn.Length);
        Assert.All(drawn, image => Assert.NotNull(image.Source));

        // One decode between the two: the converter caches by path, and a card that decoded the same
        // file twice would pay for it on every open.
        Assert.Same(drawn[0].Source, drawn[1].Source);

        // The generated art is still underneath both, which is what a poster with transparency or a
        // different aspect shows through.
        Assert.Equal(2, view.GetVisualDescendants().OfType<PosterArtView>().Count());
        Assert.DoesNotContain(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == "A");
        window.Close();
        File.Delete(poster);
    }

    /// <summary>
    /// A path that names no picture is no picture, and never an exception.
    /// </summary>
    /// <remarks>
    /// The file is one this application wrote into its own cache, so by the time a card opens it may
    /// have been deleted by hand, half written, or not be an image at all. Every one of those is the
    /// state the card already draws.
    /// </remarks>
    [AvaloniaFact]
    public void A_path_that_names_no_picture_draws_the_art_underneath_instead()
    {
        var notAnImage = Path.Combine(Path.GetTempPath(), $"ap-poster-{Guid.NewGuid():N}.jpg");
        File.WriteAllText(notAnImage, "this is not a picture");
        var converter = new CachedPosterConverter();

        Assert.Null(Convert(converter, notAnImage));
        Assert.Null(Convert(converter, Path.Combine(Path.GetTempPath(), "ap-poster-missing.jpg")));
        Assert.Null(Convert(converter, null));
        Assert.Null(Convert(converter, "   "));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(
            null,
            typeof(string),
            null,
            CultureInfo.InvariantCulture));
        File.Delete(notAnImage);
    }

    private static object? Convert(CachedPosterConverter converter, object? value) =>
        converter.Convert(value, typeof(object), null, CultureInfo.InvariantCulture);

    private static (Window Window, MovieDetailsView View) Show(MovieDetailsViewModel viewModel)
    {
        var view = new MovieDetailsView { DataContext = viewModel };
        var window = new Window { Width = 1180, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    /// <summary>The smallest thing that is genuinely a picture, so the converter has one to decode.</summary>
    private static string WriteOnePixelPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ap-poster-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        return path;
    }

    private static CatalogItem Item() => new(
        MovieId,
        CatalogTitleKind.Movie,
        "Arrival",
        2016,
        IsAvailable: true,
        HasProgress: false,
        IsPersonal: false,
        DateTimeOffset.UnixEpoch,
        null);
}
