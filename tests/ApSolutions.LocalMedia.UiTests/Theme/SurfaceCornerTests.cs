// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Every surface that is not a button draws the corner the prototype draws for it.
/// </summary>
/// <remarks>
/// ADR-0007 says "every element", and <c>ButtonShapeTests</c> covers the fourteen button classes.
/// This is the rest of what declares a corner of its own: the <c>Border</c>s, the search field, the
/// filter pill and the side list's row.
/// <para>
/// <b>Paired by element and never by number, and that is measured rather than stylistic.</b> The
/// design's small radii carry two meanings depending on where they sit: 7 is the side list's row and
/// also half of the 14 px knob inside a switch; 10 is the settings row and also half of that
/// switch's 40×20 track. Several of the design's "twelve distinct radii" are one decision — the pill
/// — written as whatever half the height happens to be: 26 on a 52 px circle, 16 on a 32 px button,
/// 15 on a 30 px one. A table keyed on the number would pair a row with a knob and be perfectly
/// self-consistent while doing it.
/// </para>
/// <para>
/// The two halves are <c>ButtonShapeTests</c>' and for its reasons: the tree draws what the table
/// says, and the table says what the design draws — read out of the design rather than restated,
/// because a hand-copied number is how the withdrawn shape rule survived a week.
/// </para>
/// </remarks>
public sealed class SurfaceCornerTests
{
    /// <summary>
    /// Each surface class, the prototype element it draws, and how that element is found in the
    /// design.
    /// </summary>
    /// <remarks>
    /// The pattern travels with the pairing rather than being derived from the class name, because
    /// the design writes these every which way — an object literal, a factory taking a tone, CSS in
    /// a style attribute — and one clever pattern over all of them matches the wrong thing the day
    /// one of them moves.
    /// </remarks>
    private static readonly Surface[] Pairings =
    [
        new(
            "Border.setting-row",
            "the settings row",
            10,
            @"padding: '13px 16px', border: '1px solid var\(--hair,rgba\(15,23,42,\.09\)\)', borderRadius: (?<radius>[0-9]+)"),
        new(
            "Border.candidate-card",
            "the accepted candidate's card",
            10,
            @"border-radius:(?<radius>[0-9]+)px;background:var\(--ok-bg"),
        new(
            "Border.state-chip",
            "the state tag",
            999,
            @"const tag = \(tone\) => \(\{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        new(
            "Border.poster-chip",
            "the kind badge over the cover",
            999,
            @"const kindBadge = \{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        new(
            "ListBox.side-list ListBoxItem",
            "the side list's row",
            7,
            @"minHeight: 32, padding: '5px 9px', borderRadius: (?<radius>[0-9]+)"),
        new(
            "TextBox.search-field",
            "the library's search box",
            999,
            @"type=""search""[^>]*border-radius:(?<radius>[0-9]+)px"),
        new(
            "ComboBox.filter-pill",
            "the library's filter",
            999,
            @"height: 34, padding: '0 14px', borderRadius: (?<radius>[0-9]+)"),
        new(
            "Border.rail-badge",
            "the rail's count",
            999,
            @"minWidth: 17, height: 17, padding: '0 5px', borderRadius: (?<radius>[0-9]+)"),
    ];

    /// <summary>
    /// The surfaces that declare a corner and are not paired here, each with the reason.
    /// </summary>
    /// <remarks>
    /// A closed list for <c>ButtonShapeTests</c>' reason: without one, a class nobody paired is
    /// indistinguishable from a class nobody got round to pairing.
    /// </remarks>
    private static readonly (string Selector, string Reason)[] Unpaired =
    [
        ("Border.apr-shim",
            "the cover's skeleton, and it is the one surface whose corner is SUPPOSED to follow the "
            + "«Redondeo de esquinas» preference: ADR-0007's third consequence says a preference "
            + "reaches only the elements the prototype gives it, and the prototype spends it on "
            + "artBox — the cover — alone. Pairing it with a fixed number here would undo that."),
        ("Border.option-row",
            "the player panel's option row. It is paired with the design — the prototype draws it at "
            + "4 — but by OptionRowShapeTests, which measures the Border that actually draws the "
            + "corner rather than the RadioButton that names the row. Repeating the pairing here "
            + "would be a second copy of one number, which is how two of three end up agreeing."),
    ];

