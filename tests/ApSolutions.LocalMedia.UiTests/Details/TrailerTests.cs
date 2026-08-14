// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
}
