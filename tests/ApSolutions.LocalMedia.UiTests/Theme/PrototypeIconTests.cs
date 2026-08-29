// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
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
    /// The prototype's <c>viewBox</c>, which every geometry carries in front of its stroke.
    /// </summary>
    /// <remarks>
    /// Two movetos that draw nothing. The conversion of 2026-08-24 copied the strokes and left the
    /// box behind, and the comparison below never noticed because a stroke is all it ever read: the
    /// prototype declares its box in the <c>svg</c> element and this file was reading the <c>path</c>
    /// elements inside it. So it is stripped before comparing rather than compared — the string on
    /// the right is still the prototype's own, character for character — and what the box itself is
    /// worth is asserted separately, below, where it can be measured instead of spelled.
    /// </remarks>
    private const string Canvas = "M0 0 M24 24 ";

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
        var declared = Geometries()[tokenKey];

        Assert.True(
            declared.StartsWith(Canvas, StringComparison.Ordinal),
            $"{tokenKey} does not carry the prototype's 24 unit box in front of its stroke.");

        Assert.Equal(expected, declared[Canvas.Length..]);
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
    /// Every geometry occupies the prototype's whole 24 unit box, measured rather than spelled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes the defect impossible for icon number thirty-two. The check above reads
    /// the prefix as a string, and a string is exactly what was there for five days while the icons
    /// drew wrong: the box has to be <b>parsed</b> for the bounds to mean anything, because a
    /// typo, a shape that reaches past 24, or a future geometry pasted in without the prefix all
    /// leave a stroke that still looks fine to a comparison of characters.
    /// </para>
    /// <para>
    /// <c>Stretch</c> <c>Uniform</c> fits the geometry's own bounds into the control and anchors what
    /// is left over at the top left, so a geometry whose bounds are not the full box is drawn both
    /// larger and off centre — by a different amount for every shape, which is why nothing that
    /// compared icons against each other caught it either. Measured on 2026-08-29, before the prefix:
    /// <c>IconClose</c> spanned 11.6 of 24 and came out 1.86x too large, <c>IconHome</c> 18 of 24 and
    /// 1.20x.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_geometry_measures_the_prototypes_own_canvas()
    {
        var geometries = Geometries();

        // A gate whose subject went missing agrees with itself perfectly: an empty dictionary would
        // walk this loop zero times and pass.
        Assert.True(geometries.Count >= 30, $"only {geometries.Count} geometries were read.");

        foreach (var (key, data) in geometries)
        {
            var bounds = Geometry.Parse(data).Bounds;

            Assert.True(
                bounds.X == 0 && bounds.Y == 0 && bounds.Width == 24 && bounds.Height == 24,
                $"{key} measures {bounds.X:0.00},{bounds.Y:0.00} {bounds.Width:0.00}x{bounds.Height:0.00} "
                    + "where the prototype's box is 0,0 24x24. Stretch Uniform scales whatever bounds "
                    + "it is given up to the control and pins the remainder top left, so this glyph is "
                    + "drawn larger than the prototype draws it and off centre inside its own button.");
        }
    }

    /// <summary>
    /// Every size class is the size it is named for, and every one of them is a size the prototype
    /// actually spends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two halves, and each one caught a real defect. <b>The name is the number</b>: from 2026-08-25
    /// to 2026-08-29 <c>size-20</c> set <c>Width="18"</c>, a two pixel subtraction applied by eye
    /// against an excess that was a different factor for every icon. With the canvas restored there
    /// is nothing left to compensate, and a class whose name lies about its size is how that comes
    /// back.
    /// </para>
    /// <para>
    /// <b>And the number is the prototype's</b>, read from <c>design/</c> rather than listed here, so
    /// a class invented for this application fails instead of quietly becoming the house style. The
    /// pattern has to accept an <b>expression</b> as the first argument: on 2026-08-29 a count that
    /// required a string literal missed ten calls — among them
    /// <c>icon(p.playing &amp;&amp; !err ? 'pause' : 'play', 22)</c> — and produced the confident,
    /// false claim that the prototype never used 22. An absence is measured, never inferred from a
    /// pattern that happened to match nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_size_class_is_its_own_number_and_one_the_prototype_spends()
    {
        var classes = SizeClasses();
        var spent = PrototypeSizes();

        // Both sides can go silently empty: a renamed selector, a markup change, a pattern that
        // stopped matching. Two empty sets agree with each other perfectly.
        Assert.True(classes.Count >= 4, $"only {classes.Count} size classes were read from the theme.");
        Assert.True(spent.Count >= 5, $"only {spent.Count} icon sizes were read from the prototype.");

        foreach (var (name, width) in classes)
        {
            Assert.True(
                name == width,
                $"Path.icon.size-{name} sets Width=\"{width}\". The class name is the size the "
                    + "prototype draws that glyph at, and with the 24 unit canvas restored there is "
                    + "nothing left for a difference between the two to compensate.");

            Assert.True(
                spent.Contains(width),
                $"Path.icon.size-{name} is not a size the prototype spends; it draws at "
                    + $"{string.Join(", ", spent.Order())}.");
        }
    }

    /// <summary>Each <c>Path.icon.size-N</c> the theme declares, as its name and the width it sets.</summary>
    private static List<(int Name, int Width)> SizeClasses()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml"));

        return
        [
            .. Regex.Matches(
                markup,
                """<Style Selector="Path\.icon\.size-(?<name>\d+)">\s*<Setter Property="Width" Value="(?<width>\d+)" />""",
                RegexOptions.None,
                TimeSpan.FromSeconds(2))
                .Cast<Match>()
                .Select(match => (
                    int.Parse(match.Groups["name"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["width"].Value, CultureInfo.InvariantCulture))),
        ];
    }

    /// <summary>Every size the prototype passes to <c>icon(n, s)</c>, expressions included.</summary>
    private static HashSet<int> PrototypeSizes()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot("design/AP Reelume.dc.html"));

        return
        [
            .. Regex.Matches(
                markup,
                @"\bicon\([^)]*,\s*(?<size>\d+)\)",
                RegexOptions.None,
                TimeSpan.FromSeconds(2))
                .Cast<Match>()
                .Select(match => int.Parse(match.Groups["size"].Value, CultureInfo.InvariantCulture)),
        ];
    }

    /// <summary>
    /// One glyph in one role is one size, across every view that draws it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>IconPlay</c> is drawn in seven places and plays two roles: the transport's toggle, which
    /// the prototype draws at 22, and the play on a catalogue action — «Continuar», «Reproducir» —
    /// which it draws in five views that a reader moves between. <b>Those five have to agree</b>, and
    /// on 2026-08-29 they did not: an alignment pass moved the size in three of them, two were put
    /// back after measuring the cost, and <c>MovieDetailsView</c> was missed. The same button, one
    /// pixel different depending on which screen you reached it from — worse than either size.
    /// </para>
    /// <para>
    /// The list is closed on purpose, the way <c>LeadingActionTests</c> keeps its table of views: a
    /// sixth view drawing a catalogue play fails here until somebody adds it and says which size it
    /// takes. A count that only asked «are they all equal?» would pass the day the last of them is
    /// deleted.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_play_of_a_catalogue_action_is_one_size_in_every_view_that_draws_it()
    {
        string[] views =
        [
            "Home/ContinueCardView.axaml",
            "Home/ResumeHeroView.axaml",
            "Movie/MovieDetailsView.axaml",
            "Show/EpisodeRowView.axaml",
            "Show/ShowDetailsView.axaml",
        ];

        var sizes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var view in views)
        {
            var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
                "src/ApSolutions.LocalMedia.Presentation/" + view));

            var match = Regex.Match(
                markup,
                @"Classes=""icon (?<size>size-\d+) filled""[^>]*Data=""\{DynamicResource IconPlay\}""",
                RegexOptions.None,
                TimeSpan.FromSeconds(2));

            Assert.True(match.Success, $"{view} no longer draws a filled IconPlay; the table is stale.");
            sizes[view] = match.Groups["size"].Value;
        }

        var distinct = sizes.Values.Distinct(StringComparer.Ordinal).ToArray();
        Assert.True(
            distinct.Length == 1,
            "the play of a catalogue action is drawn at more than one size: "
                + string.Join(", ", sizes.Select(pair => $"{pair.Key} {pair.Value}"))
                + ". A reader moves between these five views and compares the same button against "
                + "itself.");
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
