// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Where the dotted outline actually lands, view by view, with no data behind any of them.
/// </summary>
/// <remarks>
/// «Los contornos punteados salen donde no van», the owner reported on 2026-08-25, naming seven
/// screens and «algún elipse». The outline is drawn on a control that is disabled, so the question
/// is not about the outline: it is which controls are disabled, and whether each one deserves to be.
/// This is the instrument that answers it — it prints what it finds rather than judging, because
/// what is right for one of them is a decision and not a rule.
/// </remarks>
[Collection("ThemeVariant")]
public sealed class DisabledOutlineReachTests
{
    /// <summary>
    /// The ordinary themes draw no dotted outline at all, whatever is disabled behind it.
    /// </summary>
    /// <remarks>
    /// Measured before the change: 299 controls across the tree carried one with no data loaded,
    /// every one of them a command with nothing to act on. They are all genuinely disabled — the
    /// outline was never lying — and in light and dark the fill already says so. Seven screens' worth
    /// of dotted rectangles over a grey that said it first is what the owner reported.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_ordinary_themes_say_disabled_with_the_fill_and_draw_nothing_over_it(string themeName)
    {
        Assert.NotNull(Avalonia.Application.Current);
        Avalonia.Application.Current!.RequestedThemeVariant = themeName == "Light"
            ? Avalonia.Styling.ThemeVariant.Light
            : Avalonia.Styling.ThemeVariant.Dark;
        try
        {
            var button = new Button { Content = "Ok", Width = 120, Height = 36, IsEnabled = false };
            var window = new Window { Width = 400, Height = 200, Content = button };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(button.GetValue(DisabledOutline.IsShownProperty));
            window.Close();
        }
        finally
        {
            Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
        }
    }

    /// <summary>
    /// And where an outline is drawn, it is over something really disabled and never over a shape.
    /// </summary>
    [AvaloniaFact]
    public void Every_outline_is_drawn_over_something_that_is_really_disabled()
    {
        Assert.NotNull(Avalonia.Application.Current);
        Avalonia.Application.Current!.RequestedThemeVariant = AppThemeVariants.HighContrastDark;
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var views = typeof(ShellView).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(UserControl).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(views.Length >= 40, $"only {views.Length} views were found; this reads the wrong thing.");

        var outlined = new List<string>();
        foreach (var type in views)
        {
            UserControl view;
            try
            {
                view = (UserControl)Activator.CreateInstance(type)!;
            }
            catch (Exception)
            {
                continue;
            }

            var window = new Window { Width = 900, Height = 700, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            foreach (var control in view.GetVisualDescendants().OfType<Control>())
            {
                if (!control.GetValue(DisabledOutline.IsShownProperty) || control.TemplatedParent is not null)
                {
                    continue;
                }

                var name = Avalonia.Automation.AutomationProperties.GetName(control);
                outlined.Add($"{type.Name}#{name ?? control.Name ?? control.GetType().Name}");

                // The outline is only ever drawn over something disabled, and it is never drawn over
                // a shape: an ellipse or a path is decoration, and a dotted rectangle around one is
                // an outline with nothing behind it to explain.
                Assert.False(
                    control.IsEffectivelyEnabled,
                    $"{type.Name} draws the disabled outline over {name ?? control.GetType().Name}, "
                        + "which is not disabled.");
                Assert.False(
                    control is Shape,
                    $"{type.Name} draws the disabled outline over a {control.GetType().Name}, "
                        + "which is a shape rather than a control somebody can press.");
            }

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        // A floor rather than a total: what matters is that the instrument found controls at all,
        // because a query that found none would pass by measuring nothing.
        Assert.True(
            outlined.Count > 50,
            $"only {outlined.Count} outlined controls were found; this is reading the wrong thing.");
        Avalonia.Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
    }
}
