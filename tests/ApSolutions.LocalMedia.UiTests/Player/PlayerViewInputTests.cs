// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The two gestures the player answers itself, and the property the shell fills from outside.
/// </summary>
/// <remarks>
/// <para>
/// <c>PlayerView.axaml.cs</c> measured <b>65 % of lines and 41 % of branches</b> on CI — the lowest
/// pair in the whole tree — and nothing said a word, because the coverage gate watched the files
/// already on its debt list and this one had reached the bar once and got worse. Two views mounted it
/// at all, neither gave it a data context, and so neither of its two handlers had ever run.
/// </para>
/// <para>
/// They are not decoration. The tunnelling key handler is the whole fix for «la barra espaciadora
/// pone pantalla completa» — a focused transport button was answering the space bar by activating
/// itself — and the double click is the gesture the owner reported missing on the same day. Both were
/// shipped on the strength of a manual look.
/// </para>
/// </remarks>
public sealed class PlayerViewInputTests
{
    /// <summary>Double clicking the picture asks for full screen, and says the click was spent.</summary>
    [AvaloniaFact]
    public void A_double_click_on_the_picture_asks_for_full_screen()
    {
        var (window, view, model) = Mount();
        var asked = new List<PlaybackMode>();
        model.ModeHandler = mode =>
        {
            asked.Add(mode);
            return Task.CompletedTask;
        };

        var args = DoubleClick(view);
        view.RaiseEvent(args);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([PlaybackMode.Fullscreen], asked);
        Assert.True(args.Handled, "the click reached the picture and was not marked as spent.");
        window.Close();
    }

    /// <summary>
    /// With no shell to change the mode, the double click is left for somebody else to spend.
    /// </summary>
    /// <remarks>
    /// The command refuses while <c>ModeHandler</c> is null — the bar travels to a window with no
    /// shell above it — and marking the event handled there would swallow a gesture this view did
    /// nothing about. The no-context arm is the same promise for a view the composition has not
    /// filled yet.
    /// </remarks>
    [AvaloniaFact]
    public void A_double_click_with_nothing_to_change_the_mode_is_left_alone()
    {
        var (window, view, _) = Mount();

        var withModel = DoubleClick(view);
        view.RaiseEvent(withModel);
        Dispatcher.UIThread.RunJobs();
        Assert.False(withModel.Handled);

        view.DataContext = null;
        var withoutModel = DoubleClick(view);
        view.RaiseEvent(withoutModel);
        Dispatcher.UIThread.RunJobs();
        Assert.False(withoutModel.Handled);

        window.Close();
    }

    /// <summary>
    /// A key the session claims is spent on the way down, before any focused button can answer it.
    /// </summary>
    /// <remarks>
    /// Tunnelling is the whole point: a button inside the transport takes focus the moment it is
    /// clicked, and a focused button answers the space bar by activating itself — which is why space
    /// repeated whichever mode button had last been used. The probe is which gesture the handler was
    /// given, because "the player heard it first" is exactly what has to be true.
    /// </remarks>
    [AvaloniaFact]
    public void A_key_the_session_claims_never_reaches_the_control_that_has_focus()
    {
        var (window, view, model) = Mount();
        var heard = new List<KeyGesture>();
        model.GestureHandler = gesture =>
        {
            heard.Add(gesture);
            return gesture.Key == Key.Space;
        };

        var claimed = Press(view, Key.Space);
        Assert.True(claimed.Handled, "the session claimed the space bar and the event was left unspent.");

        // And one it does not claim travels on, or the player would eat every key in the application.
        var ignored = Press(view, Key.Z);
        Assert.False(ignored.Handled);

        Assert.Equal([Key.Space, Key.Z], heard.Select(gesture => gesture.Key));
        window.Close();
    }

