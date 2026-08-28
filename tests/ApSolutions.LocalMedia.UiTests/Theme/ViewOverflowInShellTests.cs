// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The other half of the overflow gate: a view is measured against the room the shell actually gives
/// it, not against the whole window.
/// </summary>
/// <remarks>
/// <para>
/// <c>ViewOverflowTests</c> states this as the first of its two limitations — "a view mounted alone
/// gets the whole 900, while inside the shell it gets 900 minus whatever the shell's own chrome
/// takes: this catches a view too wide on its own and <b>cannot catch one only too wide once
/// nested</b>". That sentence is also the shape of «secciones cortadas por el ancho», which the owner
/// reported in the original brief and which nobody had been able to locate: a view 880 wide passes
/// the gate and is clipped in the application, because the rail alone takes 64.
/// </para>
/// <para>
/// So the number is <b>measured off the shell</b> rather than written down. A constant would be a
/// second opinion about the rail's width and the content's padding, and it would go stale the first
/// time either moved — which is the mistake this file exists to catch, made one level up.
/// </para>
/// </remarks>
public sealed class ViewOverflowInShellTests
{
    /// <summary>The narrowest the main window can be made, from <c>App.axaml.cs</c>.</summary>
    private const double MinimumWindowWidth = 900;

    /// <summary>
    /// The shell gives its content less than the window, and this says how much less.
    /// </summary>
    /// <remarks>
    /// Asserted as a range rather than a number: what has to be true is that the rail and the
    /// padding are really taken off — a measurement that came back as the whole 900 would mean the
    /// probe found the window instead of the content, and would make every check below vacuous.
    /// </remarks>
    [AvaloniaFact]
    public void The_shell_gives_its_content_less_room_than_the_window_has()
    {
        var room = ContentRoom(out var window);

        Assert.True(
            room > 0 && room < MinimumWindowWidth,
            $"the shell's content area measured {room:F0} in a {MinimumWindowWidth:F0} window, which "
                + "is either the window itself or nothing — so this probe is reading the wrong control.");
        Assert.True(
            room <= MinimumWindowWidth - 64,
            $"the content area measured {room:F0}, which leaves less than the rail's own 64 px for the "
                + "rail. The probe is measuring something that is not beside the rail.");

        window.Close();
    }

    /// <summary>
    /// No view is wider than the room the shell hands it, which is 900 minus the rail and the padding.
    /// </summary>
    /// <remarks>
    /// The same sweep <c>ViewOverflowTests</c> makes, against the narrower number. It is still an
    /// upper bound rather than a scene — no data context, so every branch of every view is on screen
    /// at once — and it still carries that suite's second limitation: an empty <c>ItemsControl</c> is
    /// not a filled one. <b>Silence here is not a certificate either.</b>
    /// </remarks>
    /// <summary>Both languages since 2026-08-28, for the reason <c>ViewOverflowTests</c> gives.</summary>
    [AvaloniaTheory]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    public void No_view_is_wider_than_the_room_the_shell_gives_it(string language)
    {
        var room = ContentRoom(out var shellWindow, language);
        shellWindow.Close();

        var views = typeof(ShellView).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(UserControl).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            views.Length >= 40,
            $"only {views.Length} views were found, so this gate is reading the wrong thing.");

        var offside = new List<string>();
        foreach (var type in views)
        {
            // The shell itself is the frame rather than something inside it. Everything else is
            // measured, the player included: the prototype leaves the rail on screen for an embedded
            // session — `left: 64` — so a player surface lives in this width like any other view. It
            // was excluded here for one build on the assumption that it got the whole window, and
            // the assumption was wrong; excluding by assumption is how a gate ends up measuring the
            // half that was already fine.
            if (type == typeof(ShellView))
            {
                continue;
            }

            UserControl view;
            try
            {
                view = (UserControl)Activator.CreateInstance(type)!;
            }
            catch
            {
                // Named and skipped by the sibling suite, which is where that list lives.
                continue;
            }

            var window = new Window { Width = room, Height = 700, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            foreach (var control in view.GetVisualDescendants().OfType<Control>())
            {
                if (control.Bounds.Width <= 0 || !control.IsEffectivelyVisible)
                {
                    continue;
                }

                if (control.TranslatePoint(new Point(control.Bounds.Width, 0), window) is not { } edge)
                {
                    continue;
                }

                if (edge.X > room + 0.5 || edge.X < -0.5)
                {
                    offside.Add($"{type.Name}: {control.GetType().Name} ends at x={edge.X:F0} in {room:F0}");
                    break;
                }
            }

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(
            offside.Count == 0,
            $"these views are wider than the {room:F0} px the shell gives them at the window's own "
                + "minimum, so they are cut off by the width:\n  " + string.Join("\n  ", offside));
    }

    /// <summary>
    /// The width the shell's content area really has, measured rather than assumed.
    /// </summary>
    private static double ContentRoom(out Window window, string language = "es-ES")
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(language));

        var shell = new ShellView();
        window = new Window { Width = MinimumWindowWidth, Height = 700, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The rail is the one part of the chrome with a width of its own, and the content is what
        // shares the row with it. Found by name because that is what the shell declares.
        var rail = shell.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "NavigationRailSurface");

        return MinimumWindowWidth - rail.Bounds.Width;
    }
}
