// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.UiTests.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The row of an option list draws what the prototype's row draws.
/// </summary>
/// <remarks>
/// <para>
/// Three lists in the player's panel — audio track, output device and subtitle track — and the
/// prototype gives all three the same style object: <c>gap: 9, minHeight: 34, padding: '0 8px',
/// borderRadius: 4</c>, with the chosen row washed in <c>rgba(98,174,232,.16)</c>, a 15 px radio and
/// a 13 px label. Until 2026-09-02 all three were <c>ComboBox</c>es.
/// </para>
/// <para>
/// It is built the way <see cref="ButtonShapeTests"/> is, and for the reason that class had to be
/// rescued for: the numbers are read off <b>the control</b>, with <c>AppearanceService</c> running,
/// rather than off the token file. A preference that overwrites a token in flight is invisible in
/// the markup and obvious here, and that is not hypothetical — it is what left two shapes certified
/// and wrong.
/// </para>
/// <para>
/// The second half of every claim is the design itself. Each row of the table carries the pattern
/// that finds its number in <c>design/AP Reelume.dc.html</c>, so the table cannot quietly become a
/// set of numbers somebody copied once: if the design moves, the test that reads it fails.
/// </para>
/// </remarks>
public sealed class OptionRowShapeTests
{
    private const string DesignDocument = "design/AP Reelume.dc.html";

    /// <summary>
    /// The row's own style object in the prototype, written once per list.
    /// </summary>
    /// <remarks>
    /// Anchored on the list's name and not on the shape alone, which is the correction this pattern
    /// needed on the day it was written: unanchored it matched <b>five</b> places, and the two extra
    /// ones were a different control each — a row of gap 11 / padding 10 / radius 8, and the
    /// scenario list at minHeight 32. A pattern that matches a shape matches every control that
    /// happens to share it, and then the number it reports belongs to whichever one came first.
    /// </remarks>
    private const string RowStylePattern =
        @"\b(?:audioList|subList|devList): .*?gap: (?<gap>[0-9]+), minHeight: (?<height>[0-9]+), "
        + @"padding: '0 (?<pad>[0-9]+)px', borderRadius: (?<radius>[0-9]+)";

    /// <summary>
    /// Every number this shape is made of, and where the design writes it.
    /// </summary>
    /// <remarks>
    /// A closed table: <see cref="Every_number_of_the_row_is_the_number_the_design_writes"/> reads
    /// each pattern out of the design document, so a row nobody re-measured fails rather than
    /// standing as a claim about a document it no longer matches.
    /// </remarks>
    private static readonly (string What, double Expected, string Group, string Pattern)[] Design =
    [
        ("the row's minimum height", 34, "height", RowStylePattern),
        ("the row's horizontal padding", 8, "pad", RowStylePattern),
        ("the row's corner", 4, "radius", RowStylePattern),
        ("the gap between the radio and its label", 9, "gap", RowStylePattern),
        ("the radio's diameter", 15, "size", @"width: ?(?<size>[0-9]+)px;height: ?[0-9]+px;accent-color:#62AEE8"),
        ("the label's size", 13, "size", @"<span style=""flex:1;font-size:(?<size>[0-9]+)px"">\{\{ a\.label"),
        ("the capability's size", 11, "size", @"<span style=""font-size:(?<size>[0-9]+)px;opacity:\.6"">\{\{ d\.caps"),
    ];

    /// <summary>Every view whose radios are rows of one of these lists.</summary>
    /// <remarks>
    /// Closed on purpose. Without it a fourth list added next year would be drawn any way at all and
    /// nothing here would notice — which is the state ADR-0007 found ten button classes in.
    /// </remarks>
    private static readonly string[] Views = ["TrackSelectorView", "AudioOutputView"];

    /// <summary>
    /// The radios in this tree that are <b>not</b> rows of an option list, and why.
    /// </summary>
    /// <remarks>
    /// Two, and both were already here: choosing which copy of a duplicated film to keep is a
    /// decision made on a card, not in a list, and the prototype draws it that way — a 15 px radio
    /// inside the candidate's own surface rather than a 34 px row. They are named rather than
    /// excluded by a pattern, so a third one added later has to be a decision somebody makes.
    /// </remarks>
    private static readonly (string View, string Reason)[] NotOptionRows =
    [
        ("DuplicateReviewView",
            "the radio picks which copy of a duplicate to keep, and it sits inside the copy's own "
            + "row of path and quality — the prototype's apr-dup, drawn on the card rather than as "
            + "a list row."),
        ("DuplicatesOverviewView",
            "the same decision one level up, on the group's own card."),
    ];