    /// <summary>Every surface draws the corner its prototype element draws.</summary>
    /// <remarks>
    /// Measured on a built control with the appearance service running, which is what startup does.
    /// Reading the token file instead is exactly how <c>ButtonShapeTests</c> once certified two
    /// numbers nobody could see — the rounding preference was written over both corner tokens before
    /// any surface was built.
    /// </remarks>
    [AvaloniaFact]
    public void Every_surface_draws_the_corner_the_prototype_draws()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        var offenders = new List<string>();
        foreach (var pairing in Pairings)
        {
            var drawn = Corner(pairing.Selector);

            // Four corners and not one: a class that rounded three of them would satisfy any
            // comparison written against the first.
            if (drawn.TopLeft != drawn.TopRight
                || drawn.TopLeft != drawn.BottomLeft
                || drawn.TopLeft != drawn.BottomRight)
            {
                offenders.Add($"{pairing.Selector} draws four different corners: {drawn}");
                continue;
            }

            if (drawn.TopLeft != pairing.Radius)
            {
                offenders.Add($"{pairing.Selector} draws {drawn.TopLeft}, and {pairing.Element} draws {pairing.Radius}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A surface draws the corner its prototype element draws: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// The two literals this batch wrote are visible: a 10 px corner cuts more ink from a box than
    /// an 8 px one, and a 7 px row cuts more than a 4 px one.
    /// </summary>
    /// <remarks>
    /// The measurement above reads a property, and a property can differ while the screen does not.
    /// This repository has two gates that were green over two pixels of visible misalignment for
    /// exactly that reason, and two pixels is also the whole difference between the medium token and
    /// what the settings row should draw — so the question «would anybody see this?» is asked rather
    /// than assumed.
    /// <para>
    /// <b>Counted as ink missing from the corner, not as a radius read back.</b> A rounded box paints
    /// its own fill everywhere except the corner it cuts away, so a bigger radius leaves more of the
    /// backdrop showing in the same square. That is what a person sees, and it is the one reading a
    /// clamped or ignored radius cannot fake.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(8, 10)]
    [InlineData(4, 7)]
    public void A_bigger_radius_cuts_more_ink_from_the_corner(int smaller, int bigger)
    {
        var small = CornerInk(smaller);
        var large = CornerInk(bigger);

        // Anti-blindness floor: a box that painted nothing would report no ink in either corner and
        // satisfy any comparison between them.
        Assert.True(small > 0, $"the {smaller} px box cut no corner at all, so this measured nothing.");

        Assert.True(
            large > small,
            $"a {bigger} px radius cuts {large} px of the corner and an {smaller} px one cuts {small}: "
            + "the difference this batch wrote is not reaching the screen.");
    }

    /// <summary>
    /// How many pixels of the top-left corner square are NOT the box's own fill.
    /// </summary>
    /// <remarks>
    /// The scene paints its own black on its own white rather than taking the theme's inks, so the
    /// threshold survives a change of palette.
    /// </remarks>
    private static int CornerInk(int radius)
    {
        var box = new Border
        {
            Width = 120,
            Height = 60,
            CornerRadius = new CornerRadius(radius),
            Background = Avalonia.Media.Brushes.Black,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var host = new Border
        {
            Background = Avalonia.Media.Brushes.White,
            Child = box,
            Padding = new Thickness(0),
        };
        var window = new Window { Width = 160, Height = 100, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("the headless backend returned no frame.");
        using var buffer = frame.Lock();
        var pixels = frame.PixelSize;
        var bytes = new byte[buffer.RowBytes * pixels.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

        // The corner square is as wide as the largest radius under test, so both readings cover the
        // same area and the difference between them is the corner and nothing else.
        var pale = 0;
        for (var y = 0; y < Math.Min(12, pixels.Height); y++)
        {
            for (var x = 0; x < Math.Min(12, pixels.Width); x++)
            {
                var i = (y * buffer.RowBytes) + (x * 4);
                if (bytes[i] > 128 && bytes[i + 1] > 128 && bytes[i + 2] > 128)
                {
                    pale++;
                }
            }
        }

        window.Close();
        return pale;
    }

    /// <summary>The radii the table claims are the ones the design writes.</summary>
    /// <remarks>
    /// Without this half the table is a second set of hand-copied numbers and a pairing that drifts
    /// from the design certifies itself.
    /// </remarks>
    [Fact]
    public void The_pairings_name_the_radius_the_design_writes()
    {
        var design = File.ReadAllText(RepositoryLayout.PathFromRoot("design/AP Reelume.dc.html"));

        foreach (var pairing in Pairings)
        {
            var match = Regex.Match(design, pairing.Pattern, RegexOptions.None, TimeSpan.FromSeconds(5));

            Assert.True(
                match.Success,
                $"the design no longer draws {pairing.Element}, so {pairing.Selector} is paired with nothing.");
            Assert.Equal(
                pairing.Radius,
                int.Parse(match.Groups["radius"].Value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Every non-button class that declares a corner is in one table or the other.
    /// </summary>
    /// <remarks>
    /// The half ADR-0007 was missing when it left ten button classes unpaired without anything going
    /// red: a gate over a hand-written list measures only what somebody remembered to list.
    /// </remarks>
    [Fact]
    public void Every_surface_that_declares_a_corner_is_accounted_for()
    {
        var declared = CornerDeclaringSelectors();

        // Anti-blindness floor: a reader that found nothing would pass by measuring nothing.
        Assert.True(
            declared.Count >= 10,
            $"only {declared.Count} corner-declaring selectors were read; this reads the wrong file.");

        var accounted = Pairings
            .Select(pairing => pairing.Selector)
            .Concat(Unpaired.Select(entry => entry.Selector))
            .ToHashSet(StringComparer.Ordinal);

        var missing = declared.Where(selector => !accounted.Contains(selector)).ToArray();
        Assert.True(
            missing.Length == 0,
            "Every surface that declares a corner is paired with a prototype element, or written "
            + "into the unpaired list with its reason: " + string.Join(", ", missing));

        var stale = accounted.Where(selector => !declared.Contains(selector)).ToArray();
        Assert.True(stale.Length == 0, "these are accounted for and no longer declare a corner: " + string.Join(", ", stale));
    }

    /// <summary>
    /// The corners written in the views themselves, rather than by a class, do not grow.
    /// </summary>
    /// <remarks>
    /// <b>This is where this gate stops seeing, written as a number instead of as a caveat.</b>
    /// Everything above is about classes in the token file; a view that writes
    /// <c>CornerRadius="{DynamicResource CornerRadiusMedium}"</c> straight into its markup is
    /// invisible to all of it, and on 2026-09-03 there were <b>86</b> such sites across thirty
    /// views — 56 medium, 30 small, and two that spend the pill.
    /// <para>
    /// Pairing them is not a class-shaped job: each one belongs to a particular element of a
    /// particular screen, so it is done view by view along with everything else that view owes the
    /// prototype. What this holds meanwhile is the direction. The number can fall — every site
    /// paired is a site that moves into a class or takes the design's own literal — and it must not
    /// rise, because a new view spending a token by reflex is exactly how the other 86 got here.
    /// </para>
    /// <para>
    /// A ratchet and not an assertion of 86: passing at 80 while claiming 86 would be a gate lying
    /// about its own progress, so it fails in both directions and says which.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_corners_written_in_the_views_themselves_do_not_grow()
    {
        const int ratchet = 86;

        var sites = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.axaml", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith("DesignTokens.axaml", StringComparison.Ordinal))
            .Sum(file => Regex.Count(
                File.ReadAllText(file),
                "CornerRadius=\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)));

        Assert.True(
            sites <= ratchet,
            $"{sites} views write a corner of their own, and the ratchet is {ratchet}. A new view "
            + "spending a token by reflex is how the other 86 got here: pair it with the element the "
            + "prototype draws, the way the table above does.");

        Assert.True(
            sites == ratchet,
            $"only {sites} sites write their own corner, which is fewer than the ratchet of {ratchet}. "
            + "That is progress and the ratchet has to come down with it, in the same change.");
    }

    /// <summary>Every unpaired surface says why, and it is a sentence rather than a shrug.</summary>
    [Fact]
    public void Every_unpaired_surface_says_why()
    {
        foreach (var entry in Unpaired)
        {
            Assert.True(entry.Reason.Length >= 60, $"{entry.Selector} is unpaired with nothing said about why.");
        }
    }

    /// <summary>The corner a selector draws, measured on the control it names.</summary>
    /// <remarks>
    /// The control is built from the selector rather than from a parallel list, so a row cannot name
    /// one type and measure another — which <c>ButtonShapeTests</c> did until 2026-09-02, reporting
    /// a Button's pill under a RadioButton's name.
    /// </remarks>
    private static CornerRadius Corner(string selector)
    {
        // «ListBox.side-list ListBoxItem» is the one selector that names a control inside another,
        // and it is measured the way it is drawn: an item inside its list, because the setter is
        // written as a descendant and a bare ListBoxItem would never match it.
        if (selector == "ListBox.side-list ListBoxItem")
        {
            var list = new ListBox { ItemsSource = new[] { "x" } };
            list.Classes.Add("side-list");
            var host = new Window { Width = 400, Height = 200, Content = list };
            host.Show();
            Dispatcher.UIThread.RunJobs();
            var container = list.ContainerFromIndex(0) as ListBoxItem;
            Assert.True(container is not null, "the side list built no item, so there is no corner to read.");
            var itemRadius = container!.CornerRadius;
            host.Close();
            return itemRadius;
        }

        var parts = selector.Split('.');
        Control control = parts[0] switch
        {
            "Border" => new Border(),
            "TextBox" => new TextBox(),
            "ComboBox" => new ComboBox(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(selector),
                selector,
                "the table names a control this measurement cannot build, so it would be measured as "
                    + "something else."),
        };

        if (parts.Length > 1)
        {
            control.Classes.Add(parts[1]);
        }

        var window = new Window { Width = 400, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var radius = control switch
        {
            Border border => border.CornerRadius,
            TemplatedControl templated => templated.CornerRadius,
            _ => throw new InvalidOperationException($"{selector} has no corner to read."),
        };
        window.Close();
        return radius;
    }

    /// <summary>Every non-button selector in the token file that writes a CornerRadius setter.</summary>
    private static HashSet<string> CornerDeclaringSelectors()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml"));

        return Regex.Matches(
            markup,
            "<Style Selector=\"(?<selector>[^\"]+)\">(?<body>(?:(?!</Style>).)*?Property=\"CornerRadius\"(?:(?!</Style>).)*?)</Style>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5))
            .Cast<Match>()
            .Select(match => match.Groups["selector"].Value)
            .Where(selector => !selector.Contains("Button", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record Surface(string Selector, string Element, int Radius, string Pattern);

    /// <summary>
    /// Restores the resources the appearance service writes, so a test does not decide what the next
    /// one measures.
    /// </summary>
    /// <remarks>
    /// Every suite in this assembly shares one application. Key by key rather than by swapping the
    /// dictionary, which is ButtonShapeTests' arrangement and for the reason measured there: the
    /// accent's tokens live in theme dictionaries, and a scope that captured the top level captured
    /// none of them.
    /// </remarks>
    private sealed class ResourceScope : IDisposable
    {
        private static readonly string[] Keys =
        [
            .. AppearanceService.AccentResources,
            "AccentTintOpacity",
            "DensityGutter",
            "PosterCardPadding",
            "PosterCardWidth",
            "PosterCardHeight",
            "PosterCornerRadius",
            "CoverTitlesVisible",
        ];

        private readonly Avalonia.Application _application;
        private readonly Dictionary<string, object?> _before = [];

        public ResourceScope(Avalonia.Application application)
        {
            _application = application;
            foreach (var key in Keys)
            {
                if (application.Resources.TryGetValue(key, out var value))
                {
                    _before[key] = value;
                }
            }
        }

        public void Dispose()
        {
            foreach (var key in Keys)
            {
                _ = _application.Resources.Remove(key);
                if (_before.TryGetValue(key, out var value))
                {
                    _application.Resources[key] = value;
                }
            }
        }
    }

    private sealed class EmptyStore : ISettingsStore
    {
        public T? Read<T>(string key) => default;

        public void Write<T>(string key, T value)
        {
        }
    }

    private sealed class FixedTheme : IThemeService
    {
        public ThemePreference CurrentPreference => ThemePreference.System;

        public Avalonia.Styling.ThemeVariant PlayerThemeVariant => Avalonia.Styling.ThemeVariant.Dark;

        public bool AnimationsEnabled => true;

        public TimeSpan MotionDuration => TimeSpan.FromMilliseconds(160);

        public void Apply(ThemePreference preference)
        {
        }

        public bool TryApplyBackdrop(Window window) => false;
    }

    private sealed class NoBackdrop : IBackdropService
    {
        public bool TryApply(Window window) => false;
    }
}
