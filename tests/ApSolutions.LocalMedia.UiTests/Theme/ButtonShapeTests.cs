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
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Every button draws the corner the prototype draws, measured on the control rather than read off
/// the token file.
/// </summary>
/// <remarks>
/// <b>This file asserted the opposite until 2026-09-01</b>, and the story is kept because it is what
/// the rule now guards against. «Todos los botones o son redondos o son píldoras, pero nunca
/// cuadrados», the owner said on 2026-08-25, and two classes were changed to obey it: the player's
/// chrome and one actually called <c>player-pill</c>. Asked about a third — the lesson row, which
/// the design draws at 7 — the owner withdrew the rule outright: <i>«esa afirmación mía era
/// equivocada, los botones deben ser al igual que todos los elementos de la app, idénticos al 100 %
/// al prototipo»</i>. That decision is ADR-0007.
/// <para>
/// <b>And the first version of this gate was green over corners nobody drew.</b> It read the
/// <c>CornerRadius</c> written in <c>DesignTokens.axaml</c> and translated <c>CornerRadiusMedium</c>
/// to 8 and <c>CornerRadiusSmall</c> to 4 — the numbers that file declares. Measured on 2026-09-01,
/// the running application drew <b>10 and 5</b>: AppearanceService wrote the Rounding preference
/// over both keys before the first surface was built, so what the token file said was only what the
/// first frame would have drawn had the service never run. The gate certified <c>pbtn</c>'s 8 and
/// <c>pbtnLessons</c>' 4 while the screen showed 10 and 5 — this repository's own defect, a check
/// that passes by measuring the wrong thing, wearing the hat of the check written to prevent it.
/// </para>
/// <para>
/// So the first half now builds the control, lets the appearance service run over it exactly as
/// startup does, and reads the corner off the control. The token file is no longer consulted for a
/// number; it is consulted only for whether a class writes a corner at all.
/// </para>
/// </remarks>
public sealed class ButtonShapeTests
{
    /// <summary>
    /// Each button class, the prototype control it draws, and how that control is found in the
    /// design.
    /// </summary>
    /// <remarks>
    /// Paired by the number the design writes rather than by the token, because the scale is 4, 8
    /// and the pill while the design draws twelve distinct radii — 5, 7, 10 and 12 among them, none
    /// of which a token carries and each of which rounding to the nearest token would quietly turn
    /// into a shape the design does not draw.
    /// <para>
    /// The pattern travels with the pairing instead of being derived from the control's name,
    /// because they are written every which way: <c>pbtn</c> is an object literal, <c>btnPri</c> is
    /// an <c>Object.assign</c> over a base, the lesson row and the «Otras acciones» row have no name
    /// at all — the panel builds its rows inline — and the library tile is CSS in an attribute
    /// rather than JavaScript. A single clever pattern over all of them is a pattern that matches
    /// the wrong thing the day one of them moves.
    /// </para>
    /// <para>
    /// <c>Declared</c> says whether the class writes its own <c>CornerRadius</c>. Three of them do
    /// not and are right not to: their prototype control does not redeclare it either — it takes the
    /// base button's — so a setter here would be a second copy of one number, which is how two of
    /// three end up agreeing and the third does not.
    /// </para>
    /// </remarks>
    private static readonly Pairing[] Pairings =
    [
        new("Button, ToggleButton", "Button", "", "btnPri", 999, true,
            @"const btnPri = Object\.assign\([^;]*?borderRadius: (?<radius>[0-9]+)"),
        new("Button.player-chrome", "Button", "player-chrome", "pbtn", 8, true,
            @"\bpbtn: \{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        new("Button.player-pill", "Button", "player-pill", "pbtnLessons", 4, true,
            @"\bpbtnLessons: \{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        new("Button.lesson-row", "Button", "lesson-row", "the lesson row", 7, true,
            @"minHeight: 34, padding: '6px 10px', borderRadius: (?<radius>[0-9]+)"),
        new("Button.navigation-destination", "Button", "navigation-destination",
            "the rail's destinations", 12, true,
            @"width: 46, height: 42, borderRadius: (?<radius>[0-9]+), border: '1px solid ' \+ \(open"),
        new("Button.navigation-action", "Button", "navigation-action", "railAdd", 12, true,
            @"\brailAdd: \{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        new("Button.poster-card", "Button", "poster-card", "the library tile", 12, true,
            @"border:1px solid transparent;border-radius:(?<radius>[0-9]+)px;padding:8px;margin:-8px"),
        new("Button.action-row", "Button", "action-row", "the row of other actions", 5, true,
            @"min-height:36px;padding:0 12px;border-radius:(?<radius>[0-9]+)px"),
        new("ToggleButton.action-row", "ToggleButton", "action-row", "the row of other actions", 5, true,
            @"min-height:36px;padding:0 12px;border-radius:(?<radius>[0-9]+)px"),
        new("ToggleButton.segment", "ToggleButton", "segment", "seg", 999, true,
            @"const seg = \(sel\) => \(\{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        new("Button.accent-swatch", "Button", "accent-swatch", "the accent swatches", 999, true,
            @"width: 28, height: 28, borderRadius: (?<radius>[0-9]+)"),
        new("Button.compact", "Button", "compact", "btnSecSm", 999, false,
            @"const btnSec = Object\.assign\([^;]*?borderRadius: (?<radius>[0-9]+)"),
        new("Button.primary-action", "Button", "primary-action", "btnPri", 999, false,
            @"const btnPri = Object\.assign\([^;]*?borderRadius: (?<radius>[0-9]+)"),
        new("Button.theme-option", "Button", "theme-option", "seg", 999, false,
            @"const seg = \(sel\) => \(\{[^}]*?borderRadius: (?<radius>[0-9]+)"),
    ];

