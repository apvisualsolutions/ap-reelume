// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The two gestures that bring the chrome back while a film is playing, watched on their way down.
/// </summary>
/// <remarks>
/// They are added in code rather than declared in the markup because they tunnel: a key pressed
/// inside the player is handled there, and a bubbling handler would never see it. Both branches
/// matter — a shell with a view model and one without — because the shell exists before its context
/// does, and a gesture arriving in that window must not take the application down with it. The
/// coverage ratchet is what asked for this: the file's branches fell from 70 to 66 in the run that
/// added the two handlers.
/// </remarks>
public sealed class ShellChromeGestureTests
{
    [AvaloniaFact]
    public void A_shell_with_no_context_yet_takes_both_gestures_without_a_word()
    {
        var view = new ShellView();
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Press(view, Key.M);
        Move(view);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(view.DataContext);
        window.Close();
    }

    [AvaloniaFact]
    public void Either_gesture_brings_the_chrome_back_and_neither_takes_it_away()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());
        var view = new ShellView { DataContext = shell };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        shell.HideChrome();
        Assert.False(shell.IsChromeRevealed);
        Press(view, Key.M);
        Assert.True(shell.IsChromeRevealed);

        shell.HideChrome();
        Move(view);
        Assert.True(shell.IsChromeRevealed);

        // Revealing what is already there is the ordinary case — a pointer crossing a window raises
        // this a few hundred times a second — and it has to be the cheap one.
        Move(view);
        Assert.True(shell.IsChromeRevealed);
        window.Close();
    }

    /// <summary>A context that is replaced stops driving the shell, and the new one starts.</summary>
    [AvaloniaFact]
    public void The_gestures_follow_the_context_the_shell_is_given_now()
    {
        var first = new ShellViewModel(new NavigationService(), new ShellSurfaces());
        var second = new ShellViewModel(new NavigationService(), new ShellSurfaces());
        var view = new ShellView { DataContext = first };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.DataContext = second;
        first.HideChrome();
        second.HideChrome();
        Press(view, Key.M);

        Assert.True(second.IsChromeRevealed);
        Assert.False(first.IsChromeRevealed);
        window.Close();
    }

    /// <summary>
    /// The gesture is raised on the shell itself, which is where the tunnelling handler is added.
    /// </summary>
    /// <remarks>
    /// Not through the headless window's own key press: that one is delivered to whatever holds
    /// focus, and a shell with nothing open holds none — so it would measure focus rather than the
    /// handler. What the handler promises is «this event, on its way down, anywhere», and that is
    /// what is raised.
    /// </remarks>
    private static void Press(ShellView view, Key key)
    {
        view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Move(ShellView view)
    {
        view.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent,
            view,
            new Pointer(0, PointerType.Mouse, isPrimary: true),
            view,
            new Point(10, 10),
            timestamp: 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }
}
