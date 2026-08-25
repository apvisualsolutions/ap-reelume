// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The transport's eleven buttons carry a picture, and carrying one does not change what they are.
/// </summary>
/// <remarks>
/// <para>
/// The pictures are drawings and no longer glyphs, and that changed on 2026-08-24 <b>against a line
/// of the design package</b>, which is worth writing down rather than hiding. The Proposal and the
/// package's README both prescribe <c>Segoe Fluent Icons</c> — "los iconos son glifos de Segoe
/// Fluent Icons", chosen because the font ships with Windows and costs no download. The prototype
/// itself does something else: every pictogram in it is an SVG of 24 by 24 with
/// <c>stroke-width:1.6</c> and round caps, a line drawing. The owner looked at the built application
/// and said the player's icons are not the prototype's, and he is right: a solid Fluent glyph and a
/// thin stroked drawing are two different alphabets.
/// </para>
/// <para>
/// So the shapes now come from the prototype, converted into geometries that live in the repository
/// — which keeps the package's actual rule, the one about downloads and CDNs, intact. What the font
/// gave and this had to keep giving is one place to re-weight the whole set, and that is what the
/// <c>Path.icon</c> styles are.
/// </para>
/// <para>
/// <b>Only the picture moves.</b> <c>AutomationProperties.Name</c> keeps pointing at the resource
/// key, which is what the walk aims at and what a screen reader reads out, so the identity of the
/// control does not move at all — and that is asserted here rather than assumed, because rewriting
/// the key is the one edit that would silently rename eleven controls and break the ledger.
/// </para>
/// <para>
/// The drawing is asserted to be <b>the geometry the dictionary holds under that name</b>, and not
/// merely to be present: a Path with no data is a button with nothing in it, and it would satisfy
/// any check that only asked whether an icon was there.
/// </para>
/// </remarks>
public sealed class TransportGlyphTests
{
    /// <summary>The three the transport owns: seek back, seek forward, silence.</summary>
    private static readonly (string Name, string Key, string Icon)[] TransportOwn =
    [
        ("SkipBackwardButton", "TransportSkipBackward", "IconSkipBackward"),
        ("SkipForwardButton", "TransportSkipForward", "IconSkipForward"),
        ("MuteButton", "TransportToggleMute", "IconVolume"),
    ];

    /// <summary>The large transport's three, which carry no name and are found by the key behind theirs.</summary>
    /// <summary>
    /// The session's own three, which moved into the transport row on 2026-08-25: the prototype puts
    /// back, play and forward together, and they were on a second line because their commands belong
    /// to a different model. They are still told apart by their accessible name rather than by an
    /// x:Name — they are declared inside a template and have none.
    /// </summary>
    private static readonly (string Key, string Icon)[] LargeTransport =
    [
        ("PlayerPlayAction", "IconPlay"),
        ("PlayerPauseAction", "IconPause"),
        ("PlayerStopAction", "IconStop"),
    ];

    /// <summary>The mini player's five, whose names and keys are the same string.</summary>
    private static readonly (string Name, string Key, string Icon)[] MiniChrome =
    [
        ("MiniPlayerPlayPause", "MiniPlayerPlayPause", "IconPlay"),
        ("MiniPlayerSkipBack", "MiniPlayerSkipBack", "IconSkipBackward"),
        ("MiniPlayerSkipForward", "MiniPlayerSkipForward", "IconSkipForward"),
        ("MiniPlayerRestore", "MiniPlayerRestore", "IconExitFullscreen"),
        ("MiniPlayerClose", "MiniPlayerClose", "IconClose"),
    ];

    /// <summary>
    /// Each of the eleven paints its glyph and still answers to its own name.
    /// </summary>
    /// <remarks>
    /// The name is asserted to be <b>a word</b> and not merely to be present: a name that had become
    /// the glyph too would satisfy "has a name" while leaving a screen reader announcing a private-use
    /// codepoint, which is the exact failure this change has to avoid.
    /// </remarks>
    [AvaloniaFact]
    public void Each_transport_button_paints_a_glyph_and_keeps_the_name_it_had()
    {
        using var scope = Mount();

        foreach (var (name, key, icon) in TransportOwn)
        {
            Check(ByName(scope.Transport, name), key, icon);
        }

        foreach (var (key, icon) in LargeTransport)
        {
            Check(ByKey(scope.Transport, key), key, icon);
        }

        foreach (var (name, key, icon) in MiniChrome)
        {
            Check(ByName(scope.Mini, name), key, icon);
        }

        static void Check(Button button, string key, string icon)
        {
            var word = Resource(key);
            Assert.True(
                word.Length > 1 && word.Any(char.IsLetter),
                $"{key} does not resolve to a word, so this test cannot tell a name from a picture.");
            Assert.Equal(word, AutomationProperties.GetName(button));

            // Asked of the button and not of the application: a control resolves a resource by
            // walking up to the styles the application loaded, which is the same walk the
            // DynamicResource in the markup makes. Application.FindResource does not make it —
            // measured, it answers UnsetValue for a key the very same view is drawing — because
            // ApplyLanguage replaces Application.Resources wholesale on every language switch.
            Assert.True(
                button.TryFindResource(icon, out var expected),
                $"{icon} does not resolve from the control that draws it.");
            var drawn = button.GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Path>()
                .Select(path => path.Data)
                .ToArray();
            Assert.Contains(expected, drawn);
        }
    }

