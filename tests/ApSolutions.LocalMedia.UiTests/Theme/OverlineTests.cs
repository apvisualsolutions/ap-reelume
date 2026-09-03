// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Every overline — small, spaced, capitalised text — draws the size, weight and tracking the
/// prototype draws for the control it stands in for.
/// </summary>
/// <remarks>
/// This is ADR-0007 applied past the buttons: «todos los elementos». The gate has the same two
/// halves <c>ButtonShapeTests</c> has, and for the same reason — the tree draws what the table says,
/// and the table says what the design draws — because a table of hand-copied numbers is how the
/// withdrawn shape rule survived a week.
/// <para>
/// <b>What measuring first said, on 2026-09-03.</b> The handover called this «the small caps: the
/// prototype uses it in 35 places and the tree in two». Both halves were short. The tree draws it in
/// <b>16</b> places across <b>three</b> classes — <c>hero-overline</c>, <c>section-overline</c> and
/// <c>card-eyebrow</c>, the last of which carries no tracking at all — and the design's 35 places
/// are not one overline but <b>nine</b> distinct combinations of size, weight and tracking. Putting
/// one class on 35 sites would have invented a uniformity the design does not have, which is the
/// same defect ADR-0007 was written about, pointing the other way.
/// </para>
/// <para>
/// <b>Tracking is asserted in pixels because that is the unit Avalonia takes.</b> The design writes
/// <c>em</c>; <c>TextElement.LetterSpacing</c> is documented in Avalonia 12.1.1 as «specified in
/// pixels», so the table carries the design's em and multiplies by the size on the same row. A
/// tracking that stayed behind when its size moved is the drift this arrangement exists to stop.
/// </para>
/// <para>
/// <b>Colour and opacity are deliberately not asserted.</b> The same overline is drawn in the
/// secondary ink over a page, in the accent over a card and in the warning or error ink over a
/// notice, and over the video it is dimmed rather than recoloured. Those are the context, and a
/// table that fixed them would need one row per place rather than one per shape.
/// </para>
/// </remarks>
public sealed class OverlineTests
{
    /// <summary>
    /// Each overline class, the prototype control it draws, and how that control is found in the
    /// design.
    /// </summary>
    /// <remarks>
    /// The pattern travels with the pairing rather than being derived from the class name, for the
    /// reason <c>ButtonShapeTests</c> found: the design writes these inline, every which way, and
    /// the order of the declarations inside one <c>style</c> attribute is not consistent between
    /// them — <c>letter-spacing</c> comes before <c>text-transform</c> in some and after it in
    /// others. One clever pattern over all of them is a pattern that matches the wrong thing the day
    /// one of them moves.
    /// <para>
    /// Anchored to a named binding wherever the design has one, because the shape alone is not
    /// unique: four sites share 10.5/400/.06em and picking «the first one that looks right» is how a
    /// pairing ends up describing a control nobody meant.
    /// </para>
    /// </remarks>
    private static readonly Overline[] Pairings =
    [
        new(
            "TextBlock.section-overline",
            "section-overline",
            "the label over a field — «Ubicación del archivo»",
            11,
            FontWeight.Normal,
            0.06,
            @"font-size:(?<size>[0-9.]+)px;text-transform:uppercase;letter-spacing:(?<track>\.[0-9]+)em;color:var\(--text2,\#5B6675\)"">\{\{ t\.fileLoc"),
        new(
            "TextBlock.hero-overline",
            "hero-overline",
            "the kicker over the hero — «Continuar viendo»",
            10.5,
            FontWeight.Normal,
            0.18,
            @"font-size:(?<size>[0-9.]+)px;letter-spacing:(?<track>\.[0-9]+)em;text-transform:uppercase;color:\#9FB2C6"">\{\{ hero\.kicker"),
        new(
            "TextBlock.group-overline",
            "group-overline",
            "the label over a group of fields — «Carpeta», «Catálogo»",
            11,
            FontWeight.Bold,
            0.10,
            @"font-size:(?<size>[0-9.]+)px;font-weight:(?<weight>[0-9]+);letter-spacing:(?<track>\.[0-9]+)em;text-transform:uppercase;color:var\(--text2,\#8B97A8\)"">\{\{ ob\.pathLabel"),
        new(
            "TextBlock.notice-overline",
            "notice-overline",
            "the coloured label over a notice — «Confirmar el borrado»",
            10,
            FontWeight.Bold,
            0.14,
            @"font-size:(?<size>[0-9.]+)px;font-weight:(?<weight>[0-9]+);letter-spacing:(?<track>\.[0-9]+)em;text-transform:uppercase;color:var\(--err-fg,\#F5A3AC\)"">\{\{ ob\.confirmLabel"),
        new(
            "TextBlock.player-overline",
            "player-overline",
            "the label over the video — «Inicio», «Fin»",
            10.5,
            FontWeight.Normal,
            0.06,
            @"font-size:(?<size>[0-9.]+)px;opacity:\.72;text-transform:uppercase;letter-spacing:(?<track>\.[0-9]+)em"">\{\{ pl\.mk\.startLabel"),
        new(
            "TextBlock.column-overline",
            "column-overline",
            "the heading over a table column — «ARCHIVO», «RESOLUCIÓN»",
            10,
            FontWeight.Normal,
            0.05,
            @"grid-template-columns:1fr 22px 1fr 130px;gap:12px;padding:0 12px;font-size:(?<size>[0-9.]+)px;text-transform:uppercase;letter-spacing:(?<track>\.[0-9]+)em"),
    ];

