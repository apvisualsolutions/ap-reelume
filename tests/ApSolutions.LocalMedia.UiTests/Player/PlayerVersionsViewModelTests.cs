// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// What a person reads when the player offers them another version, and what happens when the data
/// behind it is incomplete.
/// </summary>
/// <remarks>
/// TST-001's debt. <c>VersionSwitchWiringTests</c> covers the wiring — that a row hands its own
/// version to the use case and that an unavailable one cannot be switched to — and left the label
/// itself untouched, which is where every branch in this file lives. The gap was invisible because
/// the coverage gate only held files that were new, so this one shipped at 45% of its lines and got
/// worse in ARQ-004 without anything noticing. It is a watched file now.
/// </remarks>
public sealed class PlayerVersionsViewModelTests
{
    [Fact]
    public void A_version_that_knows_everything_about_itself_says_all_three_things()
    {
        var row = new PlayerVersionRowViewModel(Version(), _ => Task.CompletedTask);

        Assert.Equal("3840×2160 · HEVC · HDR", row.QualityLabel);
    }

    /// <summary>
    /// Resolution is one fact, not two: a file that reports a width and no height cannot be
    /// described by either, so the label leaves it out rather than inventing half of it.
    /// </summary>
    [Theory]
    [InlineData(3840, null)]
    [InlineData(null, 2160)]
    [InlineData(null, null)]
    public void Half_a_resolution_is_not_a_resolution(int? width, int? height)
    {
        var row = new PlayerVersionRowViewModel(
            Version() with { Width = width, Height = height },
            _ => Task.CompletedTask);

        Assert.Equal("HEVC · HDR", row.QualityLabel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_codec_nobody_recorded_is_left_out_instead_of_shown_blank(string codec)
    {
        var row = new PlayerVersionRowViewModel(
            Version() with { VideoCodec = codec },
            _ => Task.CompletedTask);

        Assert.Equal("3840×2160 · HDR", row.QualityLabel);
    }

    [Fact]
    public void Standard_range_is_said_by_saying_nothing()
    {
        var row = new PlayerVersionRowViewModel(Version() with { IsHdr = false }, _ => Task.CompletedTask);

        Assert.Equal("3840×2160 · HEVC", row.QualityLabel);
    }

    /// <summary>
    /// A version the catalogue knows nothing about beyond its existence. An empty label is the
    /// honest answer, and it must not be a row of separators with nothing between them.
    /// </summary>
    [Fact]
    public void A_version_that_knows_nothing_about_itself_says_nothing()
    {
        var row = new PlayerVersionRowViewModel(
            Version() with { Width = null, Height = null, VideoCodec = "", IsHdr = false },
            _ => Task.CompletedTask);

        Assert.Equal(string.Empty, row.QualityLabel);
    }

    [Fact]
    public void A_row_carries_the_version_it_was_built_from()
    {
        var version = Version();

        var row = new PlayerVersionRowViewModel(version, _ => Task.CompletedTask);

        Assert.Same(version, row.Version);
        Assert.True(row.IsAvailable);
    }

    [Fact]
    public void A_row_with_no_version_and_a_row_with_nowhere_to_switch_are_both_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlayerVersionRowViewModel(null!, _ => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() =>
            new PlayerVersionRowViewModel(Version(), null!));
    }

    [Fact]
    public void A_surface_with_no_list_at_all_is_refused_rather_than_left_empty()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerVersionsViewModel(null!));
    }

    private static MediaVersion Version() => new(
        new MediaFileId(Guid.NewGuid()),
        @"R:\media\film-4k.mkv",
        IsAvailable: true,
        TimeSpan.FromMinutes(100),
        3840,
        2160,
        IsHdr: true,
        "HEVC",
        4_000_000_000);
}
