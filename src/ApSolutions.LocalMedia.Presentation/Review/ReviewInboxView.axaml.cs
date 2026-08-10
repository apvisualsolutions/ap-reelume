// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Input;

namespace ApSolutions.LocalMedia.Presentation.Review;

public sealed partial class ReviewInboxView : UserControl
{
    public ReviewInboxView()
    {
        InitializeComponent();
    }

    private void OnReviewKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (!ReviewCandidates.IsKeyboardFocusWithin || ReviewCandidates.ItemCount == 0)
        {
            return;
        }

        if (e.Key == Key.Down)
        {
            ReviewCandidates.SelectedIndex = Math.Min(
                ReviewCandidates.SelectedIndex + 1,
                ReviewCandidates.ItemCount - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            ReviewCandidates.SelectedIndex = Math.Max(ReviewCandidates.SelectedIndex - 1, 0);
            e.Handled = true;
        }
    }
}
