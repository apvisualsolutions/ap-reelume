// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

/// <summary>
/// The order in which a title's covers are asked for (ADR-0009): the one somebody picked wins, the
/// provider's follows.
/// </summary>
public sealed class CoverOrderPolicyTests
{
    [Fact]
    public void The_hand_picked_cover_wins_the_provider_follows_and_the_frame_comes_last() =>
        Assert.Equal([CoverOrigin.Personal, CoverOrigin.Provider, CoverOrigin.Frame], CoverOrderPolicy.Default);

    /// <summary>
    /// An origin missing from the order is an origin no title can ever draw, and one listed twice is a
    /// sign the list was edited by hand. Both fail here rather than on somebody's screen.
    /// </summary>
    [Fact]
    public void Every_origin_appears_exactly_once()
    {
        Assert.Equal(Enum.GetValues<CoverOrigin>().Length, CoverOrderPolicy.Default.Count);
        Assert.Equal(CoverOrderPolicy.Default.Count, CoverOrderPolicy.Default.Distinct().Count());
    }

    /// <summary>
    /// Nothing stored is nothing to obey. A settings file that has never been written and a column
    /// that has never been set both arrive here, and both have to leave walkable.
    /// </summary>
    [Fact]
    public void A_null_or_empty_order_falls_back_to_the_default()
    {
        Assert.Equal(CoverOrderPolicy.Default, CoverOrderPolicy.Normalize(null));
        Assert.Equal(CoverOrderPolicy.Default, CoverOrderPolicy.Normalize([]));
    }

    /// <summary>
    /// An origin left out of a stored order is a cover that exists and can never be drawn, with
    /// nothing saying so — which is why it is put back rather than honoured as written.
    /// </summary>
    [Fact]
    public void An_order_missing_an_origin_gets_it_back_at_the_end()
    {
        Assert.Equal(
            [CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider],
            CoverOrderPolicy.Normalize([CoverOrigin.Frame]));
    }

    [Fact]
    public void A_repeated_origin_keeps_only_its_first_place()
    {
        Assert.Equal(
            [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame],
            CoverOrderPolicy.Normalize([CoverOrigin.Provider, CoverOrigin.Provider, CoverOrigin.Personal]));
    }

    /// <summary>The control that must stay still: a complete order is not rearranged.</summary>
    [Fact]
    public void A_complete_order_comes_back_untouched()
    {
        IReadOnlyList<CoverOrigin> given = [CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal];

        Assert.Equal(given, CoverOrderPolicy.Normalize(given));
    }

    /// <summary>
    /// A value outside the enum reaches this through a settings file or a row written by a build
    /// that knew another origin; it is dropped rather than walked into the switch.
    /// </summary>
    [Fact]
    public void An_origin_that_is_not_one_is_dropped()
    {
        Assert.Equal(
            [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame],
            CoverOrderPolicy.Normalize([(CoverOrigin)42, CoverOrigin.Provider]));
    }

    [Fact]
    public void An_order_survives_the_round_trip_through_text()
    {
        var text = CoverOrderPolicy.Format([CoverOrigin.Provider, CoverOrigin.Frame, CoverOrigin.Personal]);

        Assert.Equal("Provider,Frame,Personal", text);
        Assert.True(CoverOrderPolicy.TryParse(text, out var parsed));
        Assert.Equal([CoverOrigin.Provider, CoverOrigin.Frame, CoverOrigin.Personal], parsed);
    }

    /// <summary>
    /// The refused cases hand back the default anyway, so a caller that ignores the verdict still
    /// gets a list it can walk instead of an empty one that draws nothing.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData("Provider,Nonsense")]
    [InlineData("42")]
    [InlineData("provider")]
    public void Text_that_does_not_name_origins_is_refused_and_yields_the_default(string? text)
    {
        Assert.False(CoverOrderPolicy.TryParse(text, out var parsed));
        Assert.Equal(CoverOrderPolicy.Default, parsed);
    }

    [Fact]
    public void Parsing_normalizes_what_it_reads()
    {
        Assert.True(CoverOrderPolicy.TryParse("Frame", out var parsed));
        Assert.Equal([CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider], parsed);
    }

    /// <summary>
    /// What a per-title override means: this origin wins, and the rest keep the places they had, so
    /// that moving the general order later cannot change what this title was told to do.
    /// </summary>
    [Fact]
    public void Putting_one_origin_first_keeps_the_rest_in_the_order_given()
    {
        Assert.Equal(
            [CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal],
            CoverOrderPolicy.WithFirst(CoverOrigin.Frame, [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame]));
    }

    [Fact]
    public void Putting_one_origin_first_with_no_rest_falls_back_to_the_default_order()
    {
        Assert.Equal(
            [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame],
            CoverOrderPolicy.WithFirst(CoverOrigin.Provider, null));
    }

    [Fact]
    public void Putting_the_origin_that_already_leads_first_changes_nothing()
    {
        Assert.Equal(
            CoverOrderPolicy.Default,
            CoverOrderPolicy.WithFirst(CoverOrigin.Personal, CoverOrderPolicy.Default));
    }
}
