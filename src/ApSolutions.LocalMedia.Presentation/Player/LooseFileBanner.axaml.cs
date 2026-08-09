using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Player;

public sealed partial class LooseFileBanner : UserControl
{
    public LooseFileBanner() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
