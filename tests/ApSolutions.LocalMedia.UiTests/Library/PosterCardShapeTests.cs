// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using System.Globalization;
using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.UiTests.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The four marks the prototype paints over a cover — the kind chip, the watched tick, the progress
/// track and the unavailable veil — measured in pixels rather than read off the markup.
/// </summary>
/// <remarks>
/// Read in pixels because every one of them is something a person sees on top of artwork: a tick
/// whose <c>Margin</c> is 6 and whose circle is drawn 2 px wide would satisfy any assertion made
/// against the model. The scene paints its own colours so that a threshold survives a theme change,
/// and each test states the floor it found before it subtracts anything.
/// </remarks>
public sealed class PosterCardShapeTests
{
    private static readonly Color Marker = Color.Parse("#00FF00");

    /// <summary>
    /// The scene's second colour, for what is drawn ON the accent: the tick's check.
    /// </summary>
    /// <remarks>
    /// It was the marker's own green until the gate audit of 2026-09-12 pointed out what that cost:
    /// with the check invisible, the disc's area and centroid could not see it, and dropping the
    /// 13 px class — or growing it back to the 14 it came from — passed every assertion.
    /// </remarks>
    private static readonly Color Ink = Color.Parse("#0000FF");

    /// <summary>The alpha PosterVeilBrush carries, which is .57 and not the prototype's .55.</summary>
    private const double VeilAlpha = 145 / 255.0;

    /// <summary>The alpha PosterChipSurfaceBrush carries, which is the prototype's .62.</summary>
    private const double ChipAlpha = 158 / 255.0;