    /// <summary>
    /// The overlines the design draws that this tree has no class for, and why each one is absent.
    /// </summary>
    /// <remarks>
    /// A closed list, for the reason <c>ButtonShapeTests</c> keeps one: without it, an overline
    /// nobody paired is indistinguishable from one nobody got round to pairing.
    /// <para>
    /// And these are absent for a different reason from that gate's unpaired buttons. There the
    /// design was silent; here the design speaks and <b>the tree has nowhere to say it</b> — the
    /// surface does not exist, or exists as something other than a piece of text. That is a
    /// different claim and it is written as one, because «the design does not draw it» would be
    /// false about every entry below.
    /// </para>
    /// <para>
    /// <b>This list is also where the gate's blind spot is written down.</b> The census below finds
    /// the places the tree draws capitals; a place the design draws in capitals and the tree draws
    /// flat is invisible to it, because there is no shouted string to find. Nothing measures that
    /// direction, so it is kept by hand here, and an entry is the only record that somebody looked.
    /// </para>
    /// </remarks>
    private static readonly (string Control, double Size, string Reason)[] Absent =
    [
        ("the rail destination's menu title", 10,
            "the prototype opens a menu off each rail destination — filters with counts, «Añadir "
            + "medios», «Gestionar raíces» — and its heading is that destination's name. Measured "
            + "2026-09-03: this tree has no such menu on the rail at all, so there is no heading to "
            + "style. It draws at 10/700/.16em and belongs to whatever batch builds that menu."),
        ("the absent-feature mark", 11,
            "«LIB-016» beside a settings row: the prototype marking a setting it has drawn but the "
            + "product has not built, citing the scope row by its identifier. It is the prototype "
            + "talking about itself rather than a part of the application, so there is nothing here "
            + "for it to be — and it draws at 11/700/.12em if that ever changes."),
        ("the course thread's kicker", 10,
            "«DÓNDE LO DEJASTE» over the course thread. This one is absent for a third reason and it "
            + "is the one worth naming: the tree draws the text, in the right place, saying the right "
            + "words — as a 20 px semi-bold subtitle, where the design draws a 10 px overline in the "
            + "accent ink and gives the lesson under it the weight instead. Changing only the "
            + "typography would leave the card half in each design, so it belongs to the batch that "
            + "takes that view against the prototype rather than to this one."),
        ("the speed pill's closed face", 11,
            "«VELOCIDAD» over the transport's speed control. It is drawn here, but not by a "
            + "TextBlock a class can reach: it is the Tag of a ComboBox and the pill's own template "
            + "paints it, which is why it is not in the table above rather than not in the tree. It "
            + "draws at 11/400/.05em dimmed to .72."),
    ];

