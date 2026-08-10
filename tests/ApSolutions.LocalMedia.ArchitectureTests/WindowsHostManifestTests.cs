// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// WIN-002: the Windows host declares its own application manifest. Without one, the process runs
/// under the 260-character path limit on machines that already lifted it — a library under a deep
/// folder simply loses files — and DPI awareness is whatever the runtime guessed instead of the
/// per-monitor mode the player's windows are written for.
/// </summary>
public sealed class WindowsHostManifestTests
{
    private const string ManifestPath = "src/ApSolutions.LocalMedia.Windows/app.manifest";

    [Fact]
    public void The_host_project_declares_the_manifest()
    {
        var project = XDocument.Load(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Windows/ApSolutions.LocalMedia.Windows.csproj"));

        Assert.Equal(
            "app.manifest",
            project.Descendants("ApplicationManifest").SingleOrDefault()?.Value);
    }

    [Fact]
    public void The_manifest_lifts_the_path_limit_and_pins_per_monitor_dpi()
    {
        var path = RepositoryLayout.PathFromRoot(ManifestPath);
        Assert.True(File.Exists(path), $"{ManifestPath} is missing.");
        var manifest = File.ReadAllText(path);

        Assert.Contains("<longPathAware", manifest, StringComparison.Ordinal);
        Assert.Contains(">true</longPathAware>", manifest, StringComparison.Ordinal);
        Assert.Contains("PerMonitorV2", manifest, StringComparison.Ordinal);
    }
}
