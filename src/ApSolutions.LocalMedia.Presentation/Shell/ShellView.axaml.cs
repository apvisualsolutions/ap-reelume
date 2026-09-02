// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Shell;

/// <summary>
/// The shell, and the one place that owns the window the mini player lives in.
/// <para>
/// Modes move the same stage rather than build a new one: the surface that is playing is handed to
/// the mini window and handed back, so the session, its position, and its tracks survive a mode
/// change. Building a second player for the mini window is how two sessions end up running at once.
/// </para>
/// </summary>
public sealed partial class ShellView : UserControl
{
    private MiniPlayerWindow? _miniWindow;
    private ShellViewModel? _viewModel;
    private PlayerWindowCoordinator _windowCoordinator = new();

    public ShellView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;

        // Tunnelling, and that is the whole reason these are added in code rather than declared in
        // the markup: a key pressed inside the player is handled there — that is what the shortcuts
        // are — and a bubbling handler would never see it. The chrome has to come back for the
        // gesture that was handled, not only for the ones nothing wanted.
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownAnywhere, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// The mouse moving anywhere brings the chrome back, and never takes it away.
    /// </summary>
    /// <remarks>
    /// Nothing is marked handled and nothing is swallowed: this watches the gesture on its way down
    /// and lets it carry on to whatever it was aimed at. Revealing when the chrome is already there
    /// costs a comparison — <c>RevealChrome</c> returns on the first line — which matters, because a
    /// pointer crossing a window raises this a few hundred times a second.
    /// </remarks>
    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewModel?.RevealChrome();
    }

    private void OnKeyDownAnywhere(object? sender, KeyEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewModel?.RevealChrome();
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        _viewModel = DataContext as ShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;

            // Rebuilt rather than told, because the store is the one thing a coordinator takes at
            // construction and the data context arrives after this view does. A shell handed no
            // store gets the same coordinator it had before 2026-08-28.
            _windowCoordinator = new PlayerWindowCoordinator(_viewModel.MiniPlayerPlacement);
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellViewModel.PlaybackMode) && _viewModel is not null)
        {
            ApplyPlaybackMode(_viewModel.PlaybackMode);
        }
    }

    private void ApplyPlaybackMode(PlaybackMode mode)
    {
        if (this.FindControl<Panel>("PlayerStage") is not { } stage
            || this.FindControl<ContentControl>("PlayerHost") is not { } host)
        {
            return;
        }

        // The small window carries a transport of its own — five buttons and 480 logical pixels —
        // and the picture it is handed brings the full bar with it, so both were on screen at once:
        // «se duplica la barra de reproducción», reported on 2026-08-25. Told here rather than from
        // the mode's setter, and the reason is measured: changing what is visible inside the stage
        // while the stage is between two windows asks the wrong layout manager to arrange it, which
        // is an exception six suites caught the moment it was tried.
        // The full-screen half is told here for the same two reasons, and it decides which of the two
        // arrow glyphs the transport's mode button draws: the prototype swaps `full` for `exitfull`
        // the moment the picture takes the screen, and this drew the entering arrows in both states.
        if (_viewModel?.Player is { } session)
        {
            session.Player.IsCompact = mode == PlaybackMode.Mini;
            session.Player.IsFullscreen = mode == PlaybackMode.Fullscreen;
        }

        // The shell's own window follows the mode, and until 2026-09-02 nothing did. Everything below
        // was about the mini player's separate window, and the coordinator's fullscreen geometry —
        // written, tested and reachable — was called by nobody with that mode. So pressing fullscreen
        // swapped an arrow glyph and left the window exactly as it was: «aun no funciona la pantalla
        // completa», reported that day. Registered and never fed, in the shape of a mode.
        //
        // The mini player is not included: it lives in a window of its own, made below, and telling
        // the shell's window to take its geometry would shrink the application behind it.
        if (mode != PlaybackMode.Mini && TopLevel.GetTopLevel(this) is Window shell)
        {
            // Where it was before fullscreen took the screen, so leaving it gives that back rather
            // than the coordinator's default embedded shape. Remembered only on the way in, because
            // on the way out the window is already the size of the screen.
            if (mode == PlaybackMode.Fullscreen && _windowCoordinator.Current != PlaybackMode.Fullscreen)
            {
                _windowCoordinator.Remember(
                    PlaybackMode.Embedded,
                    new PlayerWindowGeometry(
                        shell.Position.X / shell.RenderScaling,
                        shell.Position.Y / shell.RenderScaling,
                        shell.Width,
                        shell.Height),
                    ScreenOf(shell),
                    shell.RenderScaling);
            }

            _windowCoordinator.Apply(shell, mode, ScreenOf(shell), shell.RenderScaling);
        }

        if (mode == PlaybackMode.Mini)
        {
            // Built rather than reused, and the `??=` that was here first is gone on purpose: every
            // path out of this mode below closes the window and drops the field, and the view model
            // does not raise a change for a mode already in force. So the branch that reused one
            // could not be taken — a guard that guards nothing, which is this repository's
            // characteristic defect at one line long.
            var window = new MiniPlayerWindow();
            _miniWindow = window;

            // Where it was left is written when it closes, and not while it moves. A move drag
            // raises PositionChanged on every frame, and the placement goes to a file: saving per
            // frame would put a few hundred writes behind one gesture, for an answer that only ever
            // gets read at the next launch.
            window.Closing += OnMiniPlayerClosing;

            // The mini window's chrome is bound to the shell's own view model - the session, the
            // mode and the close all already live there, so a view model of its own would be a
            // second answer to questions that have one.
            window.DataContext = _viewModel;
            window.Host(stage);
            _windowCoordinator.Apply(window, PlaybackMode.Mini, ScreenOf(window), window.RenderScaling);
            window.Show();
            return;
        }

        // Back inside the shell: the stage returns to its host and the mini window closes rather than
        // lingering empty behind the main window.
        if (_miniWindow is { } mini)
        {
            mini.Release();

            // Closing raises the handler below, which is where the placement is written: doing it
            // there rather than here covers the other way this window ends, which is the application
            // shutting down with the mini player still on screen.
            mini.Close();
            mini.Closing -= OnMiniPlayerClosing;
            _miniWindow = null;
        }

        host.Content = stage;
    }

    private void OnMiniPlayerClosing(object? sender, WindowClosingEventArgs args)
    {
        _ = args;

        // Cast rather than tested: this handler is attached to exactly one window and detached from
        // it above, so a sender of any other type is not a case to fall through, it is a bug that
        // should say so.
        var mini = (MiniPlayerWindow)sender!;
        _windowCoordinator.Remember(
            PlaybackMode.Mini,
            mini.CurrentGeometry(),
            ScreenOf(mini),
            mini.RenderScaling);
    }

    /// <summary>
    /// The screen a window is on, or a plausible one when the platform names none.
    /// </summary>
    /// <remarks>
    /// One place for it, rather than the same fallback written at both call sites: the fallback is
    /// never taken on any platform this ships to, so writing it twice is two branches nothing can
    /// cover for one decision that has no second opinion.
    /// </remarks>
    private static PixelRect ScreenOf(Window window) =>
        window.Screens.Primary?.Bounds ?? new PixelRect(0, 0, 1920, 1080);
}
