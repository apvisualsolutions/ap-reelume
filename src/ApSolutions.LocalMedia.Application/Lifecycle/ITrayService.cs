namespace ApSolutions.LocalMedia.Application.Lifecycle;

/// <summary>
/// The notification-area icon, as the rest of the application sees it. Showing it is always an
/// explicit act; nothing here polls, schedules, or wakes up on its own, so an enabled tray that
/// nobody clicks costs nothing.
/// </summary>
public interface ITrayService
{
    bool IsVisible { get; }

    /// <summary>Raised when the person asks for the window back.</summary>
    event EventHandler? OpenRequested;

    /// <summary>Raised when the person asks the application to end.</summary>
    event EventHandler? ExitRequested;

    /// <summary>Shows the icon. Calling it again while it is visible does nothing.</summary>
    void Show();

    /// <summary>Hides the icon. Calling it again while it is hidden does nothing.</summary>
    void Hide();
}
