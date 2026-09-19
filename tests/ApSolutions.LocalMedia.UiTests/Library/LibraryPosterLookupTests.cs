// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Library;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The grid hands each card's two cover fields to whoever resolves the picture (LIB-021).
/// </summary>
/// <remarks>
/// Which of the two draws is <c>ResolveTitlePoster</c>'s order, tested where it lives. What this
/// guards is the seam: a grid that passed only the provider's field would draw the provider's poster
/// over a cover somebody picked, and every test of the resolver would stay green.
/// </remarks>
public sealed class LibraryPosterLookupTests
{
    [Fact]
    public async Task Each_card_hands_over_both_cover_fields_and_draws_what_comes_back()
    {
        var id = new TitleId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var chosen = new string('a', 64) + ".png";
        var item = new CatalogItem(
            id,
            CatalogTitleKind.Movie,
            "Arrival",
            2016,
            IsAvailable: true,
            HasProgress: false,
            IsPersonal: false,
            AddedUtc: DateTimeOffset.UnixEpoch,
            LastPlayedUtc: null,
            PosterPath: "/provider.jpg",
            PersonalCover: chosen);
        (TitleId Id, string? Poster, string? Personal)? asked = null;
        var viewModel = new LibraryViewModel(
            new OnePage(new CatalogPage([item], null)),
            findPoster: (title, poster, personal) =>
            {
                asked = (title, poster, personal);
                return @"C:\personal-artwork\cover.png";
            });

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal((id, "/provider.jpg", chosen), asked);
        Assert.Equal(@"C:\personal-artwork\cover.png", Assert.Single(viewModel.Items).PosterFile);
    }

    /// <summary>
    /// A frame taken in the background (LIB-021) reaches the cards already on screen without asking
    /// the catalogue again: re-querying would put whoever is scrolling back at the top.
    /// </summary>
    [Fact]
    public async Task Refreshing_the_posters_redraws_a_new_picture_without_querying_again()
    {
        var item = new CatalogItem(
            new TitleId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            CatalogTitleKind.Movie,
            "Arrival",
            2016,
            IsAvailable: true,
            HasProgress: false,
            IsPersonal: false,
            AddedUtc: DateTimeOffset.UnixEpoch,
            LastPlayedUtc: null);
        string? onDisk = null;
        var catalogue = new OnePage(new CatalogPage([item], null));
        var viewModel = new LibraryViewModel(catalogue, findPoster: (_, _, _) => onDisk);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Null(Assert.Single(viewModel.Items).PosterFile);

        var card = Assert.Single(viewModel.Items);
        var told = new List<string?>();
        card.PropertyChanged += (_, args) => told.Add(args.PropertyName);

        onDisk = @"C:\cache\title-frames\frame.png";
        viewModel.RefreshPosters();

        Assert.Equal(@"C:\cache\title-frames\frame.png", Assert.Single(viewModel.Items).PosterFile);
        Assert.Equal(1, catalogue.Queries);

        // The same card, told: rebuilding the grid would pull the card from under whoever is pressing
        // it — a person, or the assembled walk, which the background pass runs beside.
        Assert.Same(card, Assert.Single(viewModel.Items));
        Assert.Contains(nameof(CatalogItemViewModel.PosterFile), told);
        Assert.Contains(nameof(IPosterCard.HasPoster), told);
        Assert.True(((IPosterCard)card).HasPoster);
    }

    /// <summary>With no picture changed nothing is rebuilt, so the cards on screen are left alone.</summary>
    [Fact]
    public async Task Refreshing_with_nothing_new_leaves_the_cards_as_they_are()
    {
        var item = new CatalogItem(
            new TitleId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            CatalogTitleKind.Movie,
            "Dune",
            2021,
            IsAvailable: true,
            HasProgress: false,
            IsPersonal: false,
            AddedUtc: DateTimeOffset.UnixEpoch,
            LastPlayedUtc: null);
        var viewModel = new LibraryViewModel(new OnePage(new CatalogPage([item], null)), findPoster: (_, _, _) => "same.png");
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var before = viewModel.Items;

        viewModel.RefreshPosters();

        Assert.Same(before, viewModel.Items);
    }

    private sealed class OnePage(CatalogPage page) : ICatalogQueryService
    {
        public int Queries { get; private set; }

        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default)
        {
            Queries++;
            return Task.FromResult(page);
        }
    }
}
