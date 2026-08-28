// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Catalog;

/// <summary>
/// What an unidentified file is called on its card, over the real parser and real names.
/// </summary>
/// <remarks>
/// The parser is the real one rather than a stub, and that is the point of asserting here at all: the
/// policy is three lines and the interesting question is what those three lines do to the output of
/// the thing that already reads these names. A stub would let this pass while the two disagreed.
/// </remarks>
public sealed class ScannedTitlePolicyTests
{
    private static readonly MediaNameParser Parser = new();

    /// <summary>The name a card shows, for the names a library actually holds.</summary>
    /// <remarks>
    /// The first is the owner's own — «El Faro de Piedra 2019» was on the card, year and all, and it
    /// was asserted verbatim in <c>ScanSeriesGroupingTests</c> as a defect waiting for a decision. The
    /// rest are the shapes that come out of the places films come from.
    /// </remarks>
    [Theory]
    [InlineData("El Faro de Piedra 2019.mkv", "El Faro de Piedra", 2019)]
    [InlineData("El.Faro.de.Piedra.2019.1080p.mkv", "El Faro de Piedra", 2019)]
    [InlineData("Neon.Sobre.el.Rio.2022.2160p.mkv", "Neon Sobre el Rio", 2022)]
    [InlineData("Alta Marea Baja (2015) [720p].avi", "Alta Marea Baja", 2015)]
    public void A_name_with_a_year_in_it_puts_the_year_beside_the_title(
        string fileName,
        string expectedTitle,
        int expectedYear)
    {
        var title = Decide(fileName);

        Assert.Equal(expectedTitle, title.DisplayTitle);
        Assert.Equal(expectedYear, title.Year);
    }

    /// <summary>A name with no year keeps its words and says nothing about a year.</summary>
    [Fact]
    public void A_name_with_no_year_carries_no_year()
    {
        var title = Decide("Vacaciones en el lago.mp4");

        Assert.Equal("Vacaciones en el lago", title.DisplayTitle);
        Assert.Null(title.Year);
    }

    /// <summary>
    /// A name the parser can make nothing of keeps the file name, which is what the projection used
    /// to write always.
    /// </summary>
    /// <remarks>
    /// <c>2019.mkv</c> is a year and no title: the parser reads the year, blanks it out of the working
    /// name, and there is nothing left. A blank card is worse than a messy one, so the floor holds —
    /// <b>and the year goes with it</b>, because a card reading «2019» under a year of 2019 would be
    /// saying it twice.
    /// </remarks>
    [Theory]
    [InlineData("2019.mkv", "2019")]
    [InlineData("1080p.mkv", "1080p")]
    public void A_name_that_parses_to_nothing_keeps_the_file_name(string fileName, string expected)
    {
        var title = Decide(fileName);

        Assert.Equal(expected, title.DisplayTitle);
        Assert.Null(title.Year);
    }

    /// <summary>
    /// A name that is nothing but an extension keeps the whole name, because the column is NOT NULL.
    /// </summary>
    /// <remarks>
    /// <c>.mkv</c> is a legal file name on Windows whose stem is the empty string. It is the one input
    /// where the fallback itself falls back, and it is measured rather than argued about.
    /// </remarks>
    [Fact]
    public void A_name_that_is_only_an_extension_still_names_the_card()
    {
        var title = Decide(".mkv");

        Assert.Equal(".mkv", title.DisplayTitle);
        Assert.Null(title.Year);
    }

    /// <summary>The policy refuses what it cannot answer about.</summary>
    [Fact]
    public void The_policy_refuses_what_it_cannot_work_without()
    {
        var parsed = Parser.Parse(new FileNameContext("x.mkv", []));

        Assert.Throws<ArgumentNullException>(() => ScannedTitlePolicy.For("x.mkv", null!));
        Assert.Throws<ArgumentNullException>(() => ScannedTitlePolicy.For(null!, parsed));
        Assert.Throws<ArgumentException>(() => ScannedTitlePolicy.For("   ", parsed));
    }

    private static ScannedTitle Decide(string fileName)
    {
        var context = new FileNameContext(fileName, []);
        return ScannedTitlePolicy.For(context.FileName, Parser.Parse(context));
    }
}
