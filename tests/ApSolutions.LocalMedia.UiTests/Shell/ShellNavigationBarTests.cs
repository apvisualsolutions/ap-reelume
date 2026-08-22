// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The shell's first row of §4: the open destination is told by two signals, and the title actions
/// wrap.
/// </summary>
/// <remarks>
/// <para>
/// «Two signals, one of which is not colour» is the rule the whole redesign is built on, and the
/// navigation already had one: the filled or hollow glyph. §4 asks for the second, a 3px bar in the
/// accent, and it is a <b>bar that exists or does not</b> rather than one that changes colour —
/// absent is not the same as dimmed, which is the distinction the package spends a section on.
/// </para>
/// <para>
/// The bar is asserted as present on exactly one destination, not merely present somewhere: five bars
/// at once is a navigation that says every screen is open, and it would pass a check that only looked
/// for one.
/// </para>
/// </remarks>
public sealed class ShellNavigationBarTests
{
    /// <summary>The five destinations, in the order the shell declares them.</summary>
    private static readonly string[] Destinations =
        ["NavigationHome", "NavigationLibrary", "NavigationReview", "NavigationBackups", "NavigationSettings"];

    [AvaloniaFact]
    public void Exactly_one_destination_shows_the_bar_that_says_it_is_open()
    {
        var (window, view) = Show();

        var marked = Destinations
            .Where(name => BarOf(view, name) is { IsVisible: true })
            .ToArray();

        Assert.Equal(["NavigationHome"], marked);
        window.Close();
    }

    /// <summary>
    /// The bar is three pixels of the accent, and both numbers come from the theme rather than from a
    /// copy kept here.
    /// </summary>
    [AvaloniaFact]
    public void The_bar_is_three_pixels_of_the_accent()
    {
        var (window, view) = Show();
        var bar = BarOf(view, "NavigationHome");

        Assert.True(bar is not null, "The open destination carries no bar at all.");
        Assert.Equal(3, bar!.Width);

        // The accent lives in the theme dictionaries, one per variant, so it is asked for with the
        // variant in force. TryFindResource on the application answers null for it — which is how
        // this assertion first failed, and it was the test that was wrong, not the bar.
        var application = Avalonia.Application.Current!;
        var accent = Assert.IsAssignableFrom<IBrush>(
            application.TryGetResource("AccentBrush", application.ActualThemeVariant, out var token)
                ? token
                : null);
        Assert.Equal(accent.ToString(), bar.Background?.ToString());
        window.Close();
    }

    /// <summary>
    /// «Añadir medios» sits at the foot of the rail, is named in the language in force, and is not a
    /// sixth destination.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things, and each is one the drawing could get wrong on its own. The name, because the
    /// rail traded words for pictograms and the only place the word survives is here and in the
    /// tooltip. Below the five, because the prototype puts it under the flexible gap and a DockPanel
    /// hands its bands out in declaration order — written after the destinations it would take the
    /// whole rail and push them off. And no <c>navigation-current-bar</c>, because it is an action:
    /// nothing about it is ever the open destination, so a bar would be saying something untrue.
    /// </para>
    /// <para>
    /// The vertical comparison is against the <b>last</b> destination rather than the first, which is
    /// the half that fails if the button is merely somewhere in the rail.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Add_media_is_named_sits_below_every_destination_and_is_not_a_destination()
    {
        var (window, view) = Show();

        var add = view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "AddMediaAction");
        var label = Avalonia.Application.Current!.TryFindResource("NavigationAddMedia", out var resolved)
            ? resolved as string
            : null;

        Assert.Equal("Añadir medios", label);
        Assert.Equal(label, Avalonia.Automation.AutomationProperties.GetName(add));
        Assert.Equal(label, ToolTip.GetTip(add));
        Assert.DoesNotContain(
            add.GetVisualDescendants().OfType<Border>(),
            border => border.Classes.Contains("navigation-current-bar"));
        Assert.Contains("navigation-action", add.Classes);
        Assert.DoesNotContain("navigation-destination", add.Classes);

        var lowest = Destinations
            .Select(name => DestinationButton(view, name))
            .Select(button => Assert.IsType<Button>(button))
            .Max(button => button.TranslatePoint(new Point(0, button.Bounds.Height), window)!.Value.Y);
        var top = add.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        Assert.True(
            top >= lowest,
            $"Add media starts at y={top}, above the foot of the last destination at y={lowest}.");

        // Same 46 px column as the five above it, so a rail of six reads as one column.
        Assert.Equal(DestinationButton(view, "NavigationHome")!.Bounds.Width, add.Bounds.Width);
        window.Close();
    }

    // The title actions' WrapPanel used to be asserted here. It moved to WrappingSurfaceTests, which
    // holds the closed table of every row of actions §4 has decided, so the rule has one mechanism
    // rather than one per view — two of them age differently, and the second one to be written is the
    // one nobody remembers to extend.

    /// <summary>The bar of one destination, by the accessible name its button is declared under.</summary>
    private static Border? BarOf(ShellView view, string key) =>

        // A class and not an x:Name: five destinations carry one each, and a name has to be unique
        // within the control's scope — the first attempt at this threw on the second destination.
        DestinationButton(view, key)?.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Classes.Contains("navigation-current-bar"));

    /// <summary>One destination's button, found the way a screen reader would name it.</summary>
    private static Button? DestinationButton(ShellView view, string key)
    {
        var label = Avalonia.Application.Current!.TryFindResource(key, out var resolved)
            ? resolved as string
            : null;

        return view.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(candidate =>
                string.Equals(Avalonia.Automation.AutomationProperties.GetName(candidate), label, StringComparison.Ordinal));
    }

    private static (Window Window, ShellView View) Show()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var view = new ShellView { DataContext = new ShellViewModel(new NavigationService(), new ShellSurfaces()) };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }
}
