// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Show;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Details;

/// <summary>
/// The synopsis reaches the cards (LIB-013).
/// </summary>
/// <remarks>
/// It was stored, merged, editable and locked long before this: the provider read it, the database
/// kept it in its own column and the metadata editor showed it. What did not exist was the read path
/// — the cards never received it — so a person could edit a synopsis they could not read anywhere
/// else. Nothing here opens a connection: the value is already on disk.
/// </remarks>
public sealed class SynopsisTests
{
    private static readonly TitleId MovieId = new(new Guid("00000000-0000-0000-0000-0000000000d1"));
    private static readonly TitleId ShowId = new(new Guid("00000000-0000-0000-0000-0000000000d2"));

    private const string Synopsis =
        "Una lingüista es reclutada para hablar con quienes acaban de llegar. / A linguist is "
        + "recruited to speak with the ones who have just arrived.";

    [Fact]
    public void A_film_card_states_the_synopsis_it_was_given()
    {
        var viewModel = new MovieDetailsViewModel();

        viewModel.Apply(Item(MovieId, CatalogTitleKind.Movie), null, null, overview: Synopsis);

        Assert.Equal(Synopsis, viewModel.Overview);
        Assert.True(viewModel.HasOverview);
    }

    [Fact]
    public void A_show_card_states_the_synopsis_it_was_given()
    {
        var viewModel = new ShowDetailsViewModel();

        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show),
            [],
            new Dictionary<ContentKey, WatchState>(),
            overview: Synopsis);

        Assert.Equal(Synopsis, viewModel.Overview);
        Assert.True(viewModel.HasOverview);
    }

    /// <summary>
    /// A title nobody identified has none, and blank is none: a block with an accessible name and
    /// nothing in it announces an empty paragraph to a screen reader, which reads as a defect rather
    /// than as an absence.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_absent_or_blank_synopsis_is_no_synopsis(string? overview)
    {
        var movie = new MovieDetailsViewModel();
        var show = new ShowDetailsViewModel();

        movie.Apply(Item(MovieId, CatalogTitleKind.Movie), null, null, overview: overview);
        show.Apply(
            Item(ShowId, CatalogTitleKind.Show),
            [],
            new Dictionary<ContentKey, WatchState>(),
            overview: overview);

        Assert.False(movie.HasOverview);
        Assert.False(show.HasOverview);
    }

    /// <summary>
    /// Both cards must bind the text, wrap it — a synopsis is a paragraph, not a label — and announce
    /// it, or a screen reader meets an unnamed block.
    /// </summary>
    [Theory]
    [InlineData("Movie/MovieDetailsView.axaml")]
    [InlineData("Show/ShowDetailsView.axaml")]
    public void Both_cards_bind_the_synopsis_and_announce_it(string view)
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src",
            "ApSolutions.LocalMedia.Presentation",
            view.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("{Binding Overview}", markup, StringComparison.Ordinal);
        Assert.Contains("{Binding HasOverview}", markup, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", markup, StringComparison.Ordinal);
        Assert.Contains("MetadataOverviewLabel", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Binding it is not reading it. The details loader is what asks the repository for the stored
    /// metadata and hands it over; without that call both cards would bind a synopsis nobody ever
    /// gives them, which is this repository's characteristic defect wearing a new hat.
    /// </summary>
    [Fact]
    public void The_details_loader_hands_the_card_the_stored_synopsis()
    {
        var composition = CompositionSourceText.Read();

        Assert.Contains("overview:", composition, StringComparison.Ordinal);
    }

    private static CatalogItem Item(TitleId id, CatalogTitleKind kind) => new(
        id,
        kind,
        "Arrival",
        2016,
        IsAvailable: true,
        HasProgress: false,
        IsPersonal: false,
        DateTimeOffset.UnixEpoch,
        null);
}
