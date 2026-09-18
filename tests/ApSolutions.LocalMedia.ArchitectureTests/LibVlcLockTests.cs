// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// The engine the application ships is the one built without GPL code, pinned by hash, and nothing
/// can bring VideoLAN's NuGet package back in (ENG-013).
/// </summary>
public sealed partial class LibVlcLockTests
{
    private const string VideoLanPackage = "VideoLAN.LibVLC.Windows";

    private static readonly string[] Architectures = ["x64", "arm64"];

    /// <summary>
    /// The package carries fourteen GPL plugins, and the day it is referenced again — by a new project,
    /// or by a transitive pin — they are back in the output next to the rebuilt tree, under the same
    /// folder names. What is read is what restore reads: the Include attributes and the lock files'
    /// keys, not the prose, so a comment explaining why the package left does not trip it.
    /// </summary>
    [Fact]
    public void No_project_references_the_videolan_package()
    {
        var root = RepositoryLayout.PathFromRoot(".");
        var msbuild = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Append(Path.Combine(root, "Directory.Packages.props"))
            .Append(Path.Combine(root, "Directory.Build.props"))
            .Where(path => File.Exists(path) && !IsBuildOutput(root, path));
        var referencing = msbuild
            .Where(path => XDocument.Load(path).Descendants()
                .Any(element => element.Name.LocalName is "PackageReference" or "PackageVersion"
                    && (string?)element.Attribute("Include") == VideoLanPackage))
            .Concat(Directory.EnumerateFiles(root, "packages.lock.json", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(root, path))
                .Where(path => LockFileNames(path).Contains(VideoLanPackage)))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            referencing.Length == 0,
            $"{VideoLanPackage} is referenced again, and it carries GPL plugins: {string.Join(", ", referencing)}.");
    }

    /// <summary>The control for the check above: the reader does see a package the locks resolve.</summary>
    [Fact]
    public void The_lock_file_reader_sees_a_package_that_is_there()
    {
        var infrastructureLock = RepositoryLayout.PathFromRoot("src/ApSolutions.LocalMedia.Infrastructure/packages.lock.json");

        Assert.Contains("LibVLCSharp", LockFileNames(infrastructureLock));
    }

    [Fact]
    public void The_infrastructure_project_takes_the_engine_from_the_pinned_tree()
    {
        var project = XDocument.Load(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Infrastructure/ApSolutions.LocalMedia.Infrastructure.csproj"));

        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "Import"),
            element => ((string?)element.Attribute("Project") ?? string.Empty).EndsWith(
                @"eng\libvlc\LibVlc.targets", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every tree the build can download is pinned, and so is the corresponding source that has to
    /// travel with it. A null here is what the lock file holds between building a tree and publishing
    /// it; it must never reach main, because fetch-libvlc.ps1 would then have nothing to refuse with.
    /// </summary>
    [Fact]
    public void The_lock_file_pins_both_trees_and_their_source()
    {
        using var lockFile = ReadLock();
        var root = lockFile.RootElement;
        var hashes = Architectures
            .Select(architecture => (Name: architecture, Hash: root.GetProperty("assets").GetProperty(architecture).GetProperty("sha512").GetString()))
            .Append((Name: "source", Hash: root.GetProperty("sourceAsset").GetProperty("sha512").GetString()))
            .ToArray();

        var unpinned = hashes
            .Where(entry => entry.Hash is null || !Sha512Hex().IsMatch(entry.Hash))
            .Select(entry => $"{entry.Name}: '{entry.Hash}'")
            .ToArray();

        Assert.True(unpinned.Length == 0, $"libvlc.lock.json pins nothing, or not a lowercase SHA-512, for {string.Join(", ", unpinned)}.");
        Assert.Equal(hashes.Length, hashes.Select(entry => entry.Hash).Distinct(StringComparer.Ordinal).Count());
        Assert.True(root.GetProperty("buildRun").ValueKind == JsonValueKind.Number, "libvlc.lock.json does not name the run that built and verified the trees.");
    }

    /// <summary>
    /// The tag names the VLC the trees were built from, and that is the VLC build-nogpl.sh builds. The
    /// day the script moves to another tag, the pinned trees are the old engine until a new one is
    /// published — and the source this lock points at is the old source.
    /// </summary>
    [Fact]
    public void The_pinned_trees_are_of_the_vlc_the_build_script_builds()
    {
        using var lockFile = ReadLock();
        var root = lockFile.RootElement;
        var script = File.ReadAllText(RepositoryLayout.PathFromRoot("eng/libvlc/build-nogpl.sh"));
        var tag = VlcTag().Match(script);
        Assert.True(tag.Success, "build-nogpl.sh no longer declares VLC_TAG=.");

        var vlc = root.GetProperty("vlcVersion").GetString();
        Assert.Equal(tag.Groups[1].Value, vlc);
        Assert.Matches($"^libvlc-{Regex.Escape(vlc!)}-nogpl\\.[0-9]+$", root.GetProperty("tag").GetString()!);
        Assert.Equal($"{root.GetProperty("tag").GetString()}-source.tar", root.GetProperty("sourceAsset").GetProperty("fileName").GetString());
    }

    private static JsonDocument ReadLock() =>
        JsonDocument.Parse(File.ReadAllText(RepositoryLayout.PathFromRoot("eng/libvlc/libvlc.lock.json")));

    private static HashSet<string> LockFileNames(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("dependencies").EnumerateObject()
            .SelectMany(framework => framework.Value.EnumerateObject().Select(package => package.Name))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Outputs, and anything under a top-level dot folder. The second is measured, not tidiness: the
    /// first run of this test named ten files under <c>.runner/_work</c>, a self-hosted runner's
    /// checkout of an older commit sitting inside this one — and <c>.claude/</c> holds worktrees the
    /// same way. Neither is the tree being built.
    /// </summary>
    private static bool IsBuildOutput(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative.StartsWith("artifacts/", StringComparison.Ordinal)
            || relative.Contains("/obj/", StringComparison.Ordinal)
            || relative.Contains("/bin/", StringComparison.Ordinal)
            || relative.StartsWith('.');
    }

    [GeneratedRegex("^[0-9a-f]{128}$")]
    private static partial Regex Sha512Hex();

    [GeneratedRegex("(?m)^VLC_TAG=(\\S+)$")]
    private static partial Regex VlcTag();
}
