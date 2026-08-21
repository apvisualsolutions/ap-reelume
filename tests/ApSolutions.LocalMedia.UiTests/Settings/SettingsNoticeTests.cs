// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The last two of §4's settings tranche: a notice that was a plain sentence, and a dump of text
/// somebody reads before deciding whether to share it.
/// </summary>
/// <remarks>
/// <para>
/// "There is no tray on this system" is not a fact about the file, it is <b>a choice that could not be
/// honoured</b> — the same thing the three audio notices say — so it takes the same surface, the same
/// border and the same glyph. Colour alone is never the signal here.
/// </para>
/// <para>
/// The diagnostics preview asked for no wrapping, so reading it needed sideways scrolling. It is the
/// one piece of text in this application that exists <b>to be read before a decision</b>: what would
/// leave the machine if the person pressed export. Text like that does not get to hide its right-hand
/// half.
/// </para>
/// <para>
/// <b>§4's 13 px is refused, with the tree's own rule.</b> The type scale is 28/20/14/12 and has no 13;
/// a scalar declared for one consumer is the defect this repository has already named twice, and
/// <c>FontSizeMono</c> was considered and rejected on exactly that ground. The mono block takes
/// <c>FontSizeBody</c>, which is a token the markup does not have to write as a number.
/// </para>
/// </remarks>
public sealed class SettingsNoticeTests
{
    /// <summary>
    /// A tray this system does not have is a warning, in the grammar the rest of the tree uses.
    /// </summary>
    /// <remarks>
    /// The glyph is asserted alongside the surface, because a notice told apart from a fact by colour
    /// alone is the one thing this redesign spends its whole grammar avoiding. And the panel is
    /// asserted to be a <c>Grid</c>: a glyph beside wrapping text in a horizontal <c>StackPanel</c> is
    /// offered infinite width and runs off the side, which this repository has now measured nine times.
    /// </remarks>
    [AvaloniaFact]
    public void A_tray_the_system_does_not_have_is_a_warning_and_not_a_sentence()
    {
        var (window, view) = Show(new LifecycleSettingsView());

        var notice = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "TrayUnavailableNotice");
        Assert.Equal(
            ThemeColour("WarningSurfaceBrush"),
            Assert.IsAssignableFrom<ISolidColorBrush>(notice.Background).Color);
        Assert.True(notice.BorderThickness.Top > 0, "the notice has no border, so its only signal is colour.");
        Assert.Contains(notice.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "⚠");

        var laidOutInAStack = notice.GetVisualDescendants()
            .OfType<StackPanel>()
            .Any(panel => panel.Orientation == Orientation.Horizontal
                && panel.GetVisualChildren().OfType<TextBlock>().Any(block => block.TextWrapping == TextWrapping.Wrap));
        Assert.False(laidOutInAStack, "wrapping text beside a glyph in a horizontal stack never wraps.");

        window.Close();
    }

    /// <summary>
    /// The diagnostics dump wraps, keeps its ceiling, and takes its size from the scale.
    /// </summary>
    [AvaloniaFact]
    public void The_diagnostics_preview_wraps_rather_than_asking_for_a_sideways_scroll()
    {
        var (window, view) = Show(new DiagnosticsPreviewView());

        var preview = Assert.Single(
            view.GetVisualDescendants().OfType<TextBox>(),
            box => box.Name == "DiagnosticsPreviewText");
        Assert.Equal(TextWrapping.Wrap, preview.TextWrapping);
        Assert.True(preview.MaxHeight is > 0 and < double.PositiveInfinity, "the dump has no ceiling.");
        Assert.Equal(Assert.IsType<double>(Resource("FontSizeBody")), preview.FontSize);
        Assert.True(
            preview.FontFamily.FamilyNames.Count > 1,
            $"the dump declares only [{string.Join(", ", preview.FontFamily.FamilyNames)}], "
                + "so a host without the first draws it in whatever it likes.");

        window.Close();
    }

    private static (Window Window, Control View) Show(Control view)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var window = new Window { Width = 900, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current!;
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme, so nothing can paint it.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
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
