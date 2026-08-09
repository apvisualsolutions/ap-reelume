using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.About;

/// <summary>
/// The attribution the TMDB licence requires, and the licence this application is published under.
/// Both are conditions rather than decoration, so they live on a surface a person can actually open.
/// </summary>
public sealed partial class CreditsView : UserControl
{
    public CreditsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
