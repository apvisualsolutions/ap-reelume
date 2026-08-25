// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is PlayerViewModel { GestureHandler: { } handler }
            && handler(new KeyGesture(e.Key, e.KeyModifiers)))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
