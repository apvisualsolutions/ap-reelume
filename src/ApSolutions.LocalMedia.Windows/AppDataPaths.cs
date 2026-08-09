using ApSolutions.LocalMedia.Application.Storage;

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
    }

    public string DataRoot { get; }

    public string DatabasePath { get; }

    public string SettingsPath { get; }

    public string BackupsDirectory { get; }

    public string PersonalArtworkDirectory { get; }

    public string RemoteCacheDirectory { get; }

    public string DiagnosticsDirectory { get; }

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
