// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ApSolutions.LocalMedia.Presentation.Catalog;

public sealed partial class PersonalActionsView : UserControl
{
    public PersonalActionsView()
    {
        InitializeComponent();
    }

    private void OnRatingClick(object? sender, RoutedEventArgs e)
    {
        _ = e;
        if (DataContext is PersonalActionsViewModel viewModel &&
            sender is Button { Tag: int rating } &&
            viewModel.SetRatingCommand.CanExecute(rating))
        {
            viewModel.SetRatingCommand.Execute(rating);
        }
    }
}
