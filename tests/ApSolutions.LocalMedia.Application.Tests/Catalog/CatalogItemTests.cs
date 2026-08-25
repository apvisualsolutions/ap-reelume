// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Catalog;

/// <summary>
/// A row of the grid is a value, and its identity has to include everything the card paints.
/// </summary>
/// <remarks>
/// Six members arrived on 2026-08-24 with the prototype's card — a running time, the genres, the
/// watch status, how far through it is, and the two episode counts. A record answers Equals,
/// GetHashCode and ToString out of its whole member list, so a row that differed only in its running
/// time had to stop being equal to one that did not; nothing measured that, and the file's coverage
/// said so before anybody noticed. Each of the six is changed on its own here, because a comparison
/// that skipped one would call two different cards the same row — and a list that re-used the wrong
/// row would paint the wrong minutes under the right title.
/// </remarks>
public sealed class CatalogItemTests
{
    private static readonly DateTimeOffset Added = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    // One instance, shared by both rows on purpose. A record compares a collection member by
    // reference, so two rows built with separately-written genre lists are never equal however
    // equal the genres are — which is worth knowing about and is not what this test is asking.
    private static readonly string[] Genres = ["Drama", "Intriga"];

    [Fact]
    public void Two_rows_that_differ_in_any_painted_member_are_different_rows()
    {
        var item = Row();

        Assert.Equal(item, Row());
        Assert.Equal(item.GetHashCode(), Row().GetHashCode());

        foreach (var changed in new[]
        {
            item with { Runtime = TimeSpan.FromMinutes(97) },
            item with { Runtime = null },
            item with { Genres = ["Drama"] },
            item with { Genres = ["Drama", "Intriga"] },
            item with { Genres = null },
            item with { Status = WatchStatus.Watched },
            item with { CompletedFraction = 0.75 },
            item with { EpisodeCount = 12 },
            item with { EpisodesWatched = 3 },
        })
        {
            Assert.NotEqual(item, changed);
        }
    }

    /// <summary>
    /// The printed form carries them too, which is what a failing assertion shows somebody.
    /// </summary>
    [Fact]
    public void The_printed_row_names_what_the_card_paints()
    {
        var printed = Row().ToString();

        Assert.Contains("Runtime", printed, StringComparison.Ordinal);
        Assert.Contains("Genres", printed, StringComparison.Ordinal);
        Assert.Contains("Status", printed, StringComparison.Ordinal);
        Assert.Contains("CompletedFraction", printed, StringComparison.Ordinal);
        Assert.Contains("EpisodeCount", printed, StringComparison.Ordinal);
        Assert.Contains("EpisodesWatched", printed, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the six are optional on purpose: four view models build a card and only the catalogue's
    /// own query can answer all of them, so a row built without them is a row with nothing to say
    /// rather than a row that refuses to exist.
    /// </summary>
    [Fact]
    public void A_row_built_without_them_says_nothing_rather_than_refusing()
    {
        var bare = new CatalogItem(
            new TitleId(Guid.NewGuid()),
            CatalogTitleKind.Movie,
            "Arrival",
            2016,
            IsAvailable: true,
            HasProgress: false,
            IsPersonal: false,
            Added,
            LastPlayedUtc: null);

        Assert.Null(bare.Runtime);
        Assert.Null(bare.Genres);
        Assert.Equal(WatchStatus.NotStarted, bare.Status);
        Assert.Equal(0, bare.CompletedFraction);
        Assert.Equal(0, bare.EpisodeCount);
        Assert.Equal(0, bare.EpisodesWatched);
    }

    private static CatalogItem Row() => new(
        new TitleId(new Guid("11111111-1111-1111-1111-111111111111")),
        CatalogTitleKind.Show,
        "Puerto Sombra",
        2021,
        IsAvailable: true,
        HasProgress: true,
        IsPersonal: false,
        Added,
        Added,
        TimeSpan.FromMinutes(48),
        Genres,
        WatchStatus.InProgress,
        0.5,
        16,
        10);
}