    /// <summary>
    /// Every class draws the size, weight and tracking its prototype control draws.
    /// </summary>
    /// <remarks>
    /// Measured on the control rather than read off the token file, which is how
    /// <c>ButtonShapeTests</c> once certified two numbers nobody could see: the appearance service
    /// wrote the rounding preference over both corner keys before the first surface was built, so
    /// the token file said only what the first frame would have drawn had the service never run.
    /// <para>
    /// That gate builds the service; this one does not, and the difference is measured rather than
    /// assumed. The service writes the tint opacity, the gutter, the cover's size and corner, the
    /// cover titles and the accent brushes — <b>no font size and no tracking</b>. So building it
    /// here would change nothing, and the test below asserts that instead of trusting it, because
    /// «it does not reach this» is exactly the kind of claim that stops being true without anybody
    /// noticing.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_overline_draws_what_the_prototype_draws()
    {
        var offenders = new List<string>();
        foreach (var pairing in Pairings)
        {
            var (size, weight, tracking) = Drawn(pairing.Class);

            if (Math.Abs(size - pairing.Size) > 0.001)
            {
                offenders.Add(
                    $"{pairing.Selector} draws {size.ToString(CultureInfo.InvariantCulture)}, and "
                    + $"{pairing.Control} draws {pairing.Size.ToString(CultureInfo.InvariantCulture)}");
            }

            if (weight != pairing.Weight)
            {
                offenders.Add($"{pairing.Selector} draws {weight}, and {pairing.Control} draws {pairing.Weight}");
            }

            // The design's em against the size on this same row, so a tracking cannot stay behind
            // when its size moves — which is the one way these two can disagree silently.
            var expected = pairing.TrackingEm * pairing.Size;
            if (Math.Abs(tracking - expected) > 0.001)
            {
                offenders.Add(
                    $"{pairing.Selector} tracks {tracking.ToString(CultureInfo.InvariantCulture)}px, and "
                    + $"{pairing.Control} tracks {pairing.TrackingEm.ToString(CultureInfo.InvariantCulture)}em "
                    + $"of {pairing.Size.ToString(CultureInfo.InvariantCulture)}, which is "
                    + $"{expected.ToString(CultureInfo.InvariantCulture)}px");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "An overline draws what its prototype control draws: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// The numbers the table above claims are the ones the design actually writes.
    /// </summary>
    /// <remarks>
    /// Without this half the table is a second set of hand-copied numbers, and a pairing that drifts
    /// from the design would certify itself.
    /// </remarks>
    [Fact]
    public void The_pairings_name_what_the_design_writes()
    {
        var design = File.ReadAllText(RepositoryLayout.PathFromRoot("design/AP Reelume.dc.html"));

        foreach (var pairing in Pairings)
        {
            var match = Regex.Match(design, pairing.Pattern, RegexOptions.None, TimeSpan.FromSeconds(5));

            Assert.True(
                match.Success,
                $"the design no longer draws {pairing.Control}, so {pairing.Selector} is paired with nothing.");

            Assert.Equal(
                pairing.Size,
                double.Parse(match.Groups["size"].Value, CultureInfo.InvariantCulture));
            Assert.Equal(
                pairing.TrackingEm,
                double.Parse(match.Groups["track"].Value, CultureInfo.InvariantCulture));

            // A design site with no font-weight is the browser's 400, and the table has to say
            // Normal for it. Asserting the absence as well as the presence is what stops a row
            // claiming Bold over a site that never wrote one.
            var weight = match.Groups["weight"].Success
                ? (FontWeight)int.Parse(match.Groups["weight"].Value, CultureInfo.InvariantCulture)
                : FontWeight.Normal;
            Assert.Equal(pairing.Weight, weight);
        }
    }

    /// <summary>
    /// Every overline class the token file declares is in the table, and every row of the table
    /// still exists.
    /// </summary>
    /// <remarks>
    /// The half ADR-0007 was missing when it left ten button classes unpaired without anything going
    /// red: a gate over a hand-written list measures only what somebody remembered to list.
    /// </remarks>
    [Fact]
    public void Every_overline_class_in_the_token_file_is_accounted_for()
    {
        var declared = OverlineClasses();

        // Anti-blindness floor: a reader that found nothing would pass by measuring nothing.
        Assert.True(
            declared.Count >= 6,
            $"only {declared.Count} overline classes were read; this reads the wrong file.");

        var paired = Pairings.Select(pairing => pairing.Class).ToHashSet(StringComparer.Ordinal);

        var missing = declared.Where(name => !paired.Contains(name)).ToArray();
        Assert.True(
            missing.Length == 0,
            "Every overline class is paired with a prototype control: " + string.Join(", ", missing));

        var stale = paired.Where(name => !declared.Contains(name)).ToArray();
        Assert.True(stale.Length == 0, "these are paired and no longer exist: " + string.Join(", ", stale));
    }

    /// <summary>
    /// Every place the tree draws capitals wears one of the paired classes.
    /// </summary>
    /// <remarks>
    /// The table above is about classes; this is about sites, and without it the gate would be green
    /// over every view that draws an overline without asking for one. That is not hypothetical: it
    /// is the state this batch found <c>TrackSelectorView</c> in, drawing flat headings beside
    /// <c>AudioOutputView</c>'s spaced ones <em>in the same panel</em>.
    /// <para>
    /// Capitals are found through the resources rather than the markup because that is where this
    /// tree puts them — a decision <c>AudioOutputView</c> writes down: AXAML has no
    /// <c>text-transform</c> and no way to compose a converter with a resource that follows the
    /// language, so a heading is its own string. So a TextBlock reading a resource that is written
    /// in capitals is a TextBlock drawing an overline, whatever it calls itself.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_place_the_tree_draws_capitals_wears_an_overline_class()
    {
        var shouted = ShoutedResources();

        // Anti-blindness floor, and it is the one this test most needs: the whole check hangs on
        // finding the capitalised strings, and finding none would pass in silence.
        Assert.True(
            shouted.Count >= 21,
            $"only {shouted.Count} capitalised strings were read; this reads the wrong file.");

        var classes = Pairings.Select(pairing => pairing.Class).ToArray();
        var offenders = new List<string>();
        var sites = 0;

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepositoryLayout.Root, "src"), "*.axaml", SearchOption.AllDirectories))
        {
            foreach (var element in Regex.Matches(
                File.ReadAllText(file),
                "<TextBlock(?<body>.*?)/>",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5)).Cast<Match>())
            {
                var body = element.Groups["body"].Value;
                var key = Regex.Match(
                    body,
                    @"Text=""\{DynamicResource (?<key>[A-Za-z0-9]+)\}""",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(5));

                if (!key.Success || !shouted.Contains(key.Groups["key"].Value))
                {
                    continue;
                }

                sites++;
                if (!classes.Any(name => Regex.IsMatch(
                    body,
                    $@"Classes=""[^""]*\b{Regex.Escape(name)}\b",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(5))))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {key.Groups["key"].Value}");
                }
            }
        }

        // Twenty-one shouted strings, and one of them — «VELOCIDAD» — reaches no TextBlock at all:
        // it is a ComboBox's Tag, which is why it is in the absent list above rather than here. One
        // more is read from two views, so the sites outnumber the strings that reach one.
        Assert.True(sites >= 21, $"only {sites} capitalised sites were found; this reads the wrong markup.");
        Assert.True(
            offenders.Count == 0,
            "A view draws capitals without asking for an overline: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Every overline class has at least one view that asks for it.
    /// </summary>
    /// <remarks>
    /// This repository's characteristic defect is the thing registered and never fed — a service
    /// nothing resolves, a view nobody reaches — and a style class nothing wears is the same defect
    /// in the token file. It is worth its own test because the two halves above cannot see it: a
    /// class paired with a prototype control and drawing the right numbers passes both while no view
    /// has ever asked for it.
    /// </remarks>
    [Fact]
    public void Every_overline_class_is_worn_by_a_view()
    {
        var markup = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.axaml", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith("DesignTokens.axaml", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        // Anti-blindness floor: a reader that found no views would pass by measuring nothing.
        Assert.True(markup.Length >= 50, $"only {markup.Length} views were read; this reads the wrong folder.");

        var unworn = Pairings
            .Where(pairing => !markup.Any(view => Regex.IsMatch(
                view,
                $@"Classes=""[^""]*\b{Regex.Escape(pairing.Class)}\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))))
            .Select(pairing => pairing.Selector)
            .ToArray();

        Assert.True(
            unworn.Length == 0,
            "An overline class is declared and no view wears it: " + string.Join(", ", unworn));
    }

    /// <summary>
    /// The appearance service still writes no typography, which is what lets the measurement above
    /// skip building it.
    /// </summary>
    /// <remarks>
    /// A guard over an assumption rather than over the application: the day a text-size preference
    /// arrives, the measurement above starts reading the token file's number instead of the screen's
    /// — the same false green <c>ButtonShapeTests</c> was written about — and it would stay green
    /// while doing it. This fails first and says why.
    /// </remarks>
    [Fact]
    public void The_appearance_service_writes_no_font_size_and_no_tracking()
    {
        var service = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/AppearanceService.cs"));

        var written = Regex.Matches(
            service,
            @"Resources\[""(?<key>[^""]+)""\]\s*=",
            RegexOptions.None,
            TimeSpan.FromSeconds(5))
            .Cast<Match>()
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Anti-blindness floor: a reader that found no writes would pass by measuring nothing, and
        // this whole test is about what the service writes.
        Assert.True(written.Length >= 6, $"only {written.Length} resource writes were read; this reads the wrong file.");

        var typography = written
            .Where(key => key.Contains("FontSize", StringComparison.Ordinal)
                || key.Contains("LetterSpacing", StringComparison.Ordinal)
                || key.Contains("FontWeight", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            typography.Length == 0,
            "the appearance service now writes typography — " + string.Join(", ", typography)
            + " — so the overline measurement has to build it the way ButtonShapeTests does, or it "
            + "is reading the token file's number rather than the screen's.");
    }

    /// <summary>
    /// Every overline the design draws and this tree does not says why, and it is a sentence rather
    /// than a shrug.
    /// </summary>
    [Fact]
    public void Every_absent_overline_says_why_the_tree_has_nowhere_to_draw_it()
    {
        foreach (var entry in Absent)
        {
            Assert.True(
                entry.Reason.Length >= 60,
                $"{entry.Control} is absent with nothing said about why.");
        }
    }

    /// <summary>
    /// The six overlines are six on screen: each one rasterises to a width no other one shares, and
    /// each one is wider than the same words at the same size with no tracking.
    /// </summary>
    /// <remarks>
    /// The measurement above reads three properties off a control, and three properties can differ
    /// while the ink does not: half a pixel of font size and half a pixel of tracking are exactly the
    /// magnitudes a renderer is free to swallow. This repository has two gates that were green over
    /// two pixels of visible misalignment for that reason, so a table of six typographic shapes gets
    /// asked what a person would see.
    /// <para>
    /// <b>Measured on 2026-09-03 over «SEÑALES CONSIDERADAS»</b>: 122, 131, 137, 146, 153 and 154
    /// pixels of ink for the column, player, section, notice, group and hero overlines. So half a
    /// pixel of size <em>is</em> visible — the 10.5 draws 131 where the 10 draws 122 and the 11 draws
    /// 137 — and the two closest of the six still stand a pixel apart.
    /// </para>
    /// <para>
    /// <b>The word is a parameter, and the short one is the one that nearly went wrong.</b> Tracking
    /// accumulates per gap, so «INICIO» compresses the same six into 30 to 39 pixels — still six
    /// distinct widths, but the margins are small enough that the reader's threshold decides the
    /// answer, which is what happened: see <c>Threshold</c>.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("SEÑALES CONSIDERADAS")]
    [InlineData("INICIO")]
    public void The_six_overlines_are_six_on_screen(string word)
    {
        var widths = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pairing in Pairings)
        {
            var inked = Ink(pairing.Class, word);

            // Anti-blindness floor: a class that rendered nothing would report a width of nothing,
            // and nothing is distinct from everything.
            Assert.True(
                inked.Right > inked.Left && inked.Bottom > inked.Top,
                $"{pairing.Selector} rendered no ink at all, so its width says nothing.");

            widths[pairing.Class] = inked.Right - inked.Left + 1;
        }

        var shared = widths
            .GroupBy(entry => entry.Value)
            .Where(group => group.Count() > 1)
            .Select(group => $"{string.Join(" and ", group.Select(entry => entry.Key))} both draw {group.Key}px")
            .ToArray();

        Assert.True(
            shared.Length == 0,
            $"two overlines are one on screen for «{word}»: " + string.Join("; ", shared));

        // And each one's tracking is visible: the same words at the same size with the letters
        // packed draw narrower. Without this the table could carry a tracking of zero and still
        // report six distinct widths, because the six sizes alone would separate them.
        foreach (var pairing in Pairings)
        {
            var packed = Ink(null, word, pairing.Size, pairing.Weight);
            Assert.True(
                widths[pairing.Class] > packed.Right - packed.Left + 1,
                $"{pairing.Selector} draws no wider than the same words with no tracking, so its "
                + $"{pairing.TrackingEm.ToString(CultureInfo.InvariantCulture)}em is not reaching the screen.");
        }
    }

    /// <summary>The bounding box of the ink a word leaves, in pixels of a real rasterisation.</summary>
    /// <remarks>
    /// The scene paints its own black on its own white rather than taking the theme's inks, so the
    /// threshold survives a change of palette — the fifth of the five traps this repository has
    /// written down about rasterising.
    /// </remarks>
    /// <summary>
    /// How dark a pixel has to be to count as ink.
    /// </summary>
    /// <remarks>
    /// <b>200 rather than the 110 the button gate uses, and the difference was measured rather than
    /// guessed.</b> At 110 this reader lost the leading «I» of «INICIO» in four of the six overlines
    /// and most of «column-overline» — it reported 12 pixels of ink where the class draws 30 — and
    /// the smallest tracking then looked like no tracking at all. Ten-pixel type is thin: the stems
    /// of its lightest glyphs never reach a threshold calibrated for a 14-pixel label, so the reader
    /// was measuring which letters happened to be bold enough.
    /// <para>
    /// 200 is not a guess either: the reading is identical at 200, 230 and 245, so anything in that
    /// band finds the same ink and the scene's own white — 255 — stays out of it. That flat stretch
    /// is what makes the number safe rather than lucky.
    /// </para>
    /// </remarks>
    private const int Threshold = 200;

    private static (int Left, int Right, int Top, int Bottom) Ink(
        string? styleClass,
        string word,
        double? size = null,
        FontWeight? weight = null)
    {
        var text = new Avalonia.Controls.TextBlock
        {
            Text = word,
            Foreground = Avalonia.Media.Brushes.Black,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        if (styleClass is not null)
        {
            text.Classes.Add(styleClass);
        }

        if (size is not null)
        {
            text.FontSize = size.Value;
        }

        if (weight is not null)
        {
            text.FontWeight = weight.Value;
        }

        var host = new Avalonia.Controls.Border
        {
            Background = Avalonia.Media.Brushes.White,
            Child = text,
            Padding = new Avalonia.Thickness(10),
        };
        var window = new Window { Width = 400, Height = 60, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("the headless backend returned no frame.");
        using var buffer = frame.Lock();
        var pixels = frame.PixelSize;
        var bytes = new byte[buffer.RowBytes * pixels.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

        int left = int.MaxValue, right = -1, top = int.MaxValue, bottom = -1;
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                var i = (y * buffer.RowBytes) + (x * 4);
                if (bytes[i] >= Threshold || bytes[i + 1] >= Threshold || bytes[i + 2] >= Threshold)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        window.Close();
        return (left, right, top, bottom);
    }

    /// <summary>What a class draws, measured on a control rather than read off the token file.</summary>
    private static (double Size, FontWeight Weight, double Tracking) Drawn(string styleClass)
    {
        var text = new TextBlock { Text = "x" };
        text.Classes.Add(styleClass);

        var window = new Window { Width = 400, Height = 200, Content = text };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var drawn = (text.FontSize, text.FontWeight, text.LetterSpacing);
        window.Close();
        return drawn;
    }

    /// <summary>The overline classes the token file declares, by the tracking that makes them one.</summary>
    /// <remarks>
    /// Read by the presence of <c>LetterSpacing</c> rather than by a name ending in «overline»,
    /// because a class that spaces its letters is an overline whatever it is called — and one that
    /// does not is the shape this batch found <c>card-eyebrow</c> in.
    /// </remarks>
    private static HashSet<string> OverlineClasses()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml"));

        return Regex.Matches(
            markup,
            "<Style Selector=\"TextBlock\\.(?<class>[a-z-]+)\">(?<body>(?:(?!</Style>).)*?LetterSpacing(?:(?!</Style>).)*?)</Style>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5))
            .Cast<Match>()
            .Select(match => match.Groups["class"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The resource keys whose Spanish string is written in capitals.</summary>
    /// <remarks>
    /// Spanish rather than both, because the pair is already asserted to agree elsewhere and a key
    /// that shouted in one language only would be a different defect from this one.
    /// <para>
    /// An acronym is not an overline, and <b>nothing about the shape of the string separates the
    /// two</b> — which took two measured attempts to accept. The space does not: «CANALES» and
    /// «ARCHIVO» are single words, and that cut found 5 of 16. Length does not: it drops «FIN» and
    /// «END», which are headings this batch added, while a four-letter rule keeps nothing it should.
    /// Digits do not: they drop «HDR10» and keep «USB», «HDR» and «SDR».
    /// </para>
    /// <para>
    /// So the acronyms are a closed list, the way this repository writes every other list it cannot
    /// derive. A new one has to be declared rather than slipping through on a shape, which is the
    /// direction that fails safe: an undeclared acronym is asked for a class it does not need — loud
    /// and easy to fix — while a heading that slipped through would be a site nobody checks.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> Acronyms = new(StringComparer.Ordinal)
    {
        // Names of technologies rather than headings: they are written in capitals because that is
        // how they are spelt, not because anything is styling them.
        "USB",
        "HDR",
        "HDR10",
        "SDR",
    };

    private static HashSet<string> ShoutedResources()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Resources/Strings.es.axaml"));

        return Regex.Matches(
            markup,
            "<x:String x:Key=\"(?<key>[^\"]+)\">(?<value>[^<]+)</x:String>",
            RegexOptions.None,
            TimeSpan.FromSeconds(5))
            .Cast<Match>()
            .Where(match =>
            {
                var value = match.Groups["value"].Value;
                return value.Count(char.IsLetter) >= 2
                    && !value.Any(char.IsLower)
                    && !Acronyms.Contains(value);
            })
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record Overline(
        string Selector,
        string Class,
        string Control,
        double Size,
        FontWeight Weight,
        double TrackingEm,
        string Pattern);
}
