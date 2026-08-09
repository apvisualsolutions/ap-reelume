using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The always-on-top mini player. It hosts the same player surface the embedded view uses; the
/// coordinator moves that control here rather than creating a second one.
/// </summary>
public sealed partial class MiniPlayerWindow : Window
{
    public MiniPlayerWindow()
    {
        InitializeComponent();
    }

    /// <summary>Places the shared player surface inside this window.</summary>
    public void Host(Control surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var panel = this.FindControl<Panel>("MiniPlayerSurface");
        if (panel is null)
        {
            return;
        }

        if (surface.Parent is Panel previous)
        {
            _ = previous.Children.Remove(surface);
        }

        panel.Children.Clear();
        panel.Children.Add(surface);
    }
}
