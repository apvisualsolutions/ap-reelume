// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Presentation.Courses;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Courses;

/// <summary>
/// The course thread's card draws the numbers the prototype draws for it.
/// </summary>
/// <remarks>
/// This is the first view of the corner batch, and it is written view-shaped rather than class-shaped
/// on purpose: a radius belongs to an element of a screen and not to a number.
/// <c>SurfaceCornerTests</c> holds the corner and <c>OverlineTests</c> holds the two headings; what
/// is here is everything else this card owes the prototype.
/// <para>
/// <b>The tree draws what the table says, and the table says what the design draws.</b> The same two
/// halves <c>ButtonShapeTests</c> has, for its reason: a table of hand-copied numbers certifies
/// itself, and the handover that opened this batch carried two numbers measurement contradicted.
/// </para>
/// <para>
/// <b>What this found on 2026-09-03.</b> The tree drew «Dónde lo dejaste» as a 20 px semi-bold
/// subtitle where the design draws a 10 px overline in the accent's ink, so the card's weight sat on
/// its own title rather than on the lesson underneath — the one line somebody opens this screen to
/// read. The card was an ordinary one too: the shell's border over the card's surface, where the
/// design washes it in the accent and rings it in the accent itself. Changing only the lettering
/// would have left the card halfway between two designs, which is why neither half was done alone.
/// </para>
/// </remarks>
public sealed class CourseThreadCardTests
{
    /// <summary>
    /// Each number this card draws, what it is, and how it is found in the design.
    /// </summary>
    /// <remarks>
    /// The pattern travels with the pairing rather than being derived from the value, because the
    /// design writes this card as inline style attributes and several of these numbers repeat inside
    /// them — 12 is the card's inner gap AND its corner, 10 is the rule over the recap AND the gap
    /// under the whole card. A pattern keyed on the number alone would match a neighbour and be
    /// perfectly consistent with itself while doing it.
    /// </remarks>
    private static readonly (string What, double Value, string Pattern)[] Pairings =
    [
        ("the card's padding", 18, @"gap:12px;padding:(?<value>[0-9.]+)px;border-radius:12px"),
        ("the card's inner gap", 12, @"flex-direction:column;gap:(?<value>[0-9.]+)px;padding:18px"),
        ("the gap under the card", 10, @"flex-direction:column;gap:(?<value>[0-9.]+)px;position:sticky"),
        ("the lesson's size", 15, @"font-size:(?<value>[0-9.]+)px;font-weight:600;text-wrap:pretty"">\{\{ crs\.threadLesson"),
        ("the lesson's weight", 600, @"font-size:15px;font-weight:(?<value>[0-9]+);text-wrap:pretty"">\{\{ crs\.threadLesson"),
        ("the minute's size", 12.5, @"font-size:(?<value>[0-9.]+)px;color:var\(--text2,\#5B6675\);margin-top:3px"),
        ("the gap between lesson and minute", 3, @"color:var\(--text2,\#5B6675\);margin-top:(?<value>[0-9.]+)px"">\{\{ crs\.threadMinute"),
        ("the recap's gap", 6, @"flex-direction:column;gap:(?<value>[0-9.]+)px;border-top:1px solid var\(--hair"),
        ("the rule over the recap", 10, @"border-top:1px solid var\(--hair,rgba\(15,23,42,\.09\)\);padding-top:(?<value>[0-9.]+)px"),
        ("a recap line's size", 12.5, @"align-items:baseline;font-size:(?<value>[0-9.]+)px;color:var\(--text2"),
    ];

