namespace ApSolutions.LocalMedia.Domain.Lifecycle;

/// <summary>What the close button does.</summary>
public enum CloseBehavior
{
    /// <summary>Closing the window ends the application. This is the default.</summary>
    Exit,

    /// <summary>Closing the window hides it and leaves the tray icon behind.</summary>
    MinimizeToTray,
}

/// <summary>
/// Everything the person has chosen about how the application lives on their machine. Every field
/// starts in the quietest possible state: no tray, no automatic start, and a close button that
/// closes.
/// </summary>
public sealed record LifecyclePreferences
{
    /// <summary>The state a machine that has never been asked anything is in.</summary>
    public static LifecyclePreferences Default { get; } = new();

    public bool TrayEnabled { get; init; }

    public bool StartWithWindows { get; init; }

    public CloseBehavior CloseBehavior { get; init; } = CloseBehavior.Exit;
}

/// <summary>
/// What closing the window means right now, resolved in one place so no caller can reorder it.
/// Progress is always written first: everything else can be redone, a lost position cannot.
/// </summary>
public sealed record CloseDecision(
    bool PersistProgressFirst,
    bool StopPlayback,
    bool HideToTray,
    bool ExitApplication);

/// <summary>
/// The rules that keep the tray and the Windows startup entry opt-in, reversible, and consistent.
/// Nothing here touches Windows; the adapters only carry out what this decides.
/// </summary>
public static class AppLifecyclePolicy
{
    /// <summary>
    /// Repairs a stored state that contradicts itself. Closing to a tray that does not exist would
    /// make the close button do nothing, so it is corrected rather than trusted.
    /// </summary>
    public static LifecyclePreferences Normalize(LifecyclePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.TrayEnabled || preferences.CloseBehavior == CloseBehavior.Exit
            ? preferences
            : preferences with { CloseBehavior = CloseBehavior.Exit };
    }

    /// <summary>Turning the tray off also gives the close button its plain meaning back.</summary>
    public static LifecyclePreferences WithTray(LifecyclePreferences preferences, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return Normalize(preferences with { TrayEnabled = isEnabled });
    }

    /// <summary>
    /// Turning automatic start on requires consent that was actually given; turning it off never
    /// does, because withdrawing a permission cannot need permission.
    /// </summary>
    public static LifecyclePreferences WithStartup(
        LifecyclePreferences preferences,
        bool isRequested,
        bool hasConsent)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (isRequested && !hasConsent)
        {
            return preferences;
        }

        return preferences with { StartWithWindows = isRequested };
    }

    /// <summary>Closing to the tray is only a choice while the tray exists.</summary>
    public static LifecyclePreferences WithCloseBehavior(
        LifecyclePreferences preferences,
        CloseBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return Normalize(preferences with { CloseBehavior = behavior });
    }

    /// <summary>Decides what closing does, always writing the position before anything else.</summary>
    public static CloseDecision ResolveClose(LifecyclePreferences preferences, bool hasActivePlayback)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var normalized = Normalize(preferences);
        var hidesToTray = normalized is { TrayEnabled: true, CloseBehavior: CloseBehavior.MinimizeToTray };
        return new CloseDecision(
            PersistProgressFirst: true,
            StopPlayback: hasActivePlayback && !hidesToTray,
            HideToTray: hidesToTray,
            ExitApplication: !hidesToTray);
    }
}
