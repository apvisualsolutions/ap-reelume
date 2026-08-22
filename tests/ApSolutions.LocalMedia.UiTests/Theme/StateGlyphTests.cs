// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The three circles this application uses for state, at the size that makes them read like the
/// Fluent glyphs beside them on other screens.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks that <c>○ ◐ ●</c> stay and gain "the same optical size as the Fluent glyphs", and the
/// measurement says why. On 2026-08-22, at <c>FontSizeBody</c>: a Fluent glyph renders <b>14 wide by
/// 14 tall</b> — an icon font fills its em box — and <c>●</c> renders <b>9 wide</b>. The circle was
/// reading at <b>64%</b> of the icon's size, which is what makes it look like a stray character rather
/// than a state.
/// </para>
/// <para>
/// <c>FontSizeSubtitle</c> brings it to <b>13 wide</b>, or 93%, and it is a token that already exists.
/// Nothing between the two is in the scale, and a scalar declared for this alone would be the defect
/// this repository names — so the nearest existing step is what it gets, and the remaining 7% is a
/// difference nobody can see between two screens.
/// </para>
/// <para>
/// <b>Thirteen text blocks paint one of the three</b>, in three files: the watch status control, the
/// five destinations of the navigation rail, and the five appearance pills. They take one class, since
/// three of thirteen at one size and ten at another is the inconsistency this batch keeps finding.
/// </para>
/// </remarks>
public sealed class StateGlyphTests
{
    /// <summary>The three, and nothing else is one.</summary>
    private static readonly string[] Circles = ["○", "◐", "●"];

    /// <summary>
    /// Every block that paints a circle wears the class, and the class is the token.
    /// </summary>
    /// <remarks>
    /// The count is asserted first: the navigation rail and the appearance pills paint theirs through a
    /// binding, so with no data context they are empty strings — a test that only looked for the
    /// literal would find three of thirteen and call it done.
    /// </remarks>
    [AvaloniaFact]
    public void Every_state_circle_wears_one_class_and_that_class_is_the_token()
    {
        var expected = Assert.IsType<double>(Resource("FontSizeSubtitle"));
        using var scope = Mount();

        var marked = scope.Blocks.Where(block => block.Classes.Contains("state-glyph")).ToArray();
        Assert.True(
            marked.Length >= 13,
            $"only {marked.Length} blocks wear state-glyph, and thirteen paint one of the three circles.");
        Assert.All(marked, block => Assert.Equal(expected, block.FontSize));

        var literals = scope.Blocks
            .Where(block => Circles.Contains(block.Text, StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(literals);
        Assert.All(literals, block => Assert.Contains("state-glyph", block.Classes));
    }

    /// <summary>
    /// At that size a circle reads at the same scale as a Fluent glyph, and it is measured.
    /// </summary>
    /// <remarks>
    /// The band is wide on purpose — this is about two symbols on different screens looking like one
    /// family, not about pixels. What it refuses is the 64% the circles read at before, which is far
    /// outside it and is what the eye was catching.
    /// </remarks>
    [AvaloniaFact]
    public void A_circle_at_that_size_reads_at_the_scale_of_a_fluent_glyph()
    {
        var body = Assert.IsType<double>(Resource("FontSizeBody"));
        var glyphSize = Assert.IsType<double>(Resource("FontSizeSubtitle"));

        var panel = new StackPanel();
        var window = new Window { Width = 400, Height = 400, Content = panel };
        window.Show();

        var icon = Measured(panel, "", new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), body);
        var circle = Measured(panel, "●", FontFamily.Default, glyphSize);
        Dispatcher.UIThread.RunJobs();
        panel.Measure(new Size(400, double.PositiveInfinity));
        Dispatcher.UIThread.RunJobs();

        Assert.True(icon.Bounds.Width > 0, "the icon measured nothing, so this proves nothing.");
        var ratio = circle.Bounds.Width / icon.Bounds.Width;
        Assert.InRange(ratio, 0.85, 1.15);

        window.Close();
    }

    private static TextBlock Measured(StackPanel panel, string text, FontFamily family, double size)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = family,
            FontSize = size,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        };
        panel.Children.Add(block);
        return block;
    }

    /// <summary>
    /// The shell and the watch status control, which is where all thirteen live.
    /// </summary>
    /// <remarks>
    /// No data context: a binding that resolves to nothing leaves every <c>IsVisible</c> at its
    /// default, so every destination and every pill is on screen at once.
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
            var shell = new ShellView();
            var status = new WatchStatusControl();
            var appearance = new AppearanceSettingsView();
            _windows =
            [
                Open(shell, 1280),
                Open(status, 620),
                Open(appearance, 900),
            ];
            Blocks =
            [
                .. shell.GetVisualDescendants().OfType<TextBlock>(),
                .. status.GetVisualDescendants().OfType<TextBlock>(),
                .. appearance.GetVisualDescendants().OfType<TextBlock>(),
            ];
        }

        internal TextBlock[] Blocks { get; }

        public void Dispose()
        {
            foreach (var window in _windows)
            {
                window.Close();
            }
        }

        private static Window Open(Control view, double width)
        {
            var window = new Window { Width = width, Height = 900, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return window;
        }
    }

    private static object Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        Assert.NotNull(value);
        return value!;
    }
}
