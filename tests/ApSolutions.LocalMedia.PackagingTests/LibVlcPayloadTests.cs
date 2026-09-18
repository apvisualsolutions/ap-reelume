// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The LibVLC inside each package is exactly the tree that was verified free of GPL code, file for
/// file and byte for byte (ENG-013).
/// </summary>
/// <remarks>
/// verify-nogpl.ps1 proves a tree clean and writes its manifest; libvlc.lock.json pins that tree by
/// the hash of its published zip; fetch-libvlc.ps1 refuses any other. None of that says what reached
/// the package, and the gap is not theoretical: the day the engine changed, every output folder in
/// this repository still held VideoLAN's tree — GPL plugins and a win-x86 folder included — because
/// PreserveNewest copies files and never deletes one. A build over such a folder ships whatever it
/// left behind. So the package is compared with the manifest in both directions: a file the manifest
/// lacks is a leftover, and a file the package lacks is a plugin that went missing on the way.
/// </remarks>
public sealed class LibVlcPayloadTests
{
    [Theory]
    [InlineData("x64")]
    [InlineData("arm64")]
    public void The_packaged_engine_is_the_verified_tree_and_nothing_else(string architecture)
    {
        var packageRoot = architecture == "x64" ? PackageEvidence.PackageRoot() : PackageEvidence.Arm64PackageRoot();
        var engine = Path.Combine(packageRoot, "layout", "libvlc", $"win-{architecture}");
        Assert.True(
            Directory.Exists(engine),
            $"The {architecture} package carries no LibVLC at {engine}. "
                + (architecture == "x64" ? PackageEvidence.HowToProduce : PackageEvidence.HowToProduceArm64));

        var manifest = Manifest(architecture);
        var packaged = Directory.EnumerateFiles(engine, "*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(engine, file).Replace('\\', '/'),
                file => file,
                StringComparer.Ordinal);

        var leftovers = packaged.Keys.Where(path => !manifest.ContainsKey(path)).Order(StringComparer.Ordinal).ToArray();
        var missing = manifest.Keys.Where(path => !packaged.ContainsKey(path)).Order(StringComparer.Ordinal).ToArray();
        var altered = manifest
            .Where(entry => packaged.TryGetValue(entry.Key, out var file) && Sha256Of(file) != entry.Value)
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(leftovers.Length == 0, $"The package carries engine files the verified tree does not: {string.Join(", ", leftovers)}.");
        Assert.True(missing.Length == 0, $"The package lacks engine files the verified tree has: {string.Join(", ", missing)}.");
        Assert.True(altered.Length == 0, $"These engine files differ from the verified tree: {string.Join(", ", altered)}.");
    }

    /// <summary>
    /// The tree the build copied from is the pinned one, not one installed by hand for testing.
    /// </summary>
    /// <remarks>
    /// fetch-libvlc.ps1 -FromTree exists so the suites can run against a tree before it is published,
    /// and it marks what it installs "unpinned". This is what keeps such a tree from reaching a package
    /// that CI signs off.
    /// </remarks>
    [Theory]
    [InlineData("x64")]
    [InlineData("arm64")]
    public void The_engine_was_copied_from_the_pinned_tree(string architecture)
    {
        var marker = RepositoryLayout.PathFromRoot($"artifacts/libvlc/win-{architecture}.sha512");
        Assert.True(File.Exists(marker), $"No LibVLC tree was fetched: {marker} does not exist. Build the solution first.");

        using var lockFile = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.PathFromRoot("eng/libvlc/libvlc.lock.json")));
        var pinned = lockFile.RootElement.GetProperty("assets").GetProperty(architecture).GetProperty("sha512").GetString();

        Assert.False(string.IsNullOrEmpty(pinned), $"libvlc.lock.json pins no {architecture} tree.");
        Assert.Equal(pinned, File.ReadAllText(marker));
    }

    /// <summary>
    /// The bill of materials names the engine with the hash it is pinned by. It is built from the lock
    /// files, and since ENG-013 no lock file names LibVLC: without its own entry the largest native
    /// component of the artifact would drop out of the list on the day it changed.
    /// <para>
    /// Each package against its OWN architecture's tree. The first version read the x64 SBOM only, and
    /// gate-auditor found the ARM64 package naming the x64 zip — the script always took the x64 asset
    /// — with zeroed hashes in the ARM64 SBOM passing six of six.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("x64", "sbom/sbom.cyclonedx.json", "components", "hashes", "content")]
    [InlineData("x64", "sbom/sbom.spdx.json", "packages", "checksums", "checksumValue")]
    [InlineData("arm64", "sbom/sbom.cyclonedx.json", "components", "hashes", "content")]
    [InlineData("arm64", "sbom/sbom.spdx.json", "packages", "checksums", "checksumValue")]
    public void The_sbom_names_the_engine_by_its_pinned_hash(string architecture, string relativePath, string collection, string hashes, string value)
    {
        var packageRoot = architecture == "x64" ? PackageEvidence.PackageRoot() : PackageEvidence.Arm64PackageRoot();
        var path = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(
            File.Exists(path),
            $"{relativePath} is missing from the {architecture} package. "
                + (architecture == "x64" ? PackageEvidence.HowToProduce : PackageEvidence.HowToProduceArm64));

        using var lockFile = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.PathFromRoot("eng/libvlc/libvlc.lock.json")));
        var pinned = lockFile.RootElement.GetProperty("assets").GetProperty(architecture).GetProperty("sha512").GetString();
        Assert.False(string.IsNullOrEmpty(pinned), $"libvlc.lock.json pins no {architecture} tree.");

        using var sbom = JsonDocument.Parse(File.ReadAllText(path));
        var engine = sbom.RootElement.GetProperty(collection).EnumerateArray()
            .Where(entry => entry.GetProperty("name").GetString() == "LibVLC")
            .ToArray();

        var only = Assert.Single(engine);
        Assert.Contains(only.GetProperty(hashes).EnumerateArray(), hash => hash.GetProperty(value).GetString() == pinned);
    }

    private static Dictionary<string, string> Manifest(string architecture)
    {
        var path = RepositoryLayout.PathFromRoot($"artifacts/libvlc/win-{architecture}/manifest.json");
        Assert.True(File.Exists(path), $"The fetched {architecture} tree has no manifest at {path}. Build the solution first.");

        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        var files = manifest.RootElement.GetProperty("files").EnumerateArray()
            .ToDictionary(
                file => file.GetProperty("path").GetString()!,
                file => file.GetProperty("sha256").GetString()!,
                StringComparer.Ordinal);

        Assert.NotEmpty(files);
        return files;
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLower(CultureInfo.InvariantCulture);
    }
}
