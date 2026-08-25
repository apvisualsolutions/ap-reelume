// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
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
/// Where the accent is spent when something is chosen, and where it is not.
/// </summary>
/// <remarks>
/// <para>
/// The prototype makes a distinction this tree had flattened. A <b>menu</b> says where you are and is
/// drawn with a neutral wash — <c>rgba(127,145,170,.16)</c> — with the accent spent on a 3 px bar
/// beside the current destination. A <b>drop-down</b> says which one you picked off a list and does
/// carry the accent, as a hairline over the subtle fill. Both were the same two-pixel accent
/// rectangle, drawn around every selected row in the application, a poster in a rail included.
/// </para>
/// <para>
/// «Quiero cambiar los recuadros de selección de los menús en general, no quiero un borde coloreado
/// con el acento», said on 2026-08-25, and «los botones de filtro de Biblioteca no son iguales a los
/// del prototipo» in the same breath. These are the two halves, measured on realised controls rather
/// than read out of the markup: what a style names and what a control ends up painted with are
/// different questions, and this file has been on the wrong side of that before.
/// </para>
/// </remarks>
[Collection("ThemeVariant")]
public sealed class SelectionSurfaceTests
{
    [AvaloniaFact]
    public void A_selected_list_row_is_not_painted_in_the_accent()
    {
        var list = new ListBox { ItemsSource = new[] { "One", "Two" }, Width = 200, Height = 90 };
        var window = new Window { Width = 320, Height = 200, Content = list };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var row = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            ((IPseudoClasses)row.Classes).Set(":selected", true);
            Dispatcher.UIThread.RunJobs();

            var presenter = row.GetVisualDescendants().OfType<ContentPresenter>().First();
            var accent = ThemeContrast.Token(ThemeVariant.Light, "AccentBrush");

            Assert.NotEqual(accent, Colour(presenter.Background));
            Assert.NotEqual(accent, Colour(presenter.BorderBrush));

            // And the wash it does carry is the prototype's own literal, so a menu row in this
            // application and a menu row in the reference are the same colour.
            Assert.Equal(
                ThemeContrast.Token(ThemeVariant.Light, "SelectionFillBrush"),
                Colour(presenter.Background));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// And the pill that has not been chosen, which is the half the library's filter row got wrong.
    /// </summary>
    /// <remarks>
    /// seg(false) is <c>border: 1px solid transparent</c> over the plain fill with the quiet ink.
    /// This carried the control border and the primary ink instead, so three options were drawn as
    /// three chosen ones with a little more blue on the live one. The thickness is asserted along
    /// with the colour: dropping the border rather than clearing it would move the label of whichever
    /// pill is live by a pixel every time somebody pressed another one.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_option_pill_says_which_of_its_two_states_it_is_in(bool chosen)
    {
        var pill = new Button { Content = "Películas", Classes = { "theme-option" } };
        if (chosen)
        {
            pill.Classes.Add("selected");
        }

        var window = new Window { Width = 320, Height = 200, Content = pill };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var presenter = pill.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .First(part => part.Name == "PART_ContentPresenter");
            var border = Colour(presenter.BorderBrush);
            var accent = ThemeContrast.Token(ThemeVariant.Light, "AccentBrush");

            Assert.Equal(new Thickness(1), pill.BorderThickness);
            if (chosen)
            {
                Assert.Equal(accent, border);
                Assert.Equal(
                    ThemeContrast.Token(ThemeVariant.Light, "AccentSubtleBrush"),
                    Colour(presenter.Background));
            }
            else
            {
                Assert.Equal(Colors.Transparent, border);
                Assert.Equal(
                    ThemeContrast.Token(ThemeVariant.Light, "ControlFillBrush"),
                    Colour(presenter.Background));
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The drop-down keeps its accent, and says so while it is open.
    /// </summary>
    /// <remarks>
    /// The caret turned over and nothing else did: the frame stayed exactly as it was drawn closed,
    /// so a panel could be open over a control that gave no sign of it. The prototype writes the
    /// other half — the subtle accent as fill and the accent as border — and this is that half.
    /// </remarks>
    [AvaloniaFact]
    public void An_open_drop_down_says_so_with_more_than_its_caret()
    {
        var picker = new ComboBox
        {
            Classes = { "filter-pill" },
            Width = 240,
            ItemsSource = new[] { "Título", "Año" },
            SelectedIndex = 0,
        };
        var window = new Window { Width = 400, Height = 240, Content = picker };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var frame = picker.GetVisualDescendants()
                .OfType<Border>()
                .First(part => part.Name == "Background");
            var closed = (Colour(frame.Background), Colour(frame.BorderBrush));

            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            var open = (Colour(frame.Background), Colour(frame.BorderBrush));

            Assert.NotEqual(closed, open);
            Assert.Equal(ThemeContrast.Token(ThemeVariant.Light, "AccentBrush"), open.Item2);
        }
        finally
        {
            picker.IsDropDownOpen = false;
            window.Close();
        }
    }

    private static Color Colour(IBrush? brush) =>
        brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
}
