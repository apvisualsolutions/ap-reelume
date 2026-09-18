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
    public void The_hand_picked_cover_wins_and_the_provider_follows() =>
        Assert.Equal([CoverOrigin.Personal, CoverOrigin.Provider], CoverOrderPolicy.Default);

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
}