    /// <summary>Every picture the transport paints is a drawing with something in it.</summary>
    /// <remarks>
    /// <para>
    /// What this replaced asked of a font: that each codepoint resolved in one of the two families
    /// the markup declared and in neither text family, because a font that answers every question
    /// answers none of them. The same question, now that the pictures are geometries, is whether the
    /// drawing has any figures at all — a <c>Path</c> whose <c>Data</c> is an empty geometry paints
    /// nothing, takes its size from the style anyway, and would satisfy any check that only asked
    /// whether an icon was in the button.
    /// </para>
    /// <para>
    /// And the weight, because that is the other half of what a font gave: the prototype strokes
    /// every icon at 1.6 in a box of 24, so a picture drawn at 20 has to be stroked at 1.33 or it
    /// reads heavier than the same shape beside it at 22. Avalonia scales the geometry and not the
    /// pen, which is why each size class carries its own number rather than inheriting one.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_picture_the_transport_paints_is_a_drawing_and_is_stroked_for_its_size()
    {
        using var scope = Mount();

        foreach (var button in TransportOwn.Select(entry => ByName(scope.Transport, entry.Name))
            .Concat(LargeTransport.Select(entry => ByKey(scope.Transport, entry.Key)))
            .Concat(MiniChrome.Select(entry => ByName(scope.Mini, entry.Name))))
        {
            foreach (var path in button.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>())
            {
                Assert.NotNull(path.Data);
                Assert.False(
                    path.Data!.Bounds.Width == 0 && path.Data.Bounds.Height == 0,
                    $"{AutomationProperties.GetName(button)} carries a geometry that draws nothing.");

                var expected = Math.Round(1.6 * path.Width / 24d, 2);
                Assert.True(
                    Math.Abs(path.StrokeThickness - expected) <= 0.02 || path.Stroke is null,
                    $"{AutomationProperties.GetName(button)} draws a {path.Width} px icon stroked at "
                        + $"{path.StrokeThickness}, where the prototype's 1.6 in 24 gives {expected}.");
            }
        }
    }

    /// <summary>
    /// The transport's own three take the target area §4 asked for on their own row.
    /// </summary>
    /// <remarks>
    /// Measured on 2026-08-21: these three sat at <c>MinWidth 0</c> and <c>MinHeight 36</c> and wore
    /// no class at all. The 36 to 44 rise of 2026-08-21 landed on <c>player-chrome</c>, and this is
    /// the one view §4 names by name that <b>was not wearing it</b> — so the change was recorded as
    /// done while the three buttons somebody presses to skip and to silence stayed at 36. A glyph
    /// needs a square target more than a word does, which is why the two arrive together.
    /// </remarks>
    [AvaloniaFact]
    public void The_transports_own_three_wear_the_chrome_and_measure_the_target_area()
    {
        using var scope = Mount();

        foreach (var (name, _, _) in TransportOwn)
        {
            var button = ByName(scope.Transport, name);
            Assert.Contains("player-chrome", button.Classes);
            Assert.True(
                button.MinWidth >= 44 && button.MinHeight >= 44,
                $"{name} measures {button.MinWidth}x{button.MinHeight}, under the 44 §4 asks of this view.");
        }
    }

    /// <summary>
    /// The five fit on one line at the narrowest width the mini player is allowed to be.
    /// </summary>
    /// <remarks>
    /// This is the defect the glyphs were for, asserted rather than described. With translated words
    /// the chrome folded into <b>three rows inside 480x270</b> on 2026-08-19; the window's own minimum
    /// is 320, which is narrower still, so measuring there says the words could never have fitted and
    /// the glyphs always will. The panel keeps wrapping — this asserts that it does not need to.
    /// </remarks>
    [AvaloniaFact]
    public void The_five_of_the_chrome_share_one_line_at_the_windows_own_minimum()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var chrome = new MiniPlayerChromeView();
        var window = new Window { Width = 320, Height = 270, Content = chrome };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = MiniChrome
            .Select(entry => ByName(chrome, entry.Name))
            .Select(button => Math.Round(button.Bounds.Y, 1))
            .Distinct()
            .ToArray();

        Assert.Single(rows);
        window.Close();
    }

    private static int GlyphIndex(string family, uint codepoint)
    {
        var typeface = new Typeface(new FontFamily(family));
        return FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface)
            ? glyphTypeface.CharacterToGlyphMap[(int)codepoint]
            : 0;
    }

    private static Button ByName(Control root, string name) =>
        Assert.Single(root.GetVisualDescendants().OfType<Button>(), button => button.Name == name);

    private static Button ByKey(Control root, string key)
    {
        var word = Resource(key);
        return Assert.Single(
            root.GetVisualDescendants().OfType<Button>(),
            button => string.Equals(AutomationProperties.GetName(button), word, StringComparison.Ordinal));
    }

    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }

    /// <summary>
    /// The three views mounted at once, each in a window of its own.
    /// </summary>
    /// <remarks>
    /// None gets a data context, which leaves every <c>IsVisible</c> at its default and so puts play
    /// and pause on screen together — they alternate by state, and a run that bound one would never
    /// see the other.
    /// </remarks>
    private static Scope Mount()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private readonly Window[] _windows;

        internal Scope()
        {
            Transport = new TransportControlsView();
            Player = new PlayerView();
            Mini = new MiniPlayerChromeView();
            _windows =
            [
                Open(Transport, 320),
                Open(Player, 900),
                Open(Mini, 480),
            ];
        }

        internal TransportControlsView Transport { get; }

        internal PlayerView Player { get; }

        internal MiniPlayerChromeView Mini { get; }

        public void Dispose()
        {
            foreach (var window in _windows)
            {
                window.Close();
            }
        }

        private static Window Open(Control view, double width)
        {
            var window = new Window { Width = width, Height = 800, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return window;
        }
    }
}
