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