    /// <summary>
    /// The button classes the prototype has no control for, and why each one exists anyway.
    /// </summary>
    /// <remarks>
    /// A closed list, and that is the point of it: without one, a class nobody paired is
    /// indistinguishable from a class nobody had got round to pairing — which is the state ADR-0007
    /// found ten classes in. Each entry says what was searched for and did not exist, so the next
    /// reader does not repeat the search.
    /// <para>
    /// Being here is not permission to draw anything. It says the shape was decided by this tree
    /// because the design does not answer, which is a different claim from «the design says so» and
    /// has to be written as a different one. The corner is still asserted — measured the same way as
    /// a paired one — because a class the design cannot vouch for is the one most likely to drift.
    /// </para>
    /// </remarks>
    private static readonly (string Selector, string Kind, string Class, int Radius, string Reason)[] Unpaired =
    [
        ("Button.colour-cell", "Button", "colour-cell", 4,
            "the prototype opens the operating system's own colour control for a custom accent and "
            + "never draws a grid of swatches, so there is no cell to match. 4 is what this tree drew "
            + "before the withdrawn rule made it a pill and left its own comment saying «square»."),
        ("Button.rating-choice", "Button", "rating-choice", 999,
            "a personal rating is in none of the four design documents; searched for stars, "
            + "«valoración» and «rating» across all of them on 2026-09-01 and found nothing."),
        ("Button.icon-action", "Button", "icon-action", 999,
            "«en la tarjeta ancha del inicio justo después habría que poner el icono de reproducir "
            + "desde el inicio», asked on 2026-08-25 — after the prototype was drawn, so it draws no "
            + "such button."),
        ("Button.link-action", "Button", "link-action", 999,
            "btnLink is the prototype's counterpart and gives no radius at all: no background and no "
            + "border, so there is no corner to draw. What this class inherits is never painted."),
        ("RadioButton.option", "RadioButton", "option", 3,
            "the prototype does draw this row — audioList, devList and subList share one style "
            + "object with borderRadius 4 — but the surface that draws it here is the Border the "
            + "row lives in, measured 2026-09-02: the base theme's RadioButton template builds "
            + "three Ellipses and a ContentPresenter and no Border at all, so a corner set on this "
            + "class is a number nothing reads. The 3 is what the base theme hands a RadioButton "
            + "and it is never painted — and it is also what made the measurement itself wrong "
            + "until that day: Corner built a Button for every kind that was not ToggleButton, so "
            + "this class first measured 999, a Button's corner reported as a RadioButton's. "
            + "Border.option-row carries the 4, and OptionRowShapeTests measures it."),
    ];

