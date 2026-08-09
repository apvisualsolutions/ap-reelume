using ApSolutions.LocalMedia.Application.Lifecycle;
using Avalonia.Controls;
using Avalonia.Platform;

namespace ApSolutions.LocalMedia.Windows.Tray;

/// <summary>
/// The notification-area icon on Windows, built on Avalonia's tray support.
/// <para>
/// The icon is created once and only becomes visible when the person asks for it. It runs no timer,
/// opens no handle beyond the icon itself, and raises an event only when it is clicked, so an
/// enabled tray that is left alone does no work at all.
/// </para>
/// </summary>
public sealed class WindowsTrayService : ITrayService, IDisposable
{
    private const string IconUri = "avares://ApSolutions.LocalMedia.Presentation/Assets/tray-icon.png";

    private readonly TrayIcon _icon;
    private bool _isDisposed;

    public WindowsTrayService(string toolTipText, string openLabel, string exitLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolTipText);
        ArgumentException.ThrowIfNullOrWhiteSpace(openLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(exitLabel);

        // The entries carry commands rather than click handlers so the menu can be inspected and
        // exercised without a desktop shell to click it with.
        var open = new NativeMenuItem(openLabel)
        {
            Command = new TrayCommand(() => OpenRequested?.Invoke(this, EventArgs.Empty)),
        };
        var exit = new NativeMenuItem(exitLabel)
        {
            Command = new TrayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty)),
        };

        _icon = new TrayIcon
        {
            ToolTipText = toolTipText,
            IsVisible = false,
            Menu = [open, exit],
        };
        _icon.Clicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        if (AssetLoader.Exists(new Uri(IconUri)))
        {
            using var stream = AssetLoader.Open(new Uri(IconUri));
            _icon.Icon = new WindowIcon(stream);
        }
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public bool IsVisible => _icon.IsVisible;

    /// <summary>The two entries the icon offers, exposed so they can be read and exercised.</summary>
    public NativeMenu Menu => _icon.Menu ?? throw new InvalidOperationException("The tray has no menu.");

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _icon.IsVisible = true;
    }

    public void Hide()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _icon.IsVisible = false;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _icon.IsVisible = false;
        _icon.Dispose();
    }

    private sealed class TrayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
