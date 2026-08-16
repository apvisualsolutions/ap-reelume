// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Windows.Startup;

namespace ApSolutions.LocalMedia.Windows;

public sealed class AppDataPaths : IAppDataPaths
{
    /// <summary>
    /// The variable that names where the application keeps its data. It is read once, at startup.
    /// </summary>
    public const string DataRootVariableName = "AP_LOCALMEDIA_DATA_ROOT";

    public AppDataPaths()
        : this(ResolveDefaultRoot())
    {
    }

    public AppDataPaths(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        DataRoot = Path.GetFullPath(dataRoot);
        DatabasePath = Path.Combine(DataRoot, "library.db");
        SettingsPath = Path.Combine(DataRoot, "settings.json");
        BackupsDirectory = Path.Combine(DataRoot, "backups");
        PersonalArtworkDirectory = Path.Combine(DataRoot, "personal-artwork");
        RemoteCacheDirectory = Path.Combine(DataRoot, "cache", "artwork");
        DiagnosticsDirectory = Path.Combine(DataRoot, "diagnostics");
        StartupRegistrySubKey = ResolveStartupSubKey(DataRoot);
    }

    public string DataRoot { get; }

    public string DatabasePath { get; }

    public string SettingsPath { get; }

    public string BackupsDirectory { get; }

    public string PersonalArtworkDirectory { get; }

    public string RemoteCacheDirectory { get; }

    public string DiagnosticsDirectory { get; }

    public string StartupRegistrySubKey { get; }

    /// <summary>
    /// The key Windows reads at sign-in for the run that owns this machine's profile, and a key of
    /// its own for a run that does not.
    /// </summary>
    /// <remarks>
    /// The rule is the same one <see cref="ResolveDefaultRoot"/> follows, and it is deliberately
    /// about the resolved root rather than about the variable: somebody who moves their data with
    /// <see cref="DataRootVariableName"/> is still the person who signs in here, and their startup
    /// entry has to be the one Windows reads. A run handed a root that is neither — a test harness,
    /// a walk, a lifecycle check — is a run that must leave nothing behind, so it writes under a key
    /// named after that root and nowhere near sign-in.
    /// </remarks>
    private static string ResolveStartupSubKey(string dataRoot)
    {
        // Trimmed and upper-cased before either comparing or naming, because the same folder spelt
        // with a trailing separator or in another case is the same folder — and a run that found a
        // different key on its second launch would leave a second entry behind.
        var root = Path.TrimEndingDirectorySeparator(dataRoot).ToUpperInvariant();
        var owned = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ResolveDefaultRoot())).ToUpperInvariant();
        if (string.Equals(root, owned, StringComparison.Ordinal))
        {
            return WindowsStartupService.WindowsRunKey;
        }

        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)).AsSpan(0, 8));
        return $@"Software\APSolutions\LocalMedia\Isolated\{token}\Run";
    }

    /// <summary>
    /// The profile folder, unless the environment names somewhere else.
    /// <para>
    /// Naming it is what makes a run isolable: a lifecycle check — install, launch, upgrade,
    /// uninstall — otherwise has to run against the one profile folder, and on a machine with no
    /// clean virtual machine that means destroying whoever's data is already there. A blank value is
    /// the same as no value, so an empty variable never resolves to the working directory.
    /// </para>
    /// </summary>
    private static string ResolveDefaultRoot() =>
        Environment.GetEnvironmentVariable(DataRootVariableName) is { } named
        && !string.IsNullOrWhiteSpace(named)
            ? named
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "APSolutions",
                "LocalMedia");
}
