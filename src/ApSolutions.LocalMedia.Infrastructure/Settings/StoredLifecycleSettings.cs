using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Lifecycle;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>
/// Remembers the tray and startup choices between sessions. A stored state that contradicts itself
/// is repaired on the way out rather than handed to the application, so a hand-edited settings file
/// cannot leave the close button doing nothing.
/// </summary>
public sealed class StoredLifecycleSettings : ILifecycleSettings
{
    private const string TrayKey = "lifecycle.trayEnabled";
    private const string StartupKey = "lifecycle.startWithWindows";
    private const string CloseKey = "lifecycle.closeBehavior";

    private readonly ISettingsStore _store;

    public StoredLifecycleSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public LifecyclePreferences Current => AppLifecyclePolicy.Normalize(new LifecyclePreferences
    {
        TrayEnabled = _store.Read<bool?>(TrayKey) ?? false,
        StartWithWindows = _store.Read<bool?>(StartupKey) ?? false,
        CloseBehavior = _store.Read<CloseBehavior?>(CloseKey) ?? CloseBehavior.Exit,
    });

    public void Save(LifecyclePreferences updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        var normalized = AppLifecyclePolicy.Normalize(updated);
        _store.Write(TrayKey, normalized.TrayEnabled);
        _store.Write(StartupKey, normalized.StartWithWindows);
        _store.Write(CloseKey, normalized.CloseBehavior);
    }
}
