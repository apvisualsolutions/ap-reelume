// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ApSolutions.LocalMedia.Presentation.Player;

public sealed partial class PlayerView : UserControl
{
    /// <summary>
    /// Where the sound is going, written at the right of the transport the way the prototype does.
    /// </summary>
    /// <remarks>
    /// A property the shell fills rather than a binding this view makes, because the endpoint belongs
    /// to <c>AudioOutputViewModel</c> and this view's context is the session. Reaching across for it
    /// would make the player depend on the shell that hosts it, and this same control is handed to
    /// the mini window, where there is no shell above it to reach.
    /// </remarks>
    public static readonly StyledProperty<string?> OutputSummaryProperty =
        AvaloniaProperty.Register<PlayerView, string?>(nameof(OutputSummary));

    public PlayerView()
    {
        InitializeComponent();

        // The player answers the keyboard itself: without focus, every shortcut of PLY-014 would
        // depend on whichever control happened to hold it.
        Focusable = true;
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    public string? OutputSummary
    {
        get => GetValue(OutputSummaryProperty);
        set => SetValue(OutputSummaryProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _ = Focus();
    }

    /// <summary>
    /// A double click on the picture puts it on the whole screen, and takes it back off.
    /// </summary>
    /// <remarks>
    /// It is the gesture every player has and this one did not: the owner reported «el doble clic no
    /// pone pantalla completa» on 2026-08-25, and there was nothing listening for it. The transport
    /// bar sits above the picture and handles its own clicks, so a double click that reaches here is
    /// one aimed at the picture.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// The command is asked and the handler behind it is not, which is one test rather than two:
    /// <c>ToggleFullscreenCommand</c>'s own <c>CanExecute</c> <em>is</em> «ModeHandler is not null»,
    /// so a <c>{ ModeHandler: not null }</c> pattern beside it was the same question asked twice —
    /// and the second answer can never differ from the first, which the coverage gate reads as a
    /// branch nothing can take.
    /// </para>
    /// </remarks>
    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnDoubleTapped(e);
        if (DataContext is PlayerViewModel player && player.ToggleFullscreenCommand.CanExecute(null))
        {
            player.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The keyboard reaches the session's own shortcuts before anything else can spend the key.
    /// </summary>
    /// <remarks>
    /// Tunnelling, and that is the whole fix for what the owner reported: «la barra espaciadora pone
    /// pantalla completa» and «el atajo F no funciona». Neither is about the map — space has always
    /// been play/pause there and F has always been full screen — it is about who hears the key
    /// first. A button inside the transport bar takes focus the moment it is clicked, and a focused
    /// button answers the space bar by activating itself; the last button clicked was whichever mode
    /// button somebody had just used, so space repeated it. Handling on the way down means the
    /// session answers first and no focused button ever sees a key that belongs to the player.
    /// </remarks>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (DataContext is PlayerViewModel { GestureHandler: { } handler }
            && handler(new KeyGesture(e.Key, e.KeyModifiers)))
        {
            e.Handled = true;
        }
    }
}