    [AvaloniaFact]
    public void Every_number_of_the_row_is_the_number_the_design_writes()
    {
        var design = File.ReadAllText(RepositoryLayout.PathFromRoot(DesignDocument));

        // Anti-blindness floor: a document that stopped matching would let every claim below pass by
        // finding nothing. Three lists share the row style, and all three have to be there.
        var rows = Regex.Matches(design, RowStylePattern, RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(
            rows.Count == 3,
            $"the design writes the option row's style {rows.Count} times and it writes it three — "
                + "once for the audio list, once for the devices and once for the subtitles — so "
                + "this is reading the wrong document or the wrong shape.");

        // And all three write the same four numbers. The table below reads whichever one comes
        // first, so this is what stops it describing one list while another quietly differs.
        foreach (var group in new[] { "gap", "height", "pad", "radius" })
        {
            var values = rows.Select(row => row.Groups[group].Value).Distinct(StringComparer.Ordinal).ToArray();
            Assert.True(
                values.Length == 1,
                $"the three lists disagree about {group}: {string.Join(", ", values)}. The table "
                    + "describes one row, so three rows that differ is a decision somebody has to make.");
        }

        var wrong = new List<string>();
        foreach (var (what, expected, group, pattern) in Design)
        {
            var match = Regex.Match(design, pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
            if (!match.Success)
            {
                wrong.Add($"{what}: the design no longer writes /{pattern}/ at all");
                continue;
            }

            var drawn = double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
            if (drawn != expected)
            {
                wrong.Add($"{what}: the table says {expected} and the design writes {drawn}");
            }
        }

        Assert.True(
            wrong.Count == 0,
            "The table's numbers are the design's: " + string.Join("; ", wrong));
    }

    /// <summary>
    /// The row on screen carries those numbers, measured with the appearance service running.
    /// </summary>
    [AvaloniaFact]
    public void The_row_draws_the_shape_the_design_writes()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ThemeScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        // Both views of the census and not one of them, which is the correction this gate needed on
        // the day it was written: measured only on the track selector, taking Classes.selected off
        // the output list's row changed nothing here. A gate that names two views and measures one
        // is green about the half it never looked at.
        var (window, rows, radios) = ShowEveryList();
        try
        {
            // By type and not by count, which is the second correction this line needed: comparing
            // Views.Length with Surfaces().Length compares two literals of this file, so Surfaces()
            // could hand back two track selectors and every claim below would still be green about
            // an AudioOutputView it never built.
            Assert.Equal(Views, Surfaces().Select(surface => surface.GetType().Name).ToArray());

            // Both floors, because the loops below iterate: an empty list of radios would walk past
            // every ellipse and every font size without a word.
            Assert.NotEmpty(rows);
            Assert.Equal(rows.Length, radios.Length);
            var expected = Design.ToDictionary(entry => entry.What, entry => entry.Expected);

            foreach (var row in rows)
            {
                Assert.Equal(expected["the row's minimum height"], row.MinHeight);
                Assert.Equal(expected["the row's horizontal padding"], row.Padding.Left);
                Assert.Equal(expected["the row's horizontal padding"], row.Padding.Right);

                // Four corners and not one: a row that rounded three of them would satisfy any
                // comparison written against the first.
                Assert.Equal(
                    new Avalonia.CornerRadius(expected["the row's corner"]),
                    row.CornerRadius);
            }

            foreach (var radio in radios)
            {
                Assert.Equal(expected["the label's size"], radio.FontSize);

                // The two ellipses, because the theme draws the ring with one and the checked fill
                // with the other — sizing one of them leaves a 20 px circle under a 15 px one.
                foreach (var name in new[] { "OuterEllipse", "CheckOuterEllipse" })
                {
                    var sized = Assert.Single(
                        radio.GetVisualDescendants().OfType<Ellipse>(),
                        candidate => candidate.Name == name);
                    Assert.Equal(expected["the radio's diameter"], sized.Bounds.Width);
                    Assert.Equal(expected["the radio's diameter"], sized.Bounds.Height);
                }

                var ellipse = radio.GetVisualDescendants()
                    .OfType<Ellipse>()
                    .Single(candidate => candidate.Name == "OuterEllipse");

                // The gap the design writes, measured between the circle and the content rather
                // than read off a setter — because no setter says it. The template keeps a 20 px
                // column for the circle whatever size it is, so the gap is a consequence of three
                // numbers and the first version of this class asserted it in a table and never
                // measured it: the row drew 10.5 where the design writes 9, certified as measured.
                var presenter = radio.GetVisualDescendants()
                    .OfType<ContentPresenter>()
                    .Single(candidate => candidate.Name == "PART_ContentPresenter");
                Assert.Equal(
                    expected["the gap between the radio and its label"],
                    presenter.Bounds.Left - ellipse.Bounds.Right);

                // And the dot sits in the middle of the circle. Pulling the ellipses left to earn
                // that gap left the glyph where the theme had centred it — 2.5 px off, which is a
                // sixth of the circle and the kind of thing only a measurement of the position
                // catches, never one of the size.
                var glyph = radio.GetVisualDescendants()
                    .OfType<Ellipse>()
                    .Single(candidate => candidate.Name == "CheckGlyph");
                Assert.Equal(ellipse.Bounds.Center.X, glyph.Bounds.Center.X);
                Assert.Equal(ellipse.Bounds.Center.Y, glyph.Bounds.Center.Y);

                // The row answers a click across its whole width, which is what the prototype's
                // label does. Without it the control shrinks to its own words — measured at 115 px
                // in a 320 px row — and the rest of the row stops being a target.
                Assert.Equal(HorizontalAlignment.Stretch, radio.HorizontalAlignment);
                Assert.Equal(HorizontalAlignment.Stretch, radio.HorizontalContentAlignment);

                // A name too long for the panel ends in an ellipsis with the whole of it in a
                // tooltip. It comes out of the file and nobody can shorten it, so truncating is only
                // acceptable because the tooltip carries the rest.
                var label = Assert.Single(
                    radio.GetVisualDescendants().OfType<TextBlock>(),
                    candidate => candidate.Classes.Contains("row-label"));
                Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
                Assert.Equal(TextWrapping.NoWrap, label.TextWrapping);
                Assert.NotNull(ToolTip.GetTip(label));
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The wash is the accent's token, and it follows the choice rather than being painted once.
    /// </summary>
    /// <remarks>
    /// The wash is asserted as the accent's own token rather than as a colour, because that is what
    /// it has to be: a literal would ignore the six accents a person can pick and would stay blue in
    /// both high-contrast themes.
    /// </remarks>
    [AvaloniaFact]
    public void The_wash_is_the_accent_token_and_it_follows_the_choice()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ThemeScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        var viewModel = TrackSelector();
        var (window, rows, radios) = Show(
            new TrackSelectorView { DataContext = viewModel, ShowsSubtitles = false });
        try
        {
            var washed = Assert.Single(rows, row => row.Classes.Contains("selected"));

            // Asked in the variant the row is actually drawn in, because the token lives in a theme
            // dictionary: neither Application.FindResource nor the control's own TryFindResource
            // answers it without one — measured 2026-09-02, and the difference between reading the
            // wash and reading nothing at all.
            Assert.True(application.TryGetResource(
                "AccentSubtleBrush",
                washed.ActualThemeVariant,
                out var wash));
            Assert.Equal(wash, washed.Background);
            Assert.All(
                rows.Where(row => !ReferenceEquals(row, washed)),
                row => Assert.NotEqual(washed.Background, row.Background));

            var chosen = Assert.Single(radios, radio => radio.IsChecked == true);
            Assert.Equal(
                viewModel.SelectedAudio!.Display,
                Avalonia.Automation.AutomationProperties.GetHelpText(chosen));

            // And the wash follows the choice rather than being painted once at load. Choosing the
            // other track has to move it, or the row is a picture of a selection instead of one.
            var other = viewModel.AudioTracks.Single(track => !ReferenceEquals(track, viewModel.SelectedAudio));
            viewModel.ChooseAudioCommand.Execute(other);
            Dispatcher.UIThread.RunJobs();

            var moved = Assert.Single(rows, row => row.Classes.Contains("selected"));
            Assert.NotSame(washed, moved);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Every view that draws one of these lists draws it with this class, and no other.
    /// </summary>
    /// <remarks>
    /// The census is what keeps this from being a gate over one view. It is read out of the markup
    /// because that is where a list is declared, and it fails in both directions: a view that draws
    /// an <c>ItemsControl</c> of radios without the class, and a class nobody spends.
    /// </remarks>
    [Fact]
    public void Every_option_list_in_the_tree_is_drawn_by_this_class()
    {
        var spenders = new List<string>();
        var offenders = new List<string>();
        var root = RepositoryLayout.PathFromRoot("src");

        foreach (var file in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            var view = System.IO.Path.GetFileNameWithoutExtension(file);
            foreach (Match radio in Regex.Matches(
                markup,
                "<RadioButton(?<attrs>[^>]*?)/?>",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5)))
            {
                var attrs = radio.Groups["attrs"].Value;
                if (attrs.Contains("Classes=\"option\"", StringComparison.Ordinal))
                {
                    spenders.Add(view);
                }
                else if (!NotOptionRows.Any(entry => entry.View == view))
                {
                    offenders.Add($"{view} declares a RadioButton that is neither an option row nor "
                        + "named as something else");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A radio in this tree is a row of an option list, or it is written into the list of the "
                + "ones that are not, with its reason: " + string.Join("; ", offenders));

        Assert.Equal(
            Views.OrderBy(view => view, StringComparer.Ordinal),
            spenders.Distinct().OrderBy(view => view, StringComparer.Ordinal));

        // And the other way round, so an exception for a view that stopped declaring a radio fails
        // here rather than sitting in the list describing nothing.
        var declaring = Directory
            .EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("<RadioButton", StringComparison.Ordinal))
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);
        var stale = NotOptionRows.Where(entry => !declaring.Contains(entry.View)).ToArray();
        Assert.True(
            stale.Length == 0,
            "these are named as radios that are not option rows and declare no radio at all: "
                + string.Join(", ", stale.Select(entry => entry.View)));

        // Every reason is a sentence somebody wrote, not a shrug.
        Assert.All(NotOptionRows, entry => Assert.True(entry.Reason.Length > 40));
    }

    private static TrackSelectorViewModel TrackSelector()
    {
        var viewModel = new TrackSelectorViewModel(
            new SelectTrack(new SpeedMenuTests.RecordingEngine(), new NoPreferences()),
            PlaybackPreference.FileKey(Guid.Empty));
        viewModel.Load(
            [
                new MediaTrack("1", MediaTrackKind.Audio, "eng", "English", 2, "aac"),
                new MediaTrack("2", MediaTrackKind.Audio, "spa", "Español 5.1", 6, "eac3"),

                // A subtitle track as well, so the second list has a row to choose that is not the
                // "off" entry it always carries. Without one there is nothing to move.
                new MediaTrack("5", MediaTrackKind.Subtitle, "spa", "Español", null, "subrip"),
            ],
            activeAudio: null,
            activeSubtitle: null);
        viewModel.SelectedAudio = viewModel.AudioTracks[0];
        return viewModel;
    }

    /// <summary>One surface per view of the census, each with a list of its own on screen.</summary>
    private static Control[] Surfaces()
    {
        var output = new AudioOutputViewModel(new TwoEndpoints());
        output.LoadAsync(cancellationToken: TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        return
        [
            new TrackSelectorView { DataContext = TrackSelector(), ShowsSubtitles = false },
            new AudioOutputView { DataContext = output },
        ];
    }

    private static (Window Window, Border[] Rows, RadioButton[] Radios) ShowEveryList()
    {
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var surfaces = new StackPanel();
        foreach (var surface in Surfaces())
        {
            surfaces.Children.Add(surface);
        }

        return Show(surfaces);
    }

    private static (Window Window, Border[] Rows, RadioButton[] Radios) Show(Control content)
    {
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var window = new Window { Width = 420, Height = 900, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = content.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.Classes.Contains("option-row"))
            .ToArray();
        var radios = content.GetVisualDescendants()
            .OfType<RadioButton>()
            .Where(radio => radio.Classes.Contains("option"))
            .ToArray();
        return (window, rows, radios);
    }

    /// <summary>
    /// The output row draws what the endpoint can carry, beside the name and in its own weight.
    /// </summary>
    /// <remarks>
    /// The whole reason <c>AudioOutputOption</c> carries two strings instead of one is that the
    /// design draws them in two weights — and until this was written that claim was asserted on the
    /// model and nowhere else. Measured by the auditor: deleting the second <c>TextBlock</c> and its
    /// style left every suite green with the capability gone from the row, because
    /// <c>Summary</c> is read from the model and from the transport's own literal.
    /// </remarks>
    [AvaloniaFact]
    public void The_output_row_draws_what_the_endpoint_can_carry()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ThemeScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        var output = new AudioOutputViewModel(new TwoEndpoints());
        output.LoadAsync(cancellationToken: TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        var (window, rows, _) = Show(new AudioOutputView { DataContext = output });
        try
        {
            Assert.Equal(output.Devices.Count, rows.Length);
            var expected = Design.ToDictionary(entry => entry.What, entry => entry.Expected);

            var drawn = rows
                .Select(row => Assert.Single(
                    row.GetVisualDescendants().OfType<TextBlock>(),
                    text => text.Classes.Contains("option-capabilities")))
                .ToArray();

            // What each row says is that row's endpoint, in the order the model holds them: a
            // template that bound every row to the same device would draw the right words in the
            // wrong places, and a count would not see it.
            Assert.Equal(
                output.Devices.Select(option => option.Capabilities).ToArray(),
                drawn.Select(text => text.Text).ToArray());

            Assert.All(drawn, text =>
            {
                Assert.Equal(expected["the capability's size"], text.FontSize);
                Assert.Equal(0.6, text.Opacity);
            });

        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A long endpoint name gives way, and the capability stays inside the panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The row's content is a <c>Grid</c> of two columns and not a horizontal <c>StackPanel</c>, and
    /// the difference only shows with a name too long to fit: a StackPanel offers its children
    /// infinite width, so the name never gives way and what sits after it is pushed off the edge.
    /// </para>
    /// <para>
    /// <c>ViewOverflowTests</c> cannot see this one. It builds every view with <b>no data context</b>
    /// — which is what makes all the branches visible at once — and a list with no data has no rows
    /// at all. So the condition has to be built here: the panel at the 320 px the player gives it,
    /// and an endpoint whose name is far longer than that.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_long_endpoint_name_gives_way_instead_of_pushing_the_capability_off_the_panel()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ThemeScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        const double PanelWidth = 320;
        var output = new AudioOutputViewModel(new OneVeryLongName());
        output.LoadAsync(cancellationToken: TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
        var view = new AudioOutputView { DataContext = output };
        var window = new Window { Width = PanelWidth, Height = 400, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var capability = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Classes.Contains("option-capabilities"));

            // Measured the way the overflow gate measures: the control's right edge translated into
            // the window it is drawn in.
            var edge = capability.TranslatePoint(new Point(capability.Bounds.Width, 0), window);
            Assert.NotNull(edge);
            Assert.True(
                edge!.Value.X <= PanelWidth + 0.5,
                $"the capability ends at x={edge.Value.X:F0} in a {PanelWidth:F0} px panel, so the "
                    + "name is not giving way and what follows it is off the edge.");

            // And it is the name that gave way, not the capability that shrank to nothing: the
            // capability is the short string and it keeps all of it.
            Assert.True(capability.Bounds.Width > 0);
            var label = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Classes.Contains("row-label"));
            Assert.True(
                label.Bounds.Width < PanelWidth,
                "the name took the whole panel, so nothing made it give way.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The two lists of one view are two radio groups, so a choice in one does not put out the other.
    /// </summary>
    /// <remarks>
    /// <c>TrackSelectorView</c> declares both, and a shared <c>GroupName</c> would make them one
    /// group: choosing a subtitle would uncheck the audio row while the model still named it. The
    /// two halves are drawn in different panels of the player, so nothing else in this suite ever
    /// builds them together — which is why this test asks for both at once.
    /// </remarks>
    [AvaloniaFact]
    public void The_audio_and_subtitle_lists_are_two_groups_and_not_one()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ThemeScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        var viewModel = TrackSelector();
        var (window, _, radios) = Show(new TrackSelectorView { DataContext = viewModel });
        try
        {
            var groups = radios.Select(radio => radio.GroupName).Distinct(StringComparer.Ordinal).ToArray();
            Assert.Equal(2, groups.Length);

            // One row checked per list, both at once. With one group the second choice would put out
            // the first, and each list on its own would still look right.
            Assert.Equal(2, radios.Count(radio => radio.IsChecked == true));
            Assert.All(
                groups,
                group => Assert.Single(
                    radios.Where(radio => radio.GroupName == group),
                    radio => radio.IsChecked == true));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Every option row in the tree is washed by its own selection, in every view that draws one.
    /// </summary>
    /// <remarks>
    /// Separate from the shape because it is a separate claim: a row can carry the right height and
    /// corner and still be a picture of a selection. Measured across the census, since the mutation
    /// that took <c>Classes.selected</c> off the output list is exactly the one a gate over a single
    /// view sleeps through.
    /// </remarks>
    [AvaloniaFact]
    public void Every_list_lights_exactly_one_row_and_it_is_the_chosen_one()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ThemeScope(application);
        _ = new AppearanceService(application, new EmptyStore(), new FixedTheme(), new NoBackdrop());

        // All three lists and each one after its own command has run, which is what this had to
        // become: measured by the auditor, deleting Mark(...) from the SelectedDevice setter and
        // from the SelectedSubtitle setter left every suite green. Loading lights the row, so a
        // surface asked only after it loads cannot tell a wash that follows the choice from a
        // photograph of the first one.
        foreach (var (surface, choose) in Lists())
        {
            var (window, rows, radios) = Show(surface);
            try
            {
                Assert.NotEmpty(rows);
                var lit = Assert.Single(rows, row => row.Classes.Contains("selected"));
                var chosen = Assert.Single(radios, radio => radio.IsChecked == true);

                // The row that is washed is the row of the radio that is checked — a wash on one
                // row and a check on another is two lists disagreeing about one choice.
                Assert.Contains(chosen, lit.GetVisualDescendants().OfType<RadioButton>());

                choose();
                Dispatcher.UIThread.RunJobs();

                var moved = Assert.Single(rows, row => row.Classes.Contains("selected"));
                var checkedNow = Assert.Single(radios, radio => radio.IsChecked == true);
                Assert.NotSame(lit, moved);
                Assert.Contains(checkedNow, moved.GetVisualDescendants().OfType<RadioButton>());
            }
            finally
            {
                window.Close();
            }
        }
    }

    /// <summary>
    /// Every option list this application draws, with the choice that moves it.
    /// </summary>
    /// <remarks>
    /// Three and not two: <c>TrackSelectorView</c> draws the audio list in one panel of the player
    /// and the subtitle list in another, so a surface that shows one of them has not shown the
    /// other. The subtitle half was the one nothing in this suite ever looked at.
    /// </remarks>
    private static (Control Surface, Action Choose)[] Lists()
    {
        var audio = TrackSelector();
        var subtitles = TrackSelector();
        var output = new AudioOutputViewModel(new TwoEndpoints());
        output.LoadAsync(cancellationToken: TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        return
        [
            (new TrackSelectorView { DataContext = audio, ShowsSubtitles = false },
                () => audio.ChooseAudioCommand.Execute(
                    audio.AudioTracks.First(option => !ReferenceEquals(option, audio.SelectedAudio)))),
            (new TrackSelectorView { DataContext = subtitles, ShowsAudio = false },
                () => subtitles.ChooseSubtitleCommand.Execute(
                    subtitles.SubtitleTracks.First(option => !ReferenceEquals(option, subtitles.SelectedSubtitle)))),
            (new AudioOutputView { DataContext = output },
                () => output.ChooseDeviceCommand.Execute(
                    output.Devices.First(option => !ReferenceEquals(option, output.SelectedDevice)))),
        ];
    }

    /// <summary>Two endpoints, so the output list has a row that is chosen and one that is not.</summary>
    private sealed class TwoEndpoints : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioOutputDevice>>(
            [
                new("endpoint-receiver", "Receptor HDMI", [AudioChannelLayout.Stereo, AudioChannelLayout.Surround71], IsDefault: true, IsAvailable: true),
                new("endpoint-headset", "Auriculares", [AudioChannelLayout.Stereo], IsDefault: false, IsAvailable: true),
            ]);
    }

    /// <summary>One endpoint whose name is far wider than the panel that has to draw it.</summary>
    private sealed class OneVeryLongName : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioOutputDevice>>(
            [
                new(
                    "endpoint-long",
                    "Altavoces del receptor de cine en casa del salón principal (HDMI 2, ARC)",
                    [AudioChannelLayout.Stereo, AudioChannelLayout.Surround71],
                    IsDefault: true,
                    IsAvailable: true),
            ]);
    }

    private sealed class NoPreferences : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PlaybackPreference?>(null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Puts back every resource the appearance service writes, so the surfaces the rest of this
    /// suite builds are not left wearing this measurement's accent.
    /// </summary>
    private sealed class ThemeScope : IDisposable
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

        public ThemeScope(Avalonia.Application application)
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
