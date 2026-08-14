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
/// The film card offers the trailer that is already on the disk (LIB-014).
/// </summary>
/// <remarks>
/// Only a local file. A trailer from the provider is a YouTube key, and playing that inside the
/// application would mean reaching the video by a route YouTube's terms do not allow, so the key
/// belongs in a browser. What plays here is a file the person already has, opened by the same use
/// case Explorer's "open with" uses — which refuses an extension outside the approved list and never
/// writes a catalogue row.
/// </remarks>
public sealed class TrailerTests
{
    private static readonly TitleId MovieId = new(new Guid("00000000-0000-0000-0000-0000000000e1"));
    private static readonly TitleId ShowId = new(new Guid("00000000-0000-0000-0000-0000000000e2"));

    [Fact]
    public void The_film_card_offers_the_trailer_only_when_there_is_one()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Movie",
            "MovieDetailsView.axaml"));

        Assert.Contains("{Binding PlayTrailerCommand}", markup, StringComparison.Ordinal);
        Assert.Contains("{Binding HasTrailer}", markup, StringComparison.Ordinal);
        Assert.Contains("MovieTrailerAction", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The action has to exist in both languages, like every visible string here, and a screen reader
    /// has to be able to announce the button.
    /// </summary>
    [Theory]
    [InlineData("Strings.es.axaml")]
    [InlineData("Strings.en.axaml")]
    public void The_action_is_named_in_both_languages(string dictionary)
    {
        var strings = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Resources",
            dictionary));

        Assert.Contains("MovieTrailerAction", strings, StringComparison.Ordinal);
    }

    /// <summary>
    /// Offering it is not opening it. The composition is what lists the folder, asks the policy and
    /// hands the answer over; without that, the button would be bound to a command nobody feeds —
    /// this repository's characteristic defect, which is why the wiring is asserted and not assumed.
    /// </summary>
    [Fact]
    public void The_composition_asks_the_policy_and_opens_the_trailer_as_a_loose_file()
    {
        var composition = CompositionSourceText.Read();

        Assert.Contains("TrailerDiscoveryPolicy.Select", composition, StringComparison.Ordinal);
        Assert.Contains("onPlayTrailer", composition, StringComparison.Ordinal);
    }

    /// <summary>
    /// The provider's trailer is a second, separate offer (LIB-015): it leaves the application
    /// instead of playing in it, and both cards make it, because TMDB has videos for series too.
    /// </summary>
    [Theory]
    [InlineData("Movie", "MovieDetailsView.axaml")]
    [InlineData("Show", "ShowDetailsView.axaml")]
    public void Both_cards_offer_the_provider_trailer_only_when_there_is_a_key(string folder, string view)
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src",
            "ApSolutions.LocalMedia.Presentation",
            folder,
            view));

        Assert.Contains("{Binding OpenTrailerLinkCommand}", markup, StringComparison.Ordinal);
        Assert.Contains("{Binding HasTrailerLink}", markup, StringComparison.Ordinal);
        Assert.Contains("DetailsTrailerLinkAction", markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Strings.es.axaml")]
    [InlineData("Strings.en.axaml")]
    public void The_browser_action_is_named_in_both_languages(string dictionary)
    {
        var strings = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Resources",
            dictionary));

        Assert.Contains("DetailsTrailerLinkAction", strings, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same wiring question as the local trailer, and the same answer: a command nobody feeds is
    /// this repository's characteristic defect. The composition has to read the stored key, hand it
    /// to both cards, and register the launcher that opens it.
    /// </summary>
    [Fact]
    public void The_composition_reads_the_stored_key_and_registers_something_that_can_open_it()
    {
        var composition = CompositionSourceText.Read();

        Assert.Contains("TrailerKey", composition, StringComparison.Ordinal);
        Assert.Contains("onOpenTrailerLink", composition, StringComparison.Ordinal);
        Assert.Contains("IExternalLinkLauncher", composition, StringComparison.Ordinal);
    }

    /// <summary>
    /// The exact address, pinned. A card holds a key and composes the link through the policy, so
    /// this is where "a string from the network cannot steer the browser" stops being a claim about
    /// the policy and becomes one about what the button actually does.
    /// </summary>
    [Fact]
    public void The_film_card_opens_exactly_the_watch_address_for_the_stored_key()
    {
        var opened = new List<string>();
        var viewModel = new MovieDetailsViewModel(onOpenTrailerLink: link =>
        {
            opened.Add(link);
            return Task.CompletedTask;
        });

        viewModel.Apply(Item(MovieId, CatalogTitleKind.Movie), null, null, trailerKey: "dQw4w9WgXcQ");
        viewModel.OpenTrailerLinkCommand.Execute(null);

        Assert.True(viewModel.HasTrailerLink);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", Assert.Single(opened));
    }

    [Fact]
    public void The_show_card_opens_exactly_the_watch_address_for_the_stored_key()
    {
        var opened = new List<string>();
        var viewModel = new ShowDetailsViewModel(onOpenTrailerLink: link =>
        {
            opened.Add(link);
            return Task.CompletedTask;
        });

        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show),
            [],
            new Dictionary<ContentKey, WatchState>(),
            trailerKey: "dQw4w9WgXcQ");
        viewModel.OpenTrailerLinkCommand.Execute(null);

        Assert.True(viewModel.HasTrailerLink);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", Assert.Single(opened));
    }

    /// <summary>
    /// A key that is not a key offers nothing, and pressing anyway opens nothing. The stored value
    /// came from outside, so "the button is hidden" cannot be the only thing standing between it and
    /// a browser.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dQw4w9WgXc")]
    [InlineData("https://example.invalid/")]
    [InlineData("dQw4w9Wg/cQ")]
    public void A_key_that_is_not_a_key_offers_nothing_and_opens_nothing(string? trailerKey)
    {
        var opened = new List<string>();
        Task Record(string link)
        {
            opened.Add(link);
            return Task.CompletedTask;
        }

        var movie = new MovieDetailsViewModel(onOpenTrailerLink: Record);
        var show = new ShowDetailsViewModel(onOpenTrailerLink: Record);

        movie.Apply(Item(MovieId, CatalogTitleKind.Movie), null, null, trailerKey: trailerKey);
        show.Apply(
            Item(ShowId, CatalogTitleKind.Show),
            [],
            new Dictionary<ContentKey, WatchState>(),
            trailerKey: trailerKey);
        movie.OpenTrailerLinkCommand.Execute(null);
        show.OpenTrailerLinkCommand.Execute(null);

        Assert.False(movie.HasTrailerLink);
        Assert.False(show.HasTrailerLink);
        Assert.Empty(opened);
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
