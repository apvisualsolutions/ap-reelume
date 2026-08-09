using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private async void OnCatalogItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = e;
        if (DataContext is LibraryViewModel viewModel &&
            sender is Button { DataContext: CatalogItemViewModel item })
        {
            await viewModel.OpenDetailsAsync(item).ConfigureAwait(true);
        }
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (DataContext is LibraryViewModel viewModel)
        {
            viewModel.BackToLibrary();
        }
    }
}