    /// <summary>
    /// A player with no session, and one whose session has no gestures installed, spend no keys.
    /// </summary>
    /// <remarks>
    /// The composition fills <c>GestureHandler</c>, so the unfilled state is real between the view
    /// being built and the session starting — and a view that swallowed keys in that window would
    /// look like a dead keyboard.
    /// </remarks>
    [AvaloniaFact]
    public void A_player_with_no_gestures_installed_spends_no_keys()
    {
        var (window, view, _) = Mount();

        Assert.False(Press(view, Key.Space).Handled);

        view.DataContext = null;
        Assert.False(Press(view, Key.Space).Handled);

        window.Close();
    }

    /// <summary>
    /// A double click with nothing behind it is refused at the door rather than on the first read.
    /// </summary>
    [AvaloniaFact]
    public void The_view_refuses_a_gesture_that_is_not_there()
    {
        var (window, view, _) = Mount();

        // Reached through the base class's own dispatch, which is where a null would arrive from.
        Assert.Throws<ArgumentNullException>(() => ((Control)view).RaiseEvent(null!));
        window.Close();
    }

    /// <summary>
    /// The summary the shell writes in is a property of the view, not something it reaches for.
    /// </summary>
    /// <remarks>
    /// It is filled from outside because the endpoint belongs to the audio model and this view's
    /// context is the session — and because this very control is handed to the mini window, where
    /// there is no shell above it to reach up to.
    /// </remarks>
    [AvaloniaFact]
    public void The_output_summary_is_written_from_outside_and_read_back()
    {
        var (window, view, _) = Mount();

        Assert.Null(view.OutputSummary);

        view.OutputSummary = "Altavoces (Realtek) · 5.1";
        Assert.Equal("Altavoces (Realtek) · 5.1", view.OutputSummary);
        Assert.Equal("Altavoces (Realtek) · 5.1", view.GetValue(PlayerView.OutputSummaryProperty));

        window.Close();
    }

    /// <summary>The player takes the keyboard the moment it is on screen.</summary>
    /// <remarks>
    /// Without it every shortcut would depend on whichever control happened to hold focus when the
    /// session started, which is the state the tunnelling handler above exists to survive.
    /// </remarks>
    [AvaloniaFact]
    public void The_player_takes_the_keyboard_when_it_reaches_the_screen()
    {
        var (window, view, _) = Mount();

        Assert.True(view.Focusable);
        Assert.True(view.IsFocused, "the player never took focus, so its shortcuts belong to whoever did.");

        window.Close();
    }

    private static KeyEventArgs Press(PlayerView view, Key key)
    {
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
        view.RaiseEvent(args);
        Dispatcher.UIThread.RunJobs();
        return args;
    }

    /// <summary>
    /// A double click aimed at the picture, raised as the routed event the view overrides for.
    /// </summary>
    /// <remarks>
    /// Raised rather than clicked twice with the headless mouse, and that is a choice about what is
    /// being measured: two clicks would be measuring Avalonia's own double-click interval, which
    /// belongs to Avalonia. What this view promises is «a double tap on the picture asks for full
    /// screen», and that is the event on its own.
    ///
    /// <para>
    /// The event is <c>InputElement.DoubleTappedEvent</c> and not <c>Gestures.DoubleTappedEvent</c>:
    /// <c>Gestures</c> is internal in 12.1.1, which the first attempt found by not compiling.
    /// </para>
    /// </remarks>
    private static TappedEventArgs DoubleClick(PlayerView view) =>
        new(
            InputElement.DoubleTappedEvent,
            new PointerEventArgs(
                InputElement.DoubleTappedEvent,
                view,
                new Avalonia.Input.Pointer(0, PointerType.Mouse, isPrimary: true),
                view,
                new Point(20, 20),
                timestamp: 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None));

    private static (Window Window, PlayerView View, PlayerViewModel Model) Mount()
    {
        var model = new PlayerViewModel(new IdleCoordinator());
        var view = new PlayerView { DataContext = model };
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view, model);
    }

    /// <summary>A session that never starts: nothing here asks the player to play anything.</summary>
    private sealed class IdleCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
