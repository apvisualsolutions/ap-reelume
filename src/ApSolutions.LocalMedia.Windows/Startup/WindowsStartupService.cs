using ApSolutions.LocalMedia.Application.Lifecycle;
using Microsoft.Win32;

namespace ApSolutions.LocalMedia.Windows.Startup;

/// <summary>
/// Automatic start, written to the current user's Run key and nowhere else.
/// <para>
/// The key is a constructor argument so a test can exercise the real registry without writing into
/// the key Windows reads at sign-in. Nothing here needs elevation, nothing is written for another
/// user, and disabling removes one value rather than the key it lives in.
/// </para>
/// </summary>
public sealed class WindowsStartupService : IStartupService
{
    /// <summary>The Run key Windows reads when the person signs in.</summary>
    public const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The value name, which is the stable package identity, never the public product name.</summary>
    public const string ValueName = "APSolutions.LocalMedia";

    private readonly string _subKey;

    public WindowsStartupService(string executablePath, string subKey = WindowsRunKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(subKey);
        _subKey = subKey;
        ExpectedCommand = $"\"{executablePath}\"";
    }

    public string ExpectedCommand { get; }

    public StartupEntryState Inspect()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey);
        return key?.GetValue(ValueName) switch
        {
            null => StartupEntryState.Absent,
            string stored when string.Equals(stored, ExpectedCommand, StringComparison.OrdinalIgnoreCase) =>
                StartupEntryState.Present,
            _ => StartupEntryState.Invalid,
        };
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_subKey);
        key.SetValue(ValueName, ExpectedCommand, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public bool Repair()
    {
        if (Inspect() != StartupEntryState.Invalid)
        {
            return false;
        }

        Enable();
        return true;
    }
}