    /// <summary>
    /// The corner every class draws, measured on a control with the appearance service running.
    /// </summary>
    /// <remarks>
    /// The service is built rather than skipped because building it is what startup does — the
    /// composition root resolves it before any surface — and because skipping it is precisely how
    /// this gate came to certify two numbers nobody could see. A preference that reaches one
    /// resource too many is invisible in the markup and obvious here.
    /// </remarks>
    [AvaloniaFact]
    public void Every_button_draws_the_corner_the_prototype_draws()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        var measured = Pairings
            .Select(pairing => (pairing.Selector, pairing.Kind, pairing.Class, pairing.Radius, pairing.Control))
            .Concat(Unpaired.Select(entry =>
                (entry.Selector, entry.Kind, entry.Class, entry.Radius, "the decision written beside it")));

        // The kind a row names is the type its selector names, and that is asserted rather than
        // assumed: Corner builds what Kind says, so an entry reading ("RadioButton.option",
        // "Button", …) would measure a Button's corner and report it under the radio's name — the
        // very false green the RadioButton entry below was written to record.
        foreach (var (selector, kind, _, _, _) in measured)
        {
            Assert.Equal(selector.Split(',')[0].Split('.')[0], kind);
        }

        var offenders = new List<string>();
        foreach (var (selector, kind, styleClass, radius, control) in measured)
        {
            var drawn = Corner(kind, styleClass);

            // Four corners and not one, because a class that rounded three of them would satisfy any
            // comparison written against the first.
            if (drawn.TopLeft != drawn.TopRight
                || drawn.TopLeft != drawn.BottomLeft
                || drawn.TopLeft != drawn.BottomRight)
            {
                offenders.Add($"{selector} draws four different corners: {drawn}");
                continue;
            }

            if (drawn.TopLeft != radius)
            {
                offenders.Add($"{selector} draws {drawn.TopLeft}, and {control} draws {radius}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A button draws the corner its prototype control draws: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// The radii the table above claims are the ones the design actually writes.
    /// </summary>
    /// <remarks>
    /// Without this half the table would be a second set of numbers copied by hand, which is exactly
    /// how the withdrawn rule survived a week: it read like a decision and nobody re-read the design
    /// behind it. Here the design is the source, so a pairing that drifts from it fails on the number
    /// rather than certifying itself.
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
                $"the design no longer draws {pairing.Control}, so {pairing.Selector} is paired with nothing.");
            Assert.Equal(
                pairing.Radius,
                int.Parse(match.Groups["radius"].Value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// A class the table says writes its own corner writes one, and a class it says does not, does
    /// not.
    /// </summary>
    /// <remarks>
    /// The measured half above passes either way — a class that inherits the pill and a class that
    /// sets the pill both draw 999 — so on its own it cannot tell a decision from a coincidence. The
    /// three classes marked as inheriting draw the right number today because their prototype
    /// control inherits it too; if one of them started writing its own, the coincidence would hold
    /// until the base moved, and then break somewhere else entirely.
    /// </remarks>
    [Fact]
    public void A_class_writes_its_own_corner_only_where_the_table_says_it_does()
    {
        var blocks = Blocks();
        var offenders = new List<string>();

        foreach (var pairing in Pairings)
        {
            var bodies = blocks.TryGetValue(pairing.Selector, out var found) ? found : [];
            if (bodies.Count == 0)
            {
                offenders.Add($"{pairing.Selector} is paired with {pairing.Control} and no longer exists");
                continue;
            }

            // A selector can be declared more than once — player-chrome is written twice, once for
            // its padding beside the swatch and once for its shape — so the corner is looked for
            // across every block that names it rather than in whichever one comes first. Taking the
            // first was this test's own first red, and it accused the tree of drawing nothing.
            var corners = bodies
                .Select(body => Regex.Match(
                    body,
                    "Property=\"CornerRadius\" Value=\"(?<value>[^\"]+)\"",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(5)))
                .Where(match => match.Success)
                .Select(match => match.Groups["value"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            // Two blocks naming two different corners is a class arguing with itself, and whichever
            // one the renderer picks is not something a reader of either block can predict.
            if (corners.Length > 1)
            {
                offenders.Add($"{pairing.Selector} names more than one corner");
            }
            else if (pairing.Declared && corners.Length == 0)
            {
                offenders.Add($"{pairing.Selector} names no corner, and {pairing.Control} draws {pairing.Radius}");
            }
            else if (!pairing.Declared && corners.Length == 1)
            {
                offenders.Add(
                    $"{pairing.Selector} now writes {corners[0]}, and it is paired with {pairing.Control} "
                    + "on the ground that neither of them declares one");
            }
        }

        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }

    /// <summary>
    /// No button class is left out of both tables.
    /// </summary>
    /// <remarks>
    /// This is the half ADR-0007 was missing, and its absence is why the batch that wrote it left ten
    /// classes unpaired without anything going red. A gate over a hand-written list measures only
    /// what somebody remembered to list, and the classes nobody remembers are exactly the ones that
    /// drift.
    /// </remarks>
    [Fact]
    public void Every_button_class_in_the_token_file_is_accounted_for()
    {
        var declared = Blocks().Keys
            .Select(Family)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Anti-blindness floor: a normalisation that produced nothing would pass by measuring nothing.
        Assert.True(
            declared.Length >= 15,
            $"only {declared.Length} button classes were read; this reads the wrong file.");

        var accounted = Pairings
            .Select(pairing => Family(pairing.Selector)!)
            .Concat(Unpaired.Select(entry => Family(entry.Selector)!))
            .ToHashSet(StringComparer.Ordinal);

        var missing = declared.Where(selector => !accounted.Contains(selector)).ToArray();
        Assert.True(
            missing.Length == 0,
            "Every button class is paired with a prototype control, or written into the unpaired list "
            + "with its reason: " + string.Join(", ", missing));

        // And the other way round, so a pairing for a class that was deleted fails here rather than
        // sitting in the table describing nothing.
        var stale = accounted.Where(selector => !declared.Contains(selector, StringComparer.Ordinal)).ToArray();
        Assert.True(stale.Length == 0, "these are paired and no longer exist: " + string.Join(", ", stale));
    }

    /// <summary>
    /// Every unpaired class carries a reason, and it is a sentence rather than a shrug.
    /// </summary>
    [Fact]
    public void Every_unpaired_class_says_why_the_design_does_not_answer()
    {
        foreach (var entry in Unpaired)
        {
            Assert.True(
                entry.Reason.Length >= 60,
                $"{entry.Selector} is unpaired with nothing said about why.");
        }
    }

    /// <summary>
    /// The corner is measured on the screen and not read off the property.
    /// </summary>
    /// <remarks>
    /// A radius larger than half the side is clamped when it is drawn, so the number alone says
    /// nothing about the shape: 999 would satisfy any comparison while the renderer decided what it
    /// actually painted. What is asserted is that the button's own fill is absent from its corner
    /// and present at its centre, which is what «round» means to somebody looking at it.
    /// </remarks>
    [AvaloniaTheory]
    // A target as wide as it is tall: the pill radius makes it a circle.
    [InlineData(44d, 44d)]
    [InlineData(28d, 28d)]
    // And one carrying a word: the same token makes it a pill.
    [InlineData(160d, 36d)]
    public void The_same_token_draws_a_circle_and_a_pill_from_the_shape_of_the_target(
        double width,
        double height)
    {
        var button = new Button
        {
            Content = string.Empty,
            Width = width,
            Height = height,
            Background = Avalonia.Media.Brushes.Red,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = new Window
        {
            Width = width + 40,
            Height = height + 40,
            Background = Avalonia.Media.Brushes.White,
            Padding = new Thickness(0),
            Content = button,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The corner against the centre rather than against a colour written down here: what the
        // channels are called in the captured buffer is the renderer's business, and this is about
        // whether the button's own fill reaches its corner.
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var centre = Pixel(frame!, (int)(width / 2), (int)(height / 2));
        var corner = Pixel(frame!, 1, 1);
        Assert.NotEqual(centre, corner);
        window.Close();
    }

    private static CornerRadius Corner(string kind, string styleClass)
    {
        // The type the table names, and not a Button standing in for all of them. Until 2026-09-02
        // a ToggleButton was the only alternative and anything else silently became a Button — which
        // would have measured a Button's corner and reported it as a RadioButton's, the shape of
        // false green this whole class exists to catch.
        ContentControl control = kind switch
        {
            "ToggleButton" => new ToggleButton(),
            "RadioButton" => new RadioButton(),
            "Button" => new Button(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "the table names a control this measurement cannot build, so it would be measured as "
                    + "something else."),
        };
        control.Content = "x";
        if (styleClass.Length > 0)
        {
            control.Classes.Add(styleClass);
        }

        var window = new Window { Width = 400, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var radius = control.CornerRadius;
        window.Close();
        return radius;
    }

    /// <summary>Every button style in the token file, by selector, with its bodies.</summary>
    private static Dictionary<string, List<string>> Blocks()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml"));
        var styles = Regex.Matches(
            markup,
            "<Style Selector=\"(?<selector>[^\"]*Button[^\"]*)\">(?<body>.*?)</Style>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        var blocks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Match style in styles)
        {
            var selector = style.Groups["selector"].Value;
            if (!blocks.TryGetValue(selector, out var bodies))
            {
                bodies = [];
                blocks[selector] = bodies;
            }

            bodies.Add(style.Groups["body"].Value);
        }

        return blocks;
    }

    /// <summary>
    /// The class a selector belongs to, or null where it is not one.
    /// </summary>
    /// <remarks>
    /// State modifiers fold into the class they modify — <c>Button.accent-swatch.selected</c> is the
    /// swatch, not a fifteenth class — and so do pseudo-classes and template selectors, which style
    /// a part of a control that already has a shape. A bare type selector is the base button, where
    /// the pill is declared; the long one listing nine control types is the focus ring, and it
    /// reaches the same bucket.
    /// </remarks>
    private static string? Family(string selector)
    {
        if (selector.Contains("/template/", StringComparison.Ordinal))
        {
            return null;
        }

        var first = selector.Split(',')[0].Trim();
        var colon = first.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            first = first[..colon];
        }

        // A descendant selector styles somebody else's child, and the child's shape is that
        // control's business rather than this button class's.
        if (first.Contains(' ', StringComparison.Ordinal))
        {
            return null;
        }

        var parts = first.Split('.');
        return parts.Length == 1 ? "Button, ToggleButton" : $"{parts[0]}.{parts[1]}";
    }

    private static (byte First, byte Second, byte Third) Pixel(
        Avalonia.Media.Imaging.WriteableBitmap frame,
        int x,
        int y)
    {
        using var buffer = frame.Lock();
        var column = Math.Clamp(x, 0, buffer.Size.Width - 1);
        var row = Math.Clamp(y, 0, buffer.Size.Height - 1);
        var pixel = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(
            buffer.Address + (row * buffer.RowBytes) + (column * 4),
            pixel,
            0,
            4);
        return (pixel[0], pixel[1], pixel[2]);
    }

    private readonly record struct Pairing(
        string Selector,
        string Kind,
        string Class,
        string Control,
        int Radius,
        bool Declared,
        string Pattern);

    /// <summary>
    /// Puts back what the appearance service writes over, so a run leaves nothing behind.
    /// </summary>
    /// <remarks>
    /// Every suite in this assembly shares one application, so a test that let an accent or a cover
    /// size stand would decide what whatever ran next measured.
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

        public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

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
