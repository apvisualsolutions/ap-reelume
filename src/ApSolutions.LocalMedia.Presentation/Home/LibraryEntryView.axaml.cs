using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Home;

public sealed partial class LibraryEntryView : UserControl
{
    public LibraryEntryView()
    {
        InitializeComponent();
    }

    /// <summary>The library shortcut, which takes the first focus when nothing can be resumed.</summary>
    public Button PrimaryAction => LibraryEntryAction;
}
