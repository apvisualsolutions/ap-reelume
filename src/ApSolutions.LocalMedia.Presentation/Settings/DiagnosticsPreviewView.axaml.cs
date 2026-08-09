using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Settings;

public sealed partial class DiagnosticsPreviewView : UserControl
{
    public DiagnosticsPreviewView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
