// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ApSolutions.LocalMedia.Presentation.Show;

public sealed partial class EpisodeRowView : UserControl
{
    public EpisodeRowView()
    {
        InitializeComponent();
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (DataContext is not EpisodeRowViewModel episode)
        {
            return;
        }

        var details = this.FindAncestorOfType<ShowDetailsView>()?.DataContext as ShowDetailsViewModel;
        if (details?.PlayEpisodeCommand.CanExecute(episode) is true)
        {
            details.PlayEpisodeCommand.Execute(episode);
        }
    }
}