    /// <summary>The numbers this card's classes draw are the ones the design writes.</summary>
    /// <remarks>
    /// Without this half the classes below are a second set of hand-copied numbers, and a pairing
    /// that drifts from the design certifies itself.
    /// </remarks>
    [Fact]
    public void The_numbers_this_card_draws_are_the_ones_the_design_writes()
    {
        var design = File.ReadAllText(RepositoryLayout.PathFromRoot("design/AP Reelume.dc.html"));

        foreach (var (what, value, pattern) in Pairings)
        {
            var match = Regex.Match(design, pattern, RegexOptions.None, TimeSpan.FromSeconds(5));

            Assert.True(match.Success, $"the design no longer draws {what}, so this card is paired with nothing.");
            Assert.Equal(
                value,
                double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The card the view builds draws those numbers, measured on the controls rather than read off
    /// the token file.
    /// </summary>
    /// <remarks>
    /// The half this repository has lost twice: reading the token file certifies what a class
    /// declares and not what any view asks for, and a class nobody wears draws nothing while passing
    /// every reading of the file it is declared in.
    /// </remarks>
    [AvaloniaFact]
    public void The_card_the_view_builds_draws_them()
    {
        var view = new CourseDetailsView();
        var window = new Window { Width = 1000, Height = 800, Content = view };

        double cardCorner, cardPadding, headGap, lessonSize, detailSize, recapGap, columnGap;
        int lessonWeight;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var card = Wearing<Border>(view, "thread-card");
            var head = Wearing<StackPanel>(view, "thread-head");
            var lesson = Wearing<TextBlock>(view, "thread-lesson");
            var detail = Wearing<TextBlock>(view, "thread-detail");
            var recap = Wearing<StackPanel>(view, "thread-recap-body");
            var column = Wearing<StackPanel>(view, "thread-column");

            cardCorner = card.CornerRadius.TopLeft;
            cardPadding = card.Padding.Left;
            headGap = head.Spacing;
            lessonSize = lesson.FontSize;
            // The NUMBER and not the name: Avalonia's FontWeight.SemiBold and DemiBold are one
            // value, and ToString picks DemiBold, so a comparison on the name fails over spelling
            // while agreeing about the weight. 600 is also what the design writes.
            lessonWeight = (int)lesson.FontWeight;
            detailSize = detail.FontSize;
            recapGap = recap.Spacing;
            columnGap = column.Spacing;
        }
        finally
        {
            // Closed in a finally, always: an assertion that fails between Show and Close leaves the
            // window open, which breaks the harness's per-test isolation and surfaces as a «Test Case
            // Cleanup Failure» naming some unrelated test that never even ran. Measured in this
            // repository on 2026-08-28 and again on 2026-09-03, both times from this same shape.
            window.Close();
        }

        Assert.Equal(12, cardCorner);
        Assert.Equal(18, cardPadding);
        Assert.Equal(3, headGap);
        Assert.Equal(15, lessonSize);
        Assert.Equal(600, lessonWeight);
        Assert.Equal(12.5, detailSize);
        Assert.Equal(6, recapGap);
        Assert.Equal(10, columnGap);
    }

    /// <summary>
    /// The note that explains the thread sits outside the card, with the card the only thing above
    /// it.
    /// </summary>
    /// <remarks>
    /// Asserted as a position rather than as a spacing: the note was <b>inside</b> the card until
    /// 2026-09-03, where it read as a fifth line of the answer instead of as a remark about it, and
    /// a test on its gap would have been green the whole time.
    /// </remarks>
    [AvaloniaFact]
    public void The_note_sits_outside_the_card()
    {
        var view = new CourseDetailsView();
        var window = new Window { Width = 1000, Height = 800, Content = view };

        bool insideCard;
        int columnChildren;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var card = Wearing<Border>(view, "thread-card");
            var column = Wearing<StackPanel>(view, "thread-column");
            var note = column.Children.OfType<TextBlock>().Single();

            insideCard = card.GetVisualDescendants().Contains(note);
            columnChildren = column.Children.Count;
        }
        finally
        {
            window.Close();
        }

        Assert.False(insideCard, "the note is back inside the card, where it reads as part of the answer.");

        // The card and the note, and nothing else: a third child would be something this pairing
        // never looked at.
        Assert.Equal(2, columnChildren);
    }

    /// <summary>
    /// The wash reaches the screen: the card is told from the page by its own fill, and by much more
    /// of it than the ordinary card it used to be.
    /// </summary>
    /// <remarks>
    /// <b>Counted in pixels rather than read back off a brush.</b> Everything above measures
    /// properties, and a property can differ while the screen does not — this repository has had two
    /// gates green over two pixels of visible misalignment for exactly that reason. What changed here
    /// is a colour a person sees, so it is counted as ink.
    /// <para>
    /// The comparison is against what this card WAS — the shell's border over the card's surface —
    /// because that is the claim: not «the card is visible» but «the card is now a surface rather
    /// than an outline». In the light theme those two fills are <c>#FFFFFF</c> on <c>#FBFCFE</c>,
    /// which is a card told apart from its page by a hairline alone.
    /// </para>
    /// <para>
    /// <b>The threshold is a parameter of the measurement and not a constant, so it is swept rather
    /// than chosen.</b> The one that separates ink from paper over 14 px text lies about 10 px text —
    /// measured in this repository the day before this — and the one that separates two washes is
    /// smaller again. Three of them run and all three have to reach the same verdict; a conclusion
    /// that held at one number and not at its neighbours would be luck rather than a reading.
    /// </para>
    /// <para>
    /// <b>Its limitation, written rather than assumed.</b> Both high contrast dictionaries resolve
    /// the wash and the page to one ink, and there the border carries the card alone — which is why
    /// the class declares one. This measures the theme the harness runs, and a silence from it is
    /// not a certificate for the other three.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(20)]
    public void The_wash_reaches_the_screen(int threshold)
    {
        var washed = FillDifferingFromThePage("thread-card", threshold);
        var plain = FillDifferingFromThePage(null, threshold);

        // Anti-blindness floor: a scene that painted nothing would report no ink for either and
        // satisfy any comparison between the two.
        Assert.True(
            washed > 0,
            $"at a threshold of {threshold} the washed card painted nothing at all, so this "
            + "measured nothing.");

        Assert.True(
            washed > plain * 4,
            $"at a threshold of {threshold} the washed card covers {washed} px of the page and the "
            + $"ordinary one covered {plain}: the accent's wash is not reaching the screen.");
    }

    /// <summary>
    /// How many pixels of a 120×60 box are NOT the page's own colour, with the card drawn over it.
    /// </summary>
    /// <remarks>
    /// A null class draws the card this used to be — the shell's border over the card's surface —
    /// rather than nothing at all, so the two readings differ by the decision and not by whether
    /// anything was drawn.
    /// </remarks>
    private static int FillDifferingFromThePage(string? styleClass, int threshold)
    {
        var box = new Border
        {
            Width = 120,
            Height = 60,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };

        if (styleClass is null)
        {
            box.Background = Page("CardSurfaceBrush");
            box.BorderBrush = Page("ShellBorderBrush");
            box.BorderThickness = new Thickness(1);
        }
        else
        {
            box.Classes.Add(styleClass);
        }

        var host = new Border { Background = Page("ShellSurfaceBrush"), Child = box };
        var window = new Window { Width = 160, Height = 100, Content = host };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("the headless backend returned no frame.");
            using var buffer = frame.Lock();
            var pixels = frame.PixelSize;
            var bytes = new byte[buffer.RowBytes * pixels.Height];
            System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            // The page's own colour is read from the frame rather than named, at a point the box
            // cannot reach: naming it would be a third copy of a token, and a wrong one the day the
            // shell's surface moves.
            var edge = ((pixels.Height - 2) * buffer.RowBytes) + ((pixels.Width - 2) * 4);
            var (b, g, r) = (bytes[edge], bytes[edge + 1], bytes[edge + 2]);

            var differing = 0;
            for (var y = 0; y < Math.Min(60, pixels.Height); y++)
            {
                for (var x = 0; x < Math.Min(120, pixels.Width); x++)
                {
                    var i = (y * buffer.RowBytes) + (x * 4);

                    if (Math.Abs(bytes[i] - b) >= threshold
                        || Math.Abs(bytes[i + 1] - g) >= threshold
                        || Math.Abs(bytes[i + 2] - r) >= threshold)
                    {
                        differing++;
                    }
                }
            }

            return differing;
        }
        finally
        {
            // Closed in a finally for the reason written above: a throw between Show and Close leaves
            // the window open and the failure surfaces against a test that never ran.
            window.Close();
        }
    }

    /// <summary>
    /// A brush out of the application's own dictionaries, in the variant it is actually running.
    /// </summary>
    /// <remarks>
    /// The variant is passed rather than left null: these brushes live in theme dictionaries, so a
    /// null variant resolves none of them — which is a scene painted in the base theme's colours
    /// while claiming to be the application's.
    /// </remarks>
    private static Avalonia.Media.IBrush Page(string key) =>
        Avalonia.Application.Current!.TryGetResource(
            key, Avalonia.Application.Current!.ActualThemeVariant, out var value)
            && value is Avalonia.Media.IBrush brush
            ? brush
            : throw new InvalidOperationException($"{key} did not resolve, so this scene is not the application's.");

    /// <summary>The first control in the view's tree wearing a style class.</summary>
    private static T Wearing<T>(Visual root, string styleClass)
        where T : Control
    {
        var found = root.GetVisualDescendants()
            .OfType<T>()
            .Where(control => control.Classes.Contains(styleClass))
            .ToArray();

        // Anti-blindness floor: a reader that found nothing would leave every assertion below
        // measuring a control that was never built.
        Assert.True(
            found.Length > 0,
            $"no {typeof(T).Name} in this view wears «{styleClass}», so this measured nothing.");
        return found[0];
    }
}
