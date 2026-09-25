// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
    /// One click on the picture pauses a playing film and resumes a paused one (ENG-018).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every common player does this and this one did nothing: the owner's «el funcionamiento normal
    /// de estos no es como los de cualquier reproductor», 2026-09-13. A double click still goes to full
    /// screen, and its first click still pauses on the way — which is what the others do too.
    /// </para>
    /// <para>
    /// <b>Only a click on the picture.</b> The tap a button of the bar raises bubbles up to here as
    /// well, because a button does not spend it, and answering it would pause a film somebody only
    /// asked to skip.
    /// </para>
    /// </remarks>
    protected override void OnTapped(TappedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnTapped(e);
        if (IsOnThePicture(e.Source)
            && DataContext is PlayerViewModel player
            && player.TogglePlaybackCommand.CanExecute(null))
        {
            player.TogglePlaybackCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The wheel over the picture moves the volume one step up or down, as the arrow keys do.
    /// </summary>
    /// <remarks>
    /// A wheel with no vertical movement is left alone: a sideways scroll says nothing about
    /// loudness. One something else already spent — the gear's scroller — never reaches here, because
    /// Avalonia does not hand a class handler an event already marked handled; asking again was a
    /// branch CI measured nothing taking, on the run of 2026-09-25.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y == 0
            || !IsOnThePicture(e.Source)
            || DataContext is not PlayerViewModel { Transport: { } transport })
        {
            return;
        }

        var step = e.Delta.Y > 0
            ? TransportControlsViewModel.VolumeStepPercent
            : -TransportControlsViewModel.VolumeStepPercent;
        _ = transport.SetVolumeAsync(transport.VolumePercent + step);
        e.Handled = true;
    }

    /// <summary>The picture, the letterbox around it, or the view itself — and nothing drawn on top.</summary>
    private bool IsOnThePicture(object? source) =>
        source == this || source == Content || source == VideoSurface;

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
    /// <remarks>
    /// <b>Except Escape while a drop-down inside the player is open</b>, which is the drop-down's.
    /// Found by the walk on 2026-09-25, the day Escape learned to step back through the gear
    /// (ENG-018): heard here first, closing the list of subtitle families closed the whole gear
    /// around it. Asked of the open lists and not of the key's source, because in the assembled
    /// application the key comes from whatever holds focus, which the first version assumed was the
    /// list and was not.
    /// </remarks>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (e.Key == Key.Escape && this.GetVisualDescendants().OfType<ComboBox>().Any(list => list.IsDropDownOpen))
        {
            return;
        }

        if (DataContext is PlayerViewModel { GestureHandler: { } handler }
            && handler(new KeyGesture(e.Key, e.KeyModifiers)))
        {
            e.Handled = true;
        }
    }
}
