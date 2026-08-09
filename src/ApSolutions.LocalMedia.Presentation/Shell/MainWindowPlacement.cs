using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Shell;

/// <summary>
/// The main window's last placement: the position in the physical pixels Avalonia positions
/// windows with, the size in the logical units it sizes them with, and whether it was maximized.
/// </summary>
public sealed record StoredWindowPlacement(double X, double Y, double Width, double Height, bool IsMaximized);

/// <summary>
/// Remembers where the main window was and puts it back there (WIN-003).
/// <para>
/// The geometry tracked is the last <em>normal</em> one: a maximized window saves the bounds it
/// would restore to, plus the fact that it was maximized, so closing maximized does not burn the
/// screen-sized rectangle into the stored position. A stored position no current screen shows is
/// discarded — the monitor it referred to was unplugged — using the same visibility rule the
/// player's mini window already trusts.
/// </para>
/// </summary>
public sealed class MainWindowPlacement
{
    public const string SettingKey = "window.main.placement";

    private readonly ISettingsStore _store;
    private StoredWindowPlacement? _lastNormal;

    public MainWindowPlacement(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Restores the stored placement onto the window and follows it from then on; the write happens
    /// once, when the window is closing, whatever path closes it.
    /// </summary>
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (Usable(_store.Read<StoredWindowPlacement>(SettingKey), window.Screens) is { } stored)
        {
            window.Position = new PixelPoint((int)Math.Round(stored.X), (int)Math.Round(stored.Y));
            window.Width = stored.Width;
            window.Height = stored.Height;
            _lastNormal = stored with { IsMaximized = false };
            if (stored.IsMaximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        window.PositionChanged += (_, _) => Observe(window);
        window.SizeChanged += (_, _) => Observe(window);
        window.Closing += (_, _) => Save(window);
    }

    /// <summary>
    /// A stored placement is only usable while some screen still shows it. A machine that reports
    /// no screens at all cannot judge, and refusing on no evidence would lose the position on every
    /// headless start.
    /// </summary>
    public static StoredWindowPlacement? Usable(StoredWindowPlacement? stored, Screens? screens)
    {
        if (stored is null || stored.Width <= 0 || stored.Height <= 0)
        {
            return null;
        }

        var all = screens?.All;
        if (all is null || all.Count == 0)
        {
            return stored;
        }

        // The stored position is physical and the rule works in logical units, so each screen
        // judges with its own scaling — the same rule the player's mini window trusts.
        return all.Any(screen => new PlayerWindowGeometry(
                stored.X / screen.Scaling,
                stored.Y / screen.Scaling,
                stored.Width,
                stored.Height)
            .IsVisibleOn(screen.Bounds, screen.Scaling))
            ? stored
            : null;
    }

    private void Observe(Window window)
    {
        if (window.WindowState == WindowState.Normal && window.Width > 0 && window.Height > 0)
        {
            _lastNormal = new StoredWindowPlacement(
                window.Position.X,
                window.Position.Y,
                window.Width,
                window.Height,
                IsMaximized: false);
        }
    }

    private void Save(Window window)
    {
        // Minimized is a transient state nobody wants back at the next start; what is saved is the
        // last normal geometry and whether the window was maximized over it.
        var placement = (_lastNormal ?? new StoredWindowPlacement(
                window.Position.X,
                window.Position.Y,
                window.Width,
                window.Height,
                IsMaximized: false))
            with
        { IsMaximized = window.WindowState == WindowState.Maximized };
        _store.Write(SettingKey, placement);
    }
}
