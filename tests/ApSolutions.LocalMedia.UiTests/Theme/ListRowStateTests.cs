// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// A list row's states, and above all the one that carries the information: which row is selected.
/// </summary>
/// <remarks>
/// Third type of phase 2 by measured use — 17 direct uses, and the inventory counts 23 lists with
/// data behind them. A row is neither the button nor the checkbox again: it owns exactly one theme
/// resource (<c>ListBoxItemPadding</c>), it has no border of its own in any state, and what paints it
/// is the content presenter, from brushes the base theme shares across lists.
///
/// Measured on twelve control types before anything was written: of
/// <c>SystemControlHighlightListLowBrush</c>, <c>…ListMediumBrush</c> and
/// <c>…ListAccentLowBrush</c>, <b>only the list takes them</b> — not the combo box, not the menu, not
/// the tab control, not one of the ten focus types. So pointing them at tokens moves lists and
/// nothing else.
/// </remarks>
// The theme variant is one setting on one application, and these classes all change it. They are
// serialised so that a class reading a theme cannot be reading one another class just replaced.
[Collection("ThemeVariant")]
public sealed class ListRowStateTests
{
    private const double TextMinimum = 4.5;
    private const double NonTextMinimum = 3.0;

    public static TheoryData<string> Themes() =>
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_selected_row_can_be_told_apart_from_the_rest(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        var painted = scene.Read(":selected");

        // Measured before this existed: the selected row was Windows 10's blue at 40 % opacity, which
        // over the surface reads 1.73:1 in light, 1.76:1 in high contrast light and 2.24:1 in high
        // contrast dark. The text on it was perfectly legible — the defect was never the text, it was
        // knowing which row you were on. Either the fill or the border may carry it.
        var ratio = Math.Max(
            ThemeContrast.Ratio(ThemeContrast.Painted(painted.Fill, surface), surface),
            ThemeContrast.Ratio(ThemeContrast.Painted(painted.Border, surface), surface));
        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: the selected row stands out from the others by {ratio:F2}:1, counting its "
                + "fill and its border alike, so a list cannot say where you are.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_label_stays_readable_on_a_selected_row(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        var painted = scene.Read(":selected");
        var fill = ThemeContrast.Painted(painted.Fill, surface);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(painted.Foreground, fill), fill);

        // A row's label comes from the base theme's generic foreground, which no resource of ours can
        // reach for one state alone. That is the reason the selected fill stays a tint rather than
        // becoming the solid accent: a solid accent would need a label colour that cannot be given.
        Assert.True(
            ratio >= TextMinimum,
            $"{themeName}: the label on a selected row reads {ratio:F2}:1 against the fill under it.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Selecting_a_row_does_not_move_what_is_in_it(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);

        // Every row carries the same border thickness in every state, and only its colour changes.
        // A border that appears on selection would push the text of that one row sideways.
        var resting = scene.Read(state: null).Thickness;
        foreach (var state in new[] { ":pointerover", ":pressed", ":selected", ":disabled" })
        {
            Assert.Equal(resting, scene.Read(state).Thickness);
        }

        Assert.NotEqual(default, resting);
    }

    [AvaloniaFact]
    public void High_contrast_paints_a_row_differently_from_the_ordinary_themes()
    {
        // Measured before this existed: Light and HighContrastLight painted a row identically, down to
        // the byte, and so did Dark and HighContrastDark.
        foreach (var (ordinary, contrast) in new[]
        {
            ("Light", "HighContrastLight"),
            ("Dark", "HighContrastDark"),
        })
        {
            Painted a;
            using (var first = new Scene(Resolve(ordinary)))
            {
                a = first.Read(":selected");
            }

            Painted b;
            using (var second = new Scene(Resolve(contrast)))
            {
                b = second.Read(":selected");
            }

            Assert.True(
                Show(a) != Show(b),
                $"{contrast} paints a selected row exactly like {ordinary}: {Show(a)}.");
        }
    }

    private static string Show(Painted painted) =>
        $"fill={Describe(painted.Fill)} border={Describe(painted.Border)} fg={Describe(painted.Foreground)}";

    private static string Describe(IBrush? brush) => brush switch
    {
        null => "null",
        ISolidColorBrush solid => $"{solid.Color}@{solid.Opacity:0.##}",
        _ => brush.GetType().Name,
    };

    private static ThemeVariant Resolve(string name) => name switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        "HighContrastLight" => Presentation.Theme.AppThemeVariants.HighContrastLight,
        "HighContrastDark" => Presentation.Theme.AppThemeVariants.HighContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private readonly record struct Painted(
        IBrush? Fill,
        IBrush? Border,
        IBrush? Foreground,
        Avalonia.Thickness Thickness);

    /// <summary>One realised row in one theme, with the window it needs and the reset it owes.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly Window _window;
        private readonly ListBoxItem _row;

        public Scene(ThemeVariant theme)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            var list = new ListBox { ItemsSource = new[] { "One", "Two" }, Width = 200, Height = 90 };
            _window = new Window { Width = 320, Height = 200, Content = list };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
            _row = list.GetVisualDescendants().OfType<ListBoxItem>().First();
        }

        public Painted Read(string? state)
        {
            foreach (var candidate in new[] { ":pointerover", ":pressed", ":selected", ":disabled" })
            {
                ((IPseudoClasses)_row.Classes).Set(candidate, candidate == state);
            }

            Dispatcher.UIThread.RunJobs();

            // The presenter is what paints: the row's own Background is transparent in every state and
            // its BorderBrush is null, so reading the row itself measures nothing.
            var presenter = _row.GetVisualDescendants().OfType<ContentPresenter>().First();
            return new Painted(
                presenter.Background,
                presenter.BorderBrush,
                Avalonia.Controls.Documents.TextElement.GetForeground(presenter),
                presenter.BorderThickness);
        }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }
}
