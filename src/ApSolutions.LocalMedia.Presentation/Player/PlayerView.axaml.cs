// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ApSolutions.LocalMedia.Presentation.Player;

public sealed partial class PlayerView : UserControl
{
    public PlayerView()
    {
        InitializeComponent();

        // The player answers the keyboard itself: without focus, every shortcut of PLY-014 would
        // depend on whichever control happened to hold it.
        Focusable = true;
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
