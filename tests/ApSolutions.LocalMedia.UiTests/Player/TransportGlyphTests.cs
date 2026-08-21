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
/// The transport's eleven buttons carry a glyph, and carrying one does not change what they are.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for glyphs from <c>Segoe Fluent Icons</c>, and the reason is measured rather than
/// aesthetic: on 2026-08-19 five buttons carrying translated words folded the mini player's chrome
/// into <b>three rows inside 480x270</b>, and the walk's beside-point probe died with "is surrounded
/// by other command controls". The words do not fit in the window the mini player is allowed to be.
/// </para>
/// <para>
/// <b>Only <c>Content</c> moves.</b> <c>AutomationProperties.Name</c> keeps pointing at the resource
/// key, which is what the walk aims at and what a screen reader reads out, so the identity of the
/// control does not move at all — and that is asserted here rather than assumed, because rewriting
/// the key is the one edit that would silently rename eleven controls and break the ledger.
/// </para>
/// <para>
/// The glyph is asserted to <b>resolve in a family the markup itself declares</b>, and to resolve in
/// <em>none</em> of the text families. Without that second half the lookup would pass for any
/// codepoint at all: a font that answers every question answers none of them.
/// </para>
/// </remarks>
public sealed class TransportGlyphTests
{
    /// <summary>The three the transport owns: seek back, seek forward, silence.</summary>
    private static readonly (string Name, string Key, string Glyph)[] TransportOwn =
    [
        ("SkipBackwardButton", "TransportSkipBackward", "\uE72B"),
        ("SkipForwardButton", "TransportSkipForward", "\uE72A"),
        ("MuteButton", "TransportToggleMute", "\uE74F"),
    ];

    /// <summary>The large transport's three, which carry no name and are found by the key behind theirs.</summary>
    private static readonly (string Key, string Glyph)[] LargeTransport =
    [
        ("PlayerPlayAction", "\uE768"),
        ("PlayerPauseAction", "\uE769"),
        ("PlayerStopAction", "\uE71A"),
    ];

    /// <summary>The mini player's five, whose names and keys are the same string.</summary>
    private static readonly (string Name, string Key, string Glyph)[] MiniChrome =
    [
        ("MiniPlayerPlayPause", "MiniPlayerPlayPause", "\uE768"),
        ("MiniPlayerSkipBack", "MiniPlayerSkipBack", "\uE72B"),
        ("MiniPlayerSkipForward", "MiniPlayerSkipForward", "\uE72A"),
        ("MiniPlayerRestore", "MiniPlayerRestore", "\uE73F"),
        ("MiniPlayerClose", "MiniPlayerClose", "\uE8BB"),
    ];

    /// <summary>Families that must not answer, so that a family answering means something.</summary>
    private static readonly string[] TextFamilies = ["Segoe UI", "Arial"];

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

        foreach (var (name, key, glyph) in TransportOwn)
        {
            Check(ByName(scope.Transport, name), key, glyph);
        }

        foreach (var (key, glyph) in LargeTransport)
        {
            Check(ByKey(scope.Player, key), key, glyph);
        }

        foreach (var (name, key, glyph) in MiniChrome)
        {
            Check(ByName(scope.Mini, name), key, glyph);
        }

        static void Check(Button button, string key, string glyph)
        {
            var word = Resource(key);
            Assert.True(
                word.Length > 1 && word.Any(char.IsLetter),
                $"{key} does not resolve to a word, so this test cannot tell a name from a glyph.");
            Assert.Equal(glyph, button.Content as string);
            Assert.Equal(word, AutomationProperties.GetName(button));
        }
    }

    /// <summary>
    /// Every glyph the transport paints exists in a family the markup itself declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The families are read off the button rather than written down here, so this asks about the
    /// declaration and not about a copy of it. Two are declared on purpose: <c>Segoe Fluent Icons</c>
    /// ships with Windows 11, which is the only target, and <c>Segoe MDL2 Assets</c> is its
    /// predecessor and carries the same codepoints — so a host that has only the older one still
    /// draws a pictogram instead of a box.
    /// </para>
    /// <para>
    /// Glyph zero is <c>.notdef</c>, which is the box. Asking for presence without excluding zero
    /// would pass on the font that draws nothing.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_glyph_resolves_in_a_family_the_markup_declares_and_in_no_text_family()
    {
        using var scope = Mount();
        var button = ByName(scope.Mini, "MiniPlayerPlayPause");
        var declared = button.FontFamily.FamilyNames.ToArray();

        var glyphs = TransportOwn.Select(entry => entry.Glyph)
            .Concat(LargeTransport.Select(entry => entry.Glyph))
            .Concat(MiniChrome.Select(entry => entry.Glyph))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var glyph in glyphs)
        {
            var codepoint = (uint)char.ConvertToUtf32(glyph, 0);
            var drawn = declared.Where(family => GlyphIndex(family, codepoint) != 0).ToArray();
            Assert.True(
                drawn.Length >= 2,
                $"U+{codepoint:X4} is drawn by [{string.Join(", ", drawn)}] out of "
                    + $"[{string.Join(", ", declared)}], so there is no font behind the first one.");
            foreach (var family in TextFamilies)
            {
                Assert.Equal(0, GlyphIndex(family, codepoint));
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
