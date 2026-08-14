// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

/// <summary>
/// Which file next to a film is its trailer (LIB-014).
/// </summary>
/// <remarks>
/// A trailer plays here only when it is already a file on the disk, next to the film, in one of the
/// conventions Plex, Jellyfin and Kodi all write: <c>&lt;name&gt;-trailer.&lt;ext&gt;</c> beside it, or
/// a <c>Trailers</c> folder under its own. The remote one is a YouTube key and belongs in a browser.
/// <para>
/// The decision is pure on purpose: it names a candidate, and the use case that opens it is the same
/// <c>OpenLooseFile</c> a person gets from Explorer — which already refuses an extension outside the
/// approved list and never writes a catalogue row. What this policy must never do is name something
/// outside the film's own folder.
/// </para>
/// </remarks>
public sealed class TrailerDiscoveryPolicyTests
{
    private const string Film = @"D:\media\Arrival (2016)\Arrival (2016).mkv";
    private const string Folder = @"D:\media\Arrival (2016)";

    [Fact]
    public void The_sibling_named_after_the_film_is_the_trailer()
    {
        var trailer = Path.Combine(Folder, "Arrival (2016)-trailer.mkv");

        Assert.Equal(trailer, TrailerDiscoveryPolicy.Select(Film, [Film, trailer]));
    }

    [Fact]
    public void The_suffix_is_recognised_whatever_its_casing()
    {
        var trailer = Path.Combine(Folder, "Arrival (2016)-Trailer.MKV");

        Assert.Equal(trailer, TrailerDiscoveryPolicy.Select(Film, [trailer]));
    }

    [Fact]
    public void A_trailers_folder_answers_when_no_sibling_does()
    {
        var trailer = Path.Combine(Folder, "Trailers", "teaser.mp4");

        Assert.Equal(trailer, TrailerDiscoveryPolicy.Select(Film, [trailer]));
    }

    /// <summary>The sibling convention is the explicit one, so it wins over a folder of many.</summary>
    [Fact]
    public void A_sibling_beats_a_folder()
    {
        var sibling = Path.Combine(Folder, "Arrival (2016)-trailer.mkv");
        var inFolder = Path.Combine(Folder, "Trailers", "teaser.mp4");

        Assert.Equal(sibling, TrailerDiscoveryPolicy.Select(Film, [inFolder, sibling]));
    }

    /// <summary>Two candidates must not depend on the order the file system listed them.</summary>
    [Fact]
    public void Several_candidates_resolve_to_the_same_one_either_way()
    {
        var first = Path.Combine(Folder, "Trailers", "a-teaser.mp4");
        var second = Path.Combine(Folder, "Trailers", "b-teaser.mp4");

        Assert.Equal(
            TrailerDiscoveryPolicy.Select(Film, [first, second]),
            TrailerDiscoveryPolicy.Select(Film, [second, first]));
    }

    [Theory]
    [InlineData("Arrival (2016)-trailer.txt")]
    [InlineData("Arrival (2016)-trailer.exe")]
    [InlineData("Arrival (2016)-trailer.ps1")]
    public void An_extension_outside_the_approved_list_is_not_a_trailer(string name)
    {
        Assert.Null(TrailerDiscoveryPolicy.Select(Film, [Path.Combine(Folder, name)]));
    }

    [Fact]
    public void The_film_itself_is_never_its_own_trailer()
    {
        Assert.Null(TrailerDiscoveryPolicy.Select(Film, [Film]));
    }

    /// <summary>
    /// A folder holds more than one film often enough, and extras are kept beside them. Being a
    /// playable file next to this one is not being its trailer: the name is what says so.
    /// </summary>
    [Theory]
    [InlineData("Arrival (2016) - Making Of.mkv")]
    [InlineData("Sicario (2015).mkv")]
    [InlineData("Arrival (2016)-sample.mkv")]
    public void A_playable_sibling_that_does_not_follow_the_convention_is_not_a_trailer(string name)
    {
        Assert.Null(TrailerDiscoveryPolicy.Select(Film, [Path.Combine(Folder, name)]));
    }

    /// <summary>
    /// The guard that matters. A candidate list is whatever the caller listed, and a policy that
    /// trusts it would hand LibVLC a path from anywhere — the decoder is this project's largest
    /// accepted risk, and it is not going to be fed from outside the film's own folder.
    /// </summary>
    [Theory]
    [InlineData(@"D:\media\Other (2019)\Other (2019)-trailer.mkv")]
    [InlineData(@"D:\media\Arrival (2016)\Trailers\deep\teaser.mkv")]
    [InlineData(@"C:\Windows\Temp\Arrival (2016)-trailer.mkv")]
    public void Nothing_outside_the_films_own_folder_is_ever_named(string candidate)
    {
        Assert.Null(TrailerDiscoveryPolicy.Select(Film, [candidate]));
    }

    [Fact]
    public void No_candidates_is_no_trailer()
    {
        Assert.Null(TrailerDiscoveryPolicy.Select(Film, []));
    }

    /// <summary>
    /// A listing can carry an empty entry, and asking the file system what folder it belongs to
    /// throws for one. Skipped rather than resolved.
    /// </summary>
    [Fact]
    public void A_blank_entry_in_the_listing_is_skipped_rather_than_resolved()
    {
        var trailer = Path.Combine(Folder, "Arrival (2016)-trailer.mkv");

        Assert.Equal(trailer, TrailerDiscoveryPolicy.Select(Film, ["", "   ", trailer]));
    }

    /// <summary>
    /// Two siblings can both follow the convention with different containers, and the answer must
    /// not depend on which one the directory happened to list first.
    /// </summary>
    [Fact]
    public void Two_siblings_that_both_follow_the_convention_resolve_the_same_either_way()
    {
        var mkv = Path.Combine(Folder, "Arrival (2016)-trailer.mkv");
        var mp4 = Path.Combine(Folder, "Arrival (2016)-trailer.mp4");

        Assert.Equal(
            TrailerDiscoveryPolicy.Select(Film, [mkv, mp4]),
            TrailerDiscoveryPolicy.Select(Film, [mp4, mkv]));
    }

    /// <summary>
    /// The degenerate shapes a path can take. A root has no folder to look in, and a root as a
    /// candidate has no folder to compare — both are answered with silence rather than an exception,
    /// because this policy is asked about whatever the disk listed.
    /// </summary>
    [Fact]
    public void A_root_has_no_folder_to_look_in_and_none_to_compare()
    {
        var root = Path.GetPathRoot(Film)!;

        Assert.Null(TrailerDiscoveryPolicy.Select(root, [Path.Combine(Folder, "Arrival (2016)-trailer.mkv")]));
        Assert.Null(TrailerDiscoveryPolicy.Select(Film, [root]));
    }
}
