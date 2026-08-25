// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Review;

/// <summary>
/// The review tray. It carried an arrow-key handler that moved a list selection, which went with the
/// list on 2026-08-25: the three decisions live in the card now, so what a keyboard walks between is
/// buttons, and Tab already does that.
/// </summary>
public sealed partial class ReviewInboxView : UserControl
{
    public ReviewInboxView()
    {
        InitializeComponent();
    }
}
