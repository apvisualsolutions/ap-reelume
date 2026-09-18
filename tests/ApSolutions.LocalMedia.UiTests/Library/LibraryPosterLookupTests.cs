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

    private sealed class OnePage(CatalogPage page) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(page);
    }
}
