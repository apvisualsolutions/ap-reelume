// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;
using System.Text.Json;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// The engine's publication refuses a build that fetched a third-party source the licence inventory
/// does not name, or stopped fetching one it does.
/// </summary>
/// <remarks>
/// The notice of the libraries inside LibVLC is only as good as the list it was written from, and the
/// list is only true of the build it was read from. A new VLC tag, or a recipe that starts pulling in
/// one more library, would ship a binary whose notice leaves that library out — the defect the
/// inventory was written to end. So the list is compared with what the build actually fetched, at the
/// one moment that can stop it: before the engine is published.
/// </remarks>
public sealed class ContribLicenceCheckTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "contrib-check-" + Guid.NewGuid().ToString("n")[..12]);

    public ContribLicenceCheckTests()
    {
        // The scene is the inventory's own list, laid out the way the publication lays it out.
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.PathFromRoot("eng/libvlc/contrib-licences.json")));
        foreach (var source in document.RootElement.GetProperty("sources").EnumerateArray())
        {
            var path = Path.Combine(_root, source.GetProperty("tarball").GetString()!);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "archive");
        }
    }

    [Fact]
    public void A_build_that_fetched_exactly_what_the_inventory_names_passes()
    {
        // The two fetch stamps the build leaves beside a git archive are not sources.
        File.WriteAllText(Path.Combine(_root, "mfx-7efc7505465bc1f16fbd1da3d24aa5bd9d46c5ca.githash"), "stamp");

        var (exitCode, output) = Check();

        Assert.True(exitCode == 0, output);
    }

    [Fact]
    public void A_source_the_inventory_does_not_name_is_refused_by_name()
    {
        File.WriteAllText(Path.Combine(_root, "newlib-1.0.tar.gz"), "archive");

        var (exitCode, output) = Check();

        Assert.Equal(1, exitCode);
        Assert.Contains("newlib-1.0.tar.gz", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_source_the_inventory_names_and_the_build_no_longer_fetched_is_refused_by_name()
    {
        // The other direction: a library dropped from the build leaves the inventory, and the notice,
        // describing a binary that is not the one published.
        File.Delete(Path.Combine(_root, "zlib-1.3.1.tar.xz"));

        var (exitCode, output) = Check();

        Assert.Equal(1, exitCode);
        Assert.Contains("zlib-1.3.1.tar.xz", output, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private (int ExitCode, string Output) Check()
    {
        var start = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(RepositoryLayout.PathFromRoot("eng/libvlc/check-contrib-licences.ps1"));
        start.ArgumentList.Add("-Tarballs");
        start.ArgumentList.Add(_root);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("pwsh did not start; the check cannot be measured without it.");
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(120_000), "the check did not finish in two minutes.");
        return (process.ExitCode, stdout + stderr.Result);
    }
}
