// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Show;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Details;

/// <summary>
/// The poster reaches both cards, and the generated art stays underneath it.
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

    [Fact]
    public void A_show_card_states_the_poster_it_was_given()
    {
        var viewModel = new ShowDetailsViewModel();

        viewModel.Apply(
            Item(CatalogTitleKind.Show),
            [],
            new Dictionary<ContentKey, WatchState>(),
            posterFile: @"C:\cache\artwork\poster.jpg");

        Assert.Equal(@"C:\cache\artwork\poster.jpg", viewModel.PosterFile);
        Assert.True(viewModel.HasPoster);
    }

    /// <summary>
    /// The show card draws it in both places too, and the same file decodes once for both cards.
    /// </summary>
    /// <remarks>
    /// The prototype raises the show's poster at 136×204 against the same bled art wall the film
    /// card uses, so this is one chain and two views rather than two chains. The shared decode is
    /// asserted across the two <em>cards</em> here — the converter caches by path for the process,
    /// not per view.
    /// </remarks>
    [AvaloniaFact]
    public void The_show_card_draws_the_same_poster_in_both_of_its_places()
    {
        var poster = WriteOnePixelPng();
        var viewModel = new ShowDetailsViewModel();
        viewModel.Apply(
            Item(CatalogTitleKind.Show),
            [],
            new Dictionary<ContentKey, WatchState>(),
            posterFile: poster);
        var view = new ShowDetailsView { DataContext = viewModel };
        var window = new Window { Width = 1180, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var drawn = view.GetVisualDescendants()
            .OfType<Image>()
            .Where(image => image.IsEffectivelyVisible)
            .ToArray();

        Assert.Equal(2, drawn.Length);
        Assert.All(drawn, image => Assert.NotNull(image.Source));
        Assert.Same(drawn[0].Source, drawn[1].Source);
        Assert.Equal(2, view.GetVisualDescendants().OfType<PosterArtView>().Count());
        Assert.DoesNotContain(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == "A");
        window.Close();
        File.Delete(poster);
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

    /// <summary>
    /// The cache is bounded, and the same path answers the same picture until it is dropped.
    /// </summary>
    /// <remarks>
    /// A decoded `w780` poster is about 3.5 MB in memory whatever the file weighs, so an unbounded
    /// dictionary keyed by path would have somebody browsing a hundred films carrying a third of a
    /// gigabyte of pictures nothing draws. What is asserted is the bound doing its job: the oldest
    /// entry stops being answered from memory once <c>Capacity</c> newer ones exist, and the newest
    /// still is.
    /// </remarks>
    [AvaloniaFact]
    public void The_decoded_posters_are_bounded_and_the_oldest_is_the_one_that_goes()
    {
        var converter = new CachedPosterConverter();
        var posters = Enumerable.Range(0, CachedPosterConverter.Capacity + 1)
            .Select(_ => WriteOnePixelPng())
            .ToArray();

        var first = Convert(converter, posters[0]);
        Assert.NotNull(first);
        Assert.Same(first, Convert(converter, posters[0]));

        foreach (var poster in posters.Skip(1))
        {
            Assert.NotNull(Convert(converter, poster));
        }

        // The first was pushed out by the newer ones, so it is decoded again rather than answered:
        // a different instance for the same path is exactly what eviction looks like from here.
        Assert.NotSame(first, Convert(converter, posters[0]));

        // And the newest is still remembered, which is what makes the bound a cache rather than a
        // counter that clears everything.
        var newest = Convert(converter, posters[^1]);
        Assert.Same(newest, Convert(converter, posters[^1]));

        foreach (var poster in posters)
        {
            File.Delete(poster);
        }
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

    /// <summary>
    /// Every poster is decoded at one width, whatever the file on the disk actually holds.
    /// </summary>
    /// <remarks>
    /// <b>The bound exists because a personal cover is not a provider poster.</b> Until 2026-09-04
    /// everything here arrived as <c>w780</c> from TMDB, so the memory this class budgets held by
    /// construction. A cover chosen off somebody's own disk is whatever their camera produced: ten
    /// megabytes of JPEG is tens of millions of pixels, and eight of those decoded whole is most of
    /// a gigabyte on the thread that draws.
    /// <para>
    /// <b>The enlarging half is asserted on purpose, not tolerated.</b> A picture narrower than the
    /// bound is decoded <em>up</em> to it — 300×450 becomes 780×1170, which costs 3.65 MB where the
    /// file would have cost 0.5 — and somebody reading this later would be right to call that
    /// wasteful and wrong to call it a mistake. It is the price of one predictable cost per entry
    /// instead of one nobody can predict, and it is written down here so that changing it is a
    /// decision rather than a tidy-up.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_poster_is_decoded_at_one_width_however_big_the_file_is()
    {
        var wide = WritePng(2000, 3000);
        var narrow = WritePng(300, 450);
        var converter = new CachedPosterConverter();

        try
        {
            var bounded = Assert.IsType<Bitmap>(Convert(converter, wide));
            Assert.Equal(CachedPosterConverter.DecodeWidth, bounded.PixelSize.Width);

            // Aspect ratio is kept, which is what makes one width enough for both surfaces.
            Assert.Equal(CachedPosterConverter.DecodeWidth * 3 / 2, bounded.PixelSize.Height);

            var enlarged = Assert.IsType<Bitmap>(Convert(converter, narrow));
            Assert.Equal(CachedPosterConverter.DecodeWidth, enlarged.PixelSize.Width);
        }
        finally
        {
            File.Delete(wide);
            File.Delete(narrow);
        }
    }

    /// <summary>A real picture of a given size, written by a real encoder.</summary>
    private static string WritePng(int width, int height)
    {
        var target = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = target.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.Teal, new Rect(0, 0, width, height));
        }

        var path = Path.Combine(Path.GetTempPath(), $"ap-poster-{Guid.NewGuid():N}.png");
        target.Save(path, new PngBitmapEncoderOptions());
        return path;
    }

    /// <summary>The smallest thing that is genuinely a picture, so the converter has one to decode.</summary>
    private static string WriteOnePixelPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ap-poster-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        return path;
    }

    private static CatalogItem Item(CatalogTitleKind kind = CatalogTitleKind.Movie) => new(
        MovieId,
        kind,
        "Arrival",
        2016,
        IsAvailable: true,
        HasProgress: false,
        IsPersonal: false,
        DateTimeOffset.UnixEpoch,
        null);
}