    /// <summary>
    /// The tick is 20 across and sits 7 px inside the cover's top and right edges.
    /// </summary>
    /// <remarks>
    /// <c>design/AP Reelume.dc.html:313</c> puts it at <c>top:6px;right:6px</c> with
    /// <c>width/height:20</c> inside a cover whose hairline is 1 px, which is 7 from the outer edge.
    /// The tree drew 22 at 8 (9 from the outer edge) until this was measured.
    /// </remarks>
    [AvaloniaFact]
    public void The_watched_tick_is_twenty_across_and_seven_inside_the_cover()
    {
        var (card, window) = Mount(new ShapeStub("Arrival") { IsWatched = true });
        try
        {
            var cover = Cover(card, window);

            // The corner the tick lives in, and only it: text drawn anywhere else on the card leaves
            // subpixel fringes whose red channel is 0 and whose blue is not, and the first run of
            // this counted them as part of the disc — a box of 140 by 238 with the area of a tick.
            var tick = Pixels(window).Marked(Corner(cover));

            // The floor: something green was painted, and it is a disc rather than a square — a
            // threshold would have called both of them "20 px of accent".
            Assert.True(tick.Area > 100, $"Only {tick.Area:F1} px of accent were painted, so nothing was measured.");
            var roundness = tick.Area / (tick.Box.Width * tick.Box.Height);
            Assert.True(
                roundness is > 0.70 and < 0.85,
                $"The tick fills {roundness:F3} of its box {tick.Box} (area {tick.Area:F1}, glyph {tick.Glyph:F1}); a disc fills {Math.PI / 4:F3} and a square 1.");

            Assert.Equal(20, tick.Diameter, 0);
            Assert.Equal(7, tick.CentreY - (tick.Diameter / 2) - cover.Y, 0);
            Assert.Equal(7, cover.X + cover.Width - (tick.CentreX + (tick.Diameter / 2)), 0);

            // And the check inside it, which the prototype draws at 13 (:3242). Its ink is measured
            // as area and not as a box, because area goes as the square of the size: 13 inks 6,89 px
            // of stroke here, the 14 this came from inks 8 and the base class's 16 inks 10,4. A box
            // would have had to tell 8,0 px from 8,5.
            Assert.InRange(tick.Glyph, 6.3, 7.5);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A watched title whose medium is out of reach shows the veil and no tick.
    /// </summary>
    /// <remarks>
    /// The prototype's condition is <c>done: (…) &amp;&amp; av</c> (<c>:2416</c>): the veil owns the
    /// cover when the medium is gone, and a tick over it would be the card saying two things about
    /// the same title at once.
    /// </remarks>
    [AvaloniaFact]
    public void The_tick_gives_way_when_the_medium_is_out_of_reach()
    {
        var (reachableCard, first) = Mount(new ShapeStub("Arrival") { IsWatched = true });
        try
        {
            Assert.True(
                Pixels(first).Marked(Corner(Cover(reachableCard, first))).Area > 100,
                "The reachable card drew no tick, so the absence below proves nothing.");
        }
        finally
        {
            first.Close();
        }

        var (card, window) = Mount(new ShapeStub("Arrival") { IsWatched = true, IsAvailable = false });
        try
        {
            Assert.True(
                Pixels(window).Marked(Corner(Cover(card, window))).Area < 1,
                "The tick is still painted over the veil.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The track is three pixels of white at a quarter over the artwork, not a grey plate.
    /// </summary>
    /// <remarks>
    /// <c>design/AP Reelume.dc.html:312</c> paints <c>rgba(255,255,255,.25)</c> across the foot of
    /// the cover and fills it with the accent. The tree painted <c>ControlFillBrush</c>, which is
    /// opaque, so the artwork stopped at the bar instead of showing through it.
    ///
    /// Measured against the same card without progress rather than against a number: the artwork is
    /// four layers deep and its colour at the foot of the cover is not something to write down. The
    /// two cards differ in exactly the rows the track occupies, and in those rows the pixels are the
    /// artwork's own, a quarter of the way to white.
    /// </remarks>
    [AvaloniaFact]
    public void The_progress_track_is_white_at_a_quarter_over_the_artwork()
    {
        var (bare, without) = Mount(new ShapeStub("Arrival"), artwork: true);
        var (card, with) = Mount(
            new ShapeStub("Arrival") { HasKnownProgress = true, CompletedFraction = 0.42 },
            artwork: true);
        try
        {
            var cover = Cover(card, with);
            var plain = Pixels(without);
            var running = Pixels(with);

            // Read away from both ends: the cover's corner is rounded at 10 and the bar's own ends
            // are rounded by the base theme, so a column near either edge is a partly covered pixel
            // rather than the track.
            var right = (int)(cover.X + cover.Width) - 30;
            var changed = new List<int>();
            for (var y = (int)cover.Y; y < (int)(cover.Y + cover.Height); y++)
            {
                if (plain.At(right, y) != running.At(right, y))
                {
                    changed.Add(y);
                }
            }

            Assert.Equal(3, changed.Count);
            Assert.Equal((int)(cover.Y + cover.Height) - 2, changed[^1]);

            // The track: the artwork's own colour, a quarter of the way to white.
            foreach (var y in changed)
            {
                var art = plain.At(right, y);
                var track = running.At(right, y);
                AssertQuarterToWhite(art.R, track.R, "red");
                AssertQuarterToWhite(art.G, track.G, "green");
                AssertQuarterToWhite(art.B, track.B, "blue");
            }

            // And the fill is the accent, over the fraction watched of the rule the bar draws.
            var middle = changed[1];
            var filled = 0;
            var rule = 0;
            var artwork = 0;
            var lastFilled = -1;
            for (var x = (int)cover.X; x < (int)(cover.X + cover.Width); x++)
            {
                if (running.At(x, middle) == Marker)
                {
                    filled++;
                    lastFilled = x;
                }

                if (plain.At(x, middle) != running.At(x, middle))
                {
                    rule++;
                }

                if (plain.At(x, middle) != Colors.White)
                {
                    artwork++;
                }
            }

            Assert.True(filled > 0, "Nothing was painted in the accent, so the fill was not measured.");
            Assert.True(artwork > 100, $"Only {artwork} px of cover were found on that row, so the span below proves nothing.");

            // The rule spans the cover from edge to edge, as :312's left:0;right:0 does. It is
            // compared against the artwork on the same row and not against the card's width: the
            // cover's corner is rounded, so both of them lose the same few pixels there.
            Assert.Equal(artwork, rule);

            // The fill ends where 42 % of the rule ends, measured at its far edge rather than by
            // counting: the near edge loses a pixel or two to the same rounded corner, and a count
            // would quietly pay for it twice.
            var watched = lastFilled + 1 - (cover.X + 1);
            Assert.InRange(watched, (0.42 * (cover.Width - 2)) - 1.5, (0.42 * (cover.Width - 2)) + 1.5);
        }
        finally
        {
            with.Close();
            without.Close();
        }
    }

    /// <summary>
    /// An unreachable medium veils the whole cover and writes in white at its foot.
    /// </summary>
    /// <remarks>
    /// <c>design/AP Reelume.dc.html:311</c> is <c>inset:0</c> with <c>rgba(9,12,16,.55)</c>, white at
    /// 11 px in 600, at <c>padding:8</c> from the bottom left. The tree drew an amber pill in the
    /// corner, which is a different sentence: the pill says a badge is attached to the card, the veil
    /// says the cover itself is out of reach.
    ///
    /// The alpha here is .57 rather than .55, and the reason is written beside the token: over a
    /// white cover the prototype's own value leaves an 11 px white word at 4,33:1.
    /// </remarks>
    [AvaloniaFact]
    public void The_veil_dims_the_whole_cover_and_writes_in_white_at_its_foot()
    {
        var (_, reachable) = Mount(new ShapeStub("Arrival"), artwork: true);
        var (card, window) = Mount(new ShapeStub("Arrival") { IsAvailable = false }, artwork: true);
        try
        {
            var cover = Cover(card, window);
            var plain = Pixels(reachable);
            var veiled = Pixels(window);

            // The floor: the badge is there, it fills the cover, and it is painted.
            var badge = Assert.Single(card.GetVisualDescendants().OfType<UnavailableBadge>());
            Assert.True(badge.IsEffectivelyVisible, "The badge is not visible, so nothing below is about the veil.");
            var surface = Assert.Single(
                badge.GetVisualDescendants().OfType<Border>(),
                border => border.Background is not null);
            Assert.Equal(cover.Width - 2, surface.Bounds.Width, 0);

            // The veil, over the artwork, everywhere the words and the chip are not.
            var sampled = 0;
            for (var y = (int)cover.Y + 40; y < (int)(cover.Y + cover.Height) - 40; y += 7)
            {
                for (var x = (int)cover.X + 8; x < (int)(cover.X + cover.Width) - 8; x += 9)
                {
                    sampled++;
                    AssertVeiled(plain.At(x, y), veiled.At(x, y), $"at {x},{y}");
                }
            }

            Assert.True(sampled > 40, $"Only {sampled} points were compared, so the veil was barely measured.");

            // And the word, in white, at the foot of the cover.
            var word = veiled.WhiteInk(cover, plain);
            Assert.True(word.Width > 20, $"The white word measures {word.Width} px across, so it was not found.");
            Assert.InRange(word.X - cover.X, 8, 11);
            Assert.InRange(cover.Y + cover.Height - word.Bottom, 8, 12);
            Assert.InRange(word.Height, 7, 16);
        }
        finally
        {
            window.Close();
            reachable.Close();
        }
    }

    /// <summary>
    /// The veil goes over the chip and under the rule, which is the prototype's painting order.
    /// </summary>
    /// <remarks>
    /// <c>:309</c> to <c>:313</c> paint artwork, chip, veil, track, tick in that order, so a card
    /// that is out of reach dims its own chip and still shows how far in you are. Both halves are
    /// measured by composition: dimming the track instead would read 54 where 88 belongs.
    /// </remarks>
    [AvaloniaFact]
    public void The_veil_dims_the_chip_and_the_rule_is_painted_over_the_veil()
    {
        var (_, reachable) = Mount(
            new ShapeStub("Arrival") { HasKnownProgress = true, CompletedFraction = 0.42 },
            artwork: true);
        var (card, window) = Mount(
            new ShapeStub("Arrival") { HasKnownProgress = true, CompletedFraction = 0.42, IsAvailable = false },
            artwork: true);
        try
        {
            var cover = Cover(card, window);
            var plain = Pixels(reachable);
            var veiled = Pixels(window);

            // Inside the chip, which is 8 px in from the cover's inside and 21,75 tall.
            var chipX = (int)cover.X + 14;
            var chipY = (int)cover.Y + 18;
            Assert.True(
                plain.At(chipX, chipY) != plain.At((int)cover.X + 70, (int)cover.Y + 100),
                "The chip was not found where it was sampled, so its dimming proves nothing.");
            AssertVeiled(plain.At(chipX, chipY), veiled.At(chipX, chipY), "inside the chip");

            // And the rule, three rows up from the cover's foot, is painted last of the two.
            var ruleY = (int)(cover.Y + cover.Height) - 3;
            var x = (int)(cover.X + cover.Width) - 30;
            var artwork = plain.At(x, ruleY - 6);
            var overVeil = Quarter(Veil(artwork.R));
            var underVeil = Veil((byte)Quarter(artwork.R));
            Assert.True(
                Math.Abs(overVeil - underVeil) > 10,
                "The two orders cannot be told apart at this pixel, so nothing was proved.");
            Assert.InRange(veiled.At(x, ruleY).R, overVeil - 2, overVeil + 2);
        }
        finally
        {
            window.Close();
            reachable.Close();
        }
    }

    /// <summary>
    /// The two words written ON a cover hold 4,5:1 over the worst cover there is.
    /// </summary>
    /// <remarks>
    /// A white one. Both of them sit on a translucent tint over artwork nobody chose — a provider's
    /// poster or somebody's own picture — so the ratio has to be computed over what that tint leaves,
    /// and the white end of the range is where it is thinnest.
    ///
    /// This is why the veil is .57 and not the prototype's .55: at .55 the 11 px word reads 4,33:1,
    /// which is the negative control below rather than a number written in a document. The chip's own
    /// .62 needs no such change — it reads 5,55:1 — and both are asserted in the four dictionaries,
    /// where the three brushes carry the same values because what they sit on is not a theme surface.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void The_words_over_a_cover_hold_their_ratio_on_a_white_one(string theme)
    {
        ThemeVariant variant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            "HighContrastLight" => AppThemeVariants.HighContrastLight,
            _ => AppThemeVariants.HighContrastDark,
        };

        var ink = ThemeContrast.Token(variant, "PosterChipInkBrush");
        var chip = ThemeContrast.Token(variant, "PosterChipSurfaceBrush");
        var veil = ThemeContrast.Token(variant, "PosterVeilBrush");

        var onChip = ThemeContrast.Ratio(ink, ThemeContrast.Painted(new SolidColorBrush(chip), Colors.White));
        var onVeil = ThemeContrast.Ratio(ink, ThemeContrast.Painted(new SolidColorBrush(veil), Colors.White));

        Assert.True(onChip >= 4.5, $"the chip's word reads {onChip:F2}:1 over a white cover in {theme}.");
        Assert.True(onVeil >= 4.5, $"the veil's word reads {onVeil:F2}:1 over a white cover in {theme}.");

        // The negative control, and the reason the veil is not the prototype's number: its own .55
        // does not reach the same bar, so a test that passed with either would be measuring nothing.
        var asDrawn = ThemeContrast.Ratio(
            ink,
            ThemeContrast.Painted(new SolidColorBrush(Color.FromArgb(140, 9, 12, 16)), Colors.White));
        Assert.True(asDrawn < 4.5, $"the prototype's own .55 reads {asDrawn:F2}:1, so .57 buys nothing.");
    }

    /// <summary>
    /// The kind chip is a 10,5 px word on a tint at .62, and what shows through it is blurred.
    /// </summary>
    /// <remarks>
    /// <c>design/AP Reelume.dc.html:2374</c>: <c>top/left 8</c>, <c>padding 3px 9px 3px 7px</c>,
    /// <c>rgba(9,12,16,.62)</c>, <c>backdrop-filter: blur(8px)</c>, <c>#fff</c>, 10,5 px in 600. The
    /// tree carried 12 px at a normal weight on a flat .72 tint.
    ///
    /// The blur is asserted by what it does rather than by the property that asks for it: the
    /// artwork's diagonal hatch crosses the chip, and a tint alone lets it through at .38 of its
    /// strength. Both are measured on the same card — the hatch beside the chip is the control that
    /// says the scene had something to flatten.
    /// </remarks>
    [AvaloniaFact]
    public void The_kind_chip_is_a_small_bold_word_on_a_blurred_tint()
    {
        var (_, without) = Mount(new ShapeStub("Arrival") { HasKind = false }, artwork: true);
        var (card, window) = Mount(new ShapeStub("Arrival"), artwork: true);
        try
        {
            var cover = Cover(card, window);
            var plain = Pixels(without);
            var chipped = Pixels(window);

            // The box: what the chip covers, read as everything that changed in the cover's corner.
            var box = chipped.Changed(new Rect(cover.X, cover.Y, 120, 60), plain);
            Assert.True(box.Width > 30, $"The chip measures {box.Width} px across, so it was not found.");
            Assert.Equal(9, box.X - cover.X);
            Assert.Equal(9, box.Y - cover.Y);
            Assert.Equal(21.75, box.Height, 0);

            // The word, in the size and the weight the prototype writes it at.
            var word = Assert.Single(
                card.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text is "Película");
            Assert.Equal(10.5, word.FontSize);
            Assert.Equal(FontWeight.SemiBold, word.FontWeight);

            // The tint: .62 of the way to 9,12,16, on average over the chip's own back.
            var mid = (int)box.Y + 4;
            double bare = 0, painted = 0;
            var energyBare = 0.0;
            var energyPainted = 0.0;
            var count = 0;
            for (var x = (int)box.X + 4; x < (int)(box.X + box.Width) - 4; x++)
            {
                bare += plain.At(x, mid).G;
                painted += chipped.At(x, mid).G;
                energyBare += Math.Abs(plain.At(x, mid).G - plain.At(x + 1, mid).G);
                energyPainted += Math.Abs(chipped.At(x, mid).G - chipped.At(x + 1, mid).G);
                count++;
            }

            bare /= count;
            painted /= count;
            energyBare /= count;
            energyPainted /= count;

            var expected = (bare * (1 - ChipAlpha)) + (12 * ChipAlpha);
            Assert.InRange(painted, expected - 3, expected + 3);

            // The blur: the hatch beside the chip is the control, and .38 of it is what a tint alone
            // would have let through. The prototype was measured the same way on 2026-09-12 —
            // 2,73 of edge in the artwork, 1,04 of it left after its own .62 tint, and 0,066 inside
            // the chip: the blur alone takes 15,7 times what the tint took.
            Assert.True(
                energyBare * (1 - ChipAlpha) > 0.8,
                $"The artwork under the chip is too flat ({energyBare:F2}) to tell a blur from a tint.");
            Assert.True(
                energyPainted < energyBare * (1 - ChipAlpha) / 5,
                $"The chip's back carries {energyPainted:F2} of edge where a tint alone leaves "
                    + $"{energyBare * (1 - ChipAlpha):F2}: what is behind it is not blurred.");

            // And it is the artwork from BEHIND THE CHIP, at the place the chip stands. A blur put
            // down 24 px off its own box is just as flat and, over the synthetic hatch, carries the
            // same mean — the gate audit of this batch walked out through exactly that hole — so the
            // brush's two rects are read as well: its own box out of the artwork, and that box put
            // back where the bleed leaves room for it.
            var backdrop = Assert.IsType<VisualBrush>(
                Assert.Single(
                    card.GetVisualDescendants().OfType<Border>(),
                    border => border.Effect is BlurEffect)
                    .Background);
            var chip = Assert.Single(
                card.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("poster-chip"));
            Assert.Equal(
                new RelativeRect(
                    chip.Bounds.X,
                    chip.Bounds.Y,
                    chip.Bounds.Width,
                    chip.Bounds.Height,
                    RelativeUnit.Absolute),
                backdrop.SourceRect);
            Assert.Equal(
                new RelativeRect(
                    PosterCardView.ChipBackdropBleed,
                    PosterCardView.ChipBackdropBleed,
                    chip.Bounds.Width,
                    chip.Bounds.Height,
                    RelativeUnit.Absolute),
                backdrop.DestinationRect);
            Assert.Equal(TileMode.FlipXY, backdrop.TileMode);
        }
        finally
        {
            window.Close();
            without.Close();
        }
    }

    /// <summary>The veil's colour over one channel of the artwork, at the alpha the token carries.</summary>
    private static byte Veil(byte artwork) => (byte)Math.Round((artwork * (1 - VeilAlpha)) + (9 * VeilAlpha));

    /// <summary>White at a quarter over one channel of whatever is underneath.</summary>
    private static int Quarter(byte under) => (int)Math.Round((under * 0.75) + (255 * 0.25));

    private static void AssertVeiled(Color plain, Color veiled, string where)
    {
        foreach (var (bare, painted, ink) in new[]
        {
            (plain.R, veiled.R, 9),
            (plain.G, veiled.G, 12),
            (plain.B, veiled.B, 16),
        })
        {
            var expected = (int)Math.Round((bare * (1 - VeilAlpha)) + (ink * VeilAlpha));
            Assert.True(
                Math.Abs(painted - expected) <= 2,
                $"{where}: {bare} became {painted} where {expected} belongs; the alpha painted is "
                    + $"{(bare - painted) / (double)(bare - ink):F3} and the token carries {VeilAlpha:F3}.");
        }

        Assert.True(plain != veiled, $"Nothing changed {where}, so the veil is not there.");
    }

    /// <summary>
    /// Without the grid's class the card is a rail's card, and its three numbers are the old ones.
    /// </summary>
    /// <remarks>
    /// The other direction of the same decision, and the one the gate audit found unguarded: taking
    /// <c>.grid-tile</c> off any of the tile's selectors left every test green, and the three rails,
    /// the detail cards and the episode rows would have taken the tile's 13,5 px title and its
    /// stacked lines in silence. The prototype draws its own rails differently — a gap of 8, a title
    /// at 13, a subtitle at 11 and no status line — so the tile's numbers are the wrong numbers
    /// there, and closing that difference is its own piece of work.
    ///
    /// The chip's ink is asserted here too: its word takes the colour from the chip's own
    /// <c>TextElement.Foreground</c>, and until this was written a chip whose word went to
    /// <c>TextPrimaryBrush</c> — near black on a dark tint — passed all 1483.
    /// </remarks>
    [AvaloniaFact]
    public void Without_the_grids_class_the_card_keeps_the_rails_numbers()
    {
        var (card, window) = Mount(new ShapeStub("Arrival"));
        try
        {
            var stack = Assert.Single(
                card.GetVisualDescendants().OfType<StackPanel>(),
                panel => panel.Name == "CardStack");
            var caption = Assert.Single(
                card.GetVisualDescendants().OfType<StackPanel>(),
                panel => panel.Name == "CoverCaption");
            var title = Assert.Single(
                card.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Name == "CardTitle");
            var meta = Assert.Single(
                card.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Name == "CardMeta");

            Assert.False(card.Classes.Contains("grid-tile"), "This card carries the grid's class, so it is not a rail's.");
            Assert.Equal(Scalar("DensityGutter"), stack.Spacing);
            Assert.Equal(Scalar("Space8"), caption.Spacing);
            Assert.Equal(Scalar("FontSizeCaption"), meta.FontSize);
            Assert.Equal(new Thickness(0), meta.Margin);

            // The title carries no size of its own in a rail: it inherits the button's.
            Assert.NotEqual(13.5, title.FontSize);
            Assert.True(double.IsNaN(title.LineHeight), $"The rail's title sets a line height of {title.LineHeight}.");

            // And the chip's word is white, from the chip's own ink rather than the page's.
            var word = Assert.Single(
                card.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Name == "KindWord");
            Assert.Equal(
                ThemeColour("PosterChipInkBrush"),
                Assert.IsAssignableFrom<ISolidColorBrush>(word.Foreground).Color);
        }
        finally
        {
            window.Close();
        }
    }

    private static double Scalar(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can spend it.");
        return Assert.IsType<double>(value);
    }

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current!;
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme variant.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    /// <summary>The cover's top right corner, which is where the prototype puts the tick.</summary>
    private static Rect Corner(Rect cover) => new(cover.X + cover.Width - 40, cover.Y, 40, 40);

    private static void AssertQuarterToWhite(byte artwork, byte painted, string channel)
    {
        var expected = (int)Math.Round((artwork * 0.75) + (255 * 0.25));
        Assert.InRange(painted, expected - 1, expected + 1);
        Assert.True(painted != artwork, $"The {channel} channel did not move, so the track is opaque.");
    }

    private static (PosterCardView Card, Window Window) Mount(ShapeStub model, bool artwork = false)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var card = new PosterCardView
        {
            DataContext = model,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var window = new Window
        {
            Width = 300,
            Height = 400,
            Background = Brushes.White,
            Content = card,
        };

        // The scene's own colours: a flat cover, no shadow, and an accent nothing else can be.
        window.Resources["PosterArtOpacity"] = artwork ? 1d : 0d;
        window.Resources["ElevationShadow"] = default(BoxShadows);
        window.Resources["AccentBrush"] = new SolidColorBrush(Marker);
        window.Resources["AccentTextBrush"] = new SolidColorBrush(Ink);
        window.Resources["ControlFillBrush"] = Brushes.Black;
        window.Resources["ShellHairlineBrush"] = Brushes.Black;
        window.Resources["PosterInitialsBrush"] = Brushes.Black;

        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (card, window);
    }

    private static Rect Cover(PosterCardView card, Window window)
    {
        var art = Assert.Single(card.GetVisualDescendants().OfType<PosterArtView>());
        var cover = Assert.IsType<Border>(art.GetVisualAncestors().OfType<Border>().First());
        var origin = ((Visual)cover).TranslatePoint(default, window);
        Assert.True(origin.HasValue, "The cover is not in the tree, so its pixels cannot be found.");
        return new Rect(origin.Value, cover.Bounds.Size);
    }

    private static Frame Pixels(Window window)
    {
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window drew no frame.");
        using var locked = frame.Lock();
        var bytes = new byte[locked.RowBytes * locked.Size.Height];
        Marshal.Copy(locked.Address, bytes, 0, bytes.Length);
        return new Frame(bytes, locked.RowBytes, locked.Size.Width, locked.Size.Height, locked.Format.ToString());
    }

    private sealed record Frame(byte[] Bytes, int RowBytes, int Width, int Height, string Format)
    {
        /// <summary>
        /// One pixel, read in the order the framebuffer actually carries.
        /// </summary>
        /// <remarks>
        /// The order is asked for rather than assumed: this harness hands back <c>Rgba8888</c>, and
        /// a reader that assumed BGRA — as the rest of this tree's pixel tests do — swaps red and
        /// blue. Every colour measured here until 2026-09-12 happened to be grey, white or green,
        /// which survive the swap; the veil, at 9,12,16, does not, and that is how it was found.
        /// </remarks>
        public Color At(int x, int y)
        {
            var index = (y * RowBytes) + (x * 4);
            return Format.StartsWith("Rgba", StringComparison.Ordinal)
                ? Color.FromRgb(Bytes[index], Bytes[index + 1], Bytes[index + 2])
                : Color.FromRgb(Bytes[index + 2], Bytes[index + 1], Bytes[index]);
        }

        /// <summary>
        /// Everything painted in the scene's marker colour, weighted by how much of each pixel it
        /// covers.
        /// </summary>
        /// <remarks>
        /// Coverage rather than a threshold: the edge of a 20 px disc is antialiased, so counting
        /// only the pixels that are exactly the marker reads 18 and counting everything that leans
        /// green reads 21. The sum of the coverage is the area the eye sees, and the area of a disc
        /// gives its diameter without any threshold at all. The scene paints the check in the marker
        /// colour too, so the glyph inside the disc does not punch a hole in the measurement.
        /// </remarks>
        /// <summary>
        /// The box the near-white ink inside a cover occupies, that the same cover did not already
        /// have.
        /// </summary>
        /// <remarks>
        /// Compared against the cover without the veil because the page behind shows through the
        /// cover's rounded corners, and a white page inside the cover's box reads as white ink: the
        /// first run of this measured the word starting at the cover's very edge.
        /// </remarks>
        public Rect WhiteInk(Rect cover, Frame without)
        {
            int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
            for (var y = (int)cover.Y; y < (int)(cover.Y + cover.Height); y++)
            {
                for (var x = (int)cover.X; x < (int)(cover.X + cover.Width); x++)
                {
                    var colour = At(x, y);
                    var before = without.At(x, y);
                    if (colour.R < 200 || colour.G < 200 || colour.B < 200)
                    {
                        continue;
                    }

                    if (before.R >= 200 && before.G >= 200 && before.B >= 200)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            return left == int.MaxValue ? default : new Rect(left, top, right - left + 1, bottom - top + 1);
        }

        /// <summary>The box of everything inside a region that another frame does not have.</summary>
        public Rect Changed(Rect region, Frame without)
        {
            int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
            for (var y = (int)region.Y; y < (int)(region.Y + region.Height); y++)
            {
                for (var x = (int)region.X; x < (int)(region.X + region.Width); x++)
                {
                    if (At(x, y) == without.At(x, y))
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            return left == int.MaxValue ? default : new Rect(left, top, right - left + 1, bottom - top + 1);
        }

        public Mark Marked(Rect region)
        {
            double area = 0, momentX = 0, momentY = 0, glyph = 0;
            int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
            for (var y = (int)region.Y; y < Math.Min((int)(region.Y + region.Height), Height); y++)
            {
                for (var x = (int)region.X; x < Math.Min((int)(region.X + region.Width), Width); x++)
                {
                    var colour = At(x, y);
                    if (colour.R > 0 || (colour.G == 0 && colour.B == 0))
                    {
                        continue;
                    }

                    // The disc is the marker's green and the check inside it is the scene's blue, so
                    // the disc's own area is both of them: a check that punched a hole in the count
                    // would shrink the diameter it is measured from.
                    var coverage = Math.Min(1.0, (colour.G + colour.B) / 255.0);
                    glyph += colour.B / 255.0;
                    area += coverage;
                    momentX += coverage * (x + 0.5);
                    momentY += coverage * (y + 0.5);
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            return area <= 0
                ? new Mark(0, 0, 0, 0, default)
                : new Mark(
                    area,
                    glyph,
                    momentX / area,
                    momentY / area,
                    new Rect(left, top, right - left + 1, bottom - top + 1));
        }
    }

    private sealed record Mark(double Area, double Glyph, double CentreX, double CentreY, Rect Box)
    {
        public double Diameter => 2 * Math.Sqrt(Area / Math.PI);
    }

    private sealed record ShapeStub(string Title) : IPosterCard
    {
        public string Initials => PosterInitials.From(Title);

        public string CaptionText { get; init; } = "2019";

        public bool HasCaption => CaptionText.Length > 0;

        public string KindKey { get; init; } = "CatalogKindMovie";

        public bool HasKind { get; init; } = true;

        public string MetaText { get; init; } = "2019 · 116 min · Ciencia ficción";

        public bool HasMeta => MetaText.Length > 0;

        public string StatusKey { get; init; } = "WatchStatusNotStarted";

        public string EpisodeCountText { get; init; } = string.Empty;

        public bool CountsEpisodes { get; init; }

        public bool HasKnownProgress { get; init; }

        public double CompletedFraction { get; init; }

        public bool IsWatched { get; init; }

        public bool IsAvailable { get; init; } = true;

        public string? PosterFile { get; init; }
    }
}
