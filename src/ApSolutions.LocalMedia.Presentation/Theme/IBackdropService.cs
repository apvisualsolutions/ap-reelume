using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Theme;

public interface IBackdropService
{
    bool TryApply(Window window);
}
