using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Settings;

public sealed partial class AppearanceSettingsView : UserControl
{
    public AppearanceSettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
