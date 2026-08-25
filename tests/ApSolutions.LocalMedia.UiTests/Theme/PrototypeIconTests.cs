// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The icons are the prototype's, and this reads the prototype to say so.
/// </summary>
/// <remarks>
/// <para>
/// «No sé qué librería de iconos estás usando pero definitivamente la del prototipo me gusta más»,
/// said on 2026-08-25. They already were — <c>Theme/Icons.axaml</c> converted them on 2026-08-24 —
/// and «already were» is exactly the kind of claim this repository has watched go stale: the
/// prototype is a file on the disk, the geometries are another file on the disk, and nothing tied
/// the two together.
/// </para>
/// <para>
/// <b>What is compared is the path data, character for character.</b> The prototype's <c>icon(n, s)</c>
/// builds each pictogram from <c>path</c>, <c>rect</c> and <c>circle</c> elements; the ones made of
/// paths alone can be compared verbatim, and they are. The ones carrying a <c>rect</c> or a
/// <c>circle</c> became the arcs that draw those shapes, so there is no string to compare and they
/// are named below as conversions rather than quietly skipped — a skip nobody wrote down is how a
/// gate becomes blind instead of red.
/// </para>
/// </remarks>
public sealed class PrototypeIconTests
{
    /// <summary>
    /// The geometries whose whole shape is <c>path</c> elements, by the name each carries in the two
    /// files. These are the ones a string can hold.
    /// </summary>
    private static readonly (string Prototype, string Token)[] Verbatim =
    [
        ("home", "IconHome"),
        ("play", "IconPlay"),
        ("back", "IconSkipBackward"),
        ("fwd", "IconSkipForward"),
        ("vol", "IconVolume"),
        ("mute", "IconMute"),
        ("full", "IconFullscreen"),
        ("exitfull", "IconExitFullscreen"),
        ("close", "IconClose"),
        ("chev", "IconChevronRight"),
        ("chevd", "IconChevronDown"),
        ("warn", "IconWarning"),
        ("check", "IconCheck"),
        ("plus", "IconAdd"),
        ("mark", "IconBookmark"),
        ("ext", "IconExternal"),
    ];

    /// <summary>
    /// The ones that carry a <c>rect</c> or a <c>circle</c> and so had to be converted into the arcs
    /// that draw them, and the two that are this application's own. Named so the set below adds up.
    /// </summary>
    private static readonly string[] Converted =
    [
        "IconLibrary", "IconReview", "IconDuplicates", "IconSettings", "IconSearch", "IconPause",
        "IconMiniPlayer", "IconFilm", "IconShow", "IconClock",
    ];

    private static readonly string[] Ours =
    [
        // A stop, because this transport has one where the prototype has a single toggle.
        "IconStop",
        // ChevronDown upside down.
        "IconChevronUp",
        // The five-pointed star of the rating, which the prototype draws with no pictogram at all.
        "IconStar",
        // The restart arc and its mirror, asked for by name on 2026-08-25.
        "IconRestart", "IconReset",
    ];

    [Theory]
    [MemberData(nameof(VerbatimShapes))]
    public void A_shape_made_of_paths_is_the_prototypes_own_string(string prototypeName, string tokenKey)
    {
        var expected = string.Join(' ', PrototypePaths()[prototypeName]);
        var actual = Geometries()[tokenKey];

        Assert.Equal(expected, actual);
    }

    public static TheoryData<string, string> VerbatimShapes()
    {
        var data = new TheoryData<string, string>();
        foreach (var (prototype, token) in Verbatim)
        {
            data.Add(prototype, token);
        }

        return data;
    }

    /// <summary>
    /// Every geometry the application declares is accounted for: copied, converted, or its own.
    /// </summary>
    /// <remarks>
    /// This is the half that makes the file above a gate rather than a list. A shape added next year
    /// from somewhere that is not the prototype fails here, which is the whole complaint being
    /// answered — one drawing tradition, not two.
    /// </remarks>
    [Fact]
    public void Every_geometry_is_accounted_for()
    {
        var declared = Geometries().Keys.Order(StringComparer.Ordinal).ToArray();
        var accounted = Verbatim.Select(pair => pair.Token)
            .Concat(Converted)
            .Concat(Ours)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(accounted, declared);
    }

    /// <summary>
    /// And the prototype still draws what this file says it draws.
    /// </summary>
    /// <remarks>
    /// Reading a file for the strings on the left is only worth anything while the file still holds
    /// them. A prototype replaced by a different one would otherwise leave every comparison above
    /// passing against a map that had quietly become empty.
    /// </remarks>
    [Fact]
    public void The_prototype_still_holds_the_shapes_this_file_reads()
    {
        var paths = PrototypePaths();

        Assert.True(paths.Count >= 30, $"the prototype's icon map yielded only {paths.Count} shapes.");
        foreach (var (prototype, _) in Verbatim)
        {
            Assert.True(paths.ContainsKey(prototype), $"the prototype no longer draws '{prototype}'.");
        }
    }

    /// <summary>
    /// The <c>d</c> attribute of every <c>path</c> in the prototype's icon map, keyed by icon name.
    /// </summary>
    /// <remarks>
    /// The map is one JavaScript object literal of the form <c>name: [p('…'), c(…), rc(…)]</c>, so
    /// the entries are cut at their names and the path strings read out of each. Anything built from
    /// a circle or a rectangle simply has fewer strings than shapes, which is what the conversion
    /// list above is for.
    /// </remarks>
    private static Dictionary<string, string[]> PrototypePaths()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot("design/AP Reelume.dc.html"));
        var start = markup.IndexOf("function icon(n, s)", StringComparison.Ordinal);
        Assert.True(start >= 0, "the prototype no longer declares its icon function.");
        var mapStart = markup.IndexOf("const m = {", start, StringComparison.Ordinal);
        var mapEnd = markup.IndexOf("\n  };", mapStart, StringComparison.Ordinal);
        Assert.True(mapStart >= 0 && mapEnd > mapStart, "the prototype's icon map could not be read.");

        var map = markup[mapStart..mapEnd];
        var shapes = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var entry in Regex.Matches(
            map,
            @"(?m)^\s{4}(?<name>\w+):\s*\[(?<body>.*)\],?\s*$",
            RegexOptions.None,
            TimeSpan.FromSeconds(2)).Cast<Match>())
        {
            shapes[entry.Groups["name"].Value] =
            [
                .. Regex.Matches(
                    entry.Groups["body"].Value,
                    @"(?:p|E\('path',\s*\{\s*d:)\s*\(?'(?<d>[^']+)'",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(2))
                    .Cast<Match>()
                    .Select(path => path.Groups["d"].Value),
            ];
        }

        return shapes;
    }

    /// <summary>Every <c>StreamGeometry</c> the theme declares, keyed by its resource key.</summary>
    private static Dictionary<string, string> Geometries()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/Icons.axaml"));
        return Regex.Matches(
            markup,
            @"<StreamGeometry x:Key=""(?<key>[^""]+)"">(?<data>[^<]+)</StreamGeometry>",
            RegexOptions.None,
            TimeSpan.FromSeconds(2))
            .Cast<Match>()
            .ToDictionary(
                match => match.Groups["key"].Value,
                match => match.Groups["data"].Value.Trim(),
                StringComparer.Ordinal);
    }
}
