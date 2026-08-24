// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// A drop-down's states: the frame you see when it is closed, and the rows inside it when it is open.
/// </summary>
/// <remarks>
/// Phase 2f, eight uses. Measured before anything was written, and the drop-down inherits nothing
/// from the text field: <c>IsEditable</c> appears nowhere in the tree, so a closed combo box has no
/// <c>PART_BorderElement</c> at all. It has three families of its own — 59 keys, of which 23 are the
/// rows' — and the rows are the ones that carry information.
///
/// <para>
/// The measurement that decided the design: a <c>ComboBoxItem</c>'s content presenter <b>does</b>
/// take <c>BorderBrush</c> and <c>BorderThickness</c> by template binding, the same as a
/// <c>ListBoxItem</c>'s. Measured by setting fuchsia at 2 px on the item and reading the presenter:
/// <c>item th=2,2,2,2 bd=Fuchsia -> pres th=2,2,2,2 bd=Fuchsia</c>, in all four themes. So the cue
/// can be geometry from an application style, and does not need an adorner.
/// </para>
/// <para>
/// One thing the earlier note had backwards, worth writing down because it changes what is possible
/// here: a drop-down row's text <b>does</b> have eight resources of its own
/// (<c>ComboBoxItemForeground*</c>), unlike a list row's, which comes from a generic brush no state
/// can reach. A solid accent fill would therefore be paintable here. It is still a tint plus a
/// border, because a drop-down row and a list row are the same idea and reading them two different
/// ways is worse than either.
/// </para>
/// </remarks>
[Collection("ThemeVariant")]
public sealed class ComboBoxStateTests
{
    private const double TextMinimum = 4.5;
    private const double NonTextMinimum = 3.0;

    public static TheoryData<string> Themes() =>
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_selected_row_can_be_told_apart_from_the_rest(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ComboBoxDropDownBackground");

        var painted = scene.Row(":selected");

        // Measured before this existed: the drop-down's highlight was Windows 10's blue, opaque in
        // the colour and at 0.4 opacity on top in the light themes and 0.6 in the dark ones. Over the
        // drop-down's own surface that is 1.74:1 in light and 2.24:1 in high contrast dark, against a
        // bar of 3. Either the fill or the border may carry it.
        var ratio = Math.Max(
            ThemeContrast.Ratio(ThemeContrast.Painted(painted.Fill, surface), surface),
            ThemeContrast.Ratio(ThemeContrast.Painted(painted.Border, surface), surface));
        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: the selected row of a drop-down stands out by {ratio:F2}:1, counting its "
                + "fill and its border alike, so the list cannot say which one is chosen.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_label_stays_readable_on_a_selected_row(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ComboBoxDropDownBackground");

        var painted = scene.Row(":selected");
        var fill = ThemeContrast.Painted(painted.Fill, surface);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(painted.Foreground, fill), fill);

        Assert.True(
            ratio >= TextMinimum,
            $"{themeName}: the label on a selected drop-down row reads {ratio:F2}:1 against its fill.");
    }

    /// <summary>Every state's label, against the fill that state puts under it.</summary>
    /// <remarks>
    /// This exists because of what it caught. In the two high contrast themes hovering and pressing
    /// <b>invert</b> — the fill becomes the border's colour — and the label was still coming from
    /// the primary text token, so <c>HighContrastLight</c> painted a hovered row black on black:
    /// <c>bg=Black fg=Black</c>, 1:1. Nothing measured it, because the phase's other assertions all
    /// look at the selected row. A state nobody asserts about is a state nobody paints correctly.
    /// </remarks>
    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_label_stays_readable_in_every_state(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ComboBoxDropDownBackground");

        foreach (var state in new string?[] { null, ":pointerover", ":pressed", ":selected", ":disabled" })
        {
            var painted = scene.Row(state);
            var fill = ThemeContrast.Painted(painted.Fill, surface);
            var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(painted.Foreground, fill), fill);

            // A disabled row is measured against the non-text bar, which is the bar this repository
            // already gives disabled text in TextFieldStateTests: WCAG 1.4.3 exempts it, and that
            // exemption is exactly why it goes unmeasured everywhere and ends up illegible. Not
            // exempt here, just held to the bar that fits it. Measured: 4.17:1 in Dark, its worst.
            var bar = state == ":disabled" ? NonTextMinimum : TextMinimum;
            Assert.True(
                ratio >= bar,
                $"{themeName}: a {state ?? "resting"} drop-down row reads {ratio:F2}:1 against its "
                    + $"own fill, under a bar of {bar:F1}.");
        }
    }

    /// <summary>Each state's fill is the shared token for that state, and not the base theme's.</summary>
    /// <remarks>
    /// Identity rather than a contrast number, which is what <c>ControlStateTests</c> asks of a
    /// button for the same states — and asking a hover fill to clear 3:1 would be a bar no type in
    /// this application meets. Measured against the panel it sits on, a hovered row differs by
    /// 1.35:1 in light and 1.20:1 in dark; that is the shared fill token's number, not this type's,
    /// and it belongs to whichever phase revisits the fills rather than to this one.
    /// </remarks>
    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Every_state_takes_its_fill_from_the_shared_tokens(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);

        foreach (var (state, token) in new[]
        {
            (":pointerover", "ControlFillHoverBrush"),
            (":pressed", "ControlFillPressedBrush"),
            (":selected", "AccentSubtleBrush"),
        })
        {
            var fill = Assert.IsAssignableFrom<ISolidColorBrush>(scene.Row(state).Fill);
            Assert.Equal(ThemeContrast.Token(theme, token), fill.Color);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Choosing_a_row_does_not_move_what_is_in_it(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);

        // Same thickness in every state, colour the only thing that changes, for the same reason as
        // the list: a border that appears on selection shoves that one row's text sideways.
        var resting = scene.Row(state: null).Thickness;
        foreach (var state in new[] { ":pointerover", ":pressed", ":selected", ":disabled" })
        {
            Assert.Equal(resting, scene.Row(state).Thickness);
        }

        Assert.NotEqual(default, resting);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_closed_frame_is_a_shape_before_it_is_a_colour(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        var frame = scene.Frame();
        var fill = ThemeContrast.Painted(frame.Fill, surface);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(frame.Border, fill), fill);

        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: a closed drop-down is outlined at {ratio:F2}:1 against its own fill.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_open_drop_down_is_bounded_against_what_is_behind_it(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        // Measured before this existed: the drop-down's own border was `Black` at 0.14 opacity, which
        // over the surface behind it is a hairline nobody can see. An open drop-down floats over the
        // window and its edge is the only thing saying where it ends.
        var panel = scene.DropDown();
        var fill = ThemeContrast.Painted(panel.Fill, surface);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(panel.Border, fill), fill);

        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: an open drop-down is bounded at {ratio:F2}:1 against its own fill.");
    }

    /// <summary>A closed pill shows the row as its template draws it, not as the object prints itself.</summary>
    /// <remarks>
    /// Found in a capture, not by a gate: the season picker of a series read
    /// <c>ApSolutions.LocalMedia.Presentation.Show.SeasonViewModel</c> where it should read
    /// <c>Season 1</c>. The pill's own template binds <c>SelectionBoxItem</c> into a
    /// <c>ContentControl</c> and stopped there, so the presenter had a view model and no template and
    /// fell back to <c>ToString()</c>. The base theme's template binds <c>ItemTemplate</c> alongside
    /// the item, and this one had dropped that line. The two <c>ComboBox</c>es of the library never
    /// showed it because their rows are <c>ComboBoxItem</c>s whose content is already a string.
    /// </remarks>
    [AvaloniaFact]
    public void A_closed_pill_draws_its_selection_with_the_item_template()
    {
        var box = new ComboBox
        {
            Classes = { "filter-pill" },
            Width = 320,
            ItemsSource = new object[] { new Unprintable() },
            ItemTemplate = new FuncDataTemplate<Unprintable>(
                (_, _) => new TextBlock { Text = "Season 1" },
                supportsRecycling: true),
            SelectedIndex = 0,
        };
        var window = new Window { Width = 480, Height = 200, Content = box };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var presenter = box.GetVisualDescendants()
                .OfType<ContentControl>()
                .First(c => c.Name == "ContentPresenter");
            var drawn = presenter.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();

            Assert.Contains("Season 1", drawn);
            Assert.DoesNotContain(typeof(Unprintable).FullName, drawn);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A row that prints its own type name, which is what a view model without a template does.</summary>
    private sealed class Unprintable
    {
    }

    [AvaloniaFact]
    public void High_contrast_paints_a_drop_down_row_differently_from_the_ordinary_themes()
    {
        // The fifth type in a row where this had to be measured: Light and HighContrastLight painted
        // a drop-down row identically, down to the byte, and so did Dark and HighContrastDark.
        foreach (var (ordinary, contrast) in new[]
        {
            ("Light", "HighContrastLight"),
            ("Dark", "HighContrastDark"),
        })
        {
            Painted a;
            using (var first = new Scene(Resolve(ordinary)))
            {
                a = first.Row(":selected");
            }

            Painted b;
            using (var second = new Scene(Resolve(contrast)))
            {
                b = second.Row(":selected");
            }

            Assert.True(
                Show(a) != Show(b),
                $"{contrast} paints a selected drop-down row exactly like {ordinary}: {Show(a)}.");
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

    /// <summary>One open drop-down in one theme, with the window it needs and the reset it owes.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly Window _window;
        private readonly ComboBox _box;
        private readonly ComboBoxItem _row;

        public Scene(ThemeVariant theme)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            _box = new ComboBox { ItemsSource = new[] { "One", "Two" }, Width = 200 };
            _window = new Window { Width = 320, Height = 240, Content = _box };
            _window.Show();
            Dispatcher.UIThread.RunJobs();

            // The rows live in the popup, which is not a visual descendant of the box: they are
            // reached through the popup's child, and only once the drop-down is open.
            _box.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            var popup = _box.GetVisualDescendants().OfType<Popup>().First();
            _row = ((Visual)popup.Child!).GetVisualDescendants().OfType<ComboBoxItem>().First();
        }

        public Painted Row(string? state)
        {
            foreach (var candidate in new[] { ":pointerover", ":pressed", ":selected", ":disabled" })
            {
                ((IPseudoClasses)_row.Classes).Set(candidate, candidate == state);
            }

            Dispatcher.UIThread.RunJobs();

            // The presenter is what paints, as it is for a list row: the item's own Background is
            // transparent in every state and its BorderBrush is null.
            var presenter = _row.GetVisualDescendants().OfType<ContentPresenter>().First();
            return new Painted(
                presenter.Background,
                presenter.BorderBrush,
                Avalonia.Controls.Documents.TextElement.GetForeground(presenter),
                presenter.BorderThickness);
        }

        /// <summary>The closed box's own frame, which is the first Border in its template.</summary>
        public Painted Frame()
        {
            var border = _box.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Background");
            return new Painted(
                border.Background,
                border.BorderBrush,
                Avalonia.Controls.Documents.TextElement.GetForeground(border),
                border.BorderThickness);
        }

        /// <summary>The panel the open drop-down draws around its rows.</summary>
        public Painted DropDown()
        {
            var popup = _box.GetVisualDescendants().OfType<Popup>().First();
            var border = (Border)popup.Child!;
            return new Painted(
                border.Background,
                border.BorderBrush,
                Avalonia.Controls.Documents.TextElement.GetForeground(border),
                border.BorderThickness);
        }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }
}
