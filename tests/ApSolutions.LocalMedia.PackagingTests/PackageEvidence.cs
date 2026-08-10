// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// Where the packaging suites read from. The artifact is too large and too slow to build inside a
/// test, so <c>eng/package-x64.ps1</c> and <c>eng/verify-package.ps1</c> produce it and leave their
/// findings behind as JSON; these suites are the part that refuses to accept them.
/// </summary>
/// <remarks>
/// Nothing here is optional. A missing report is a failure, never a skip: a packaging suite that goes
/// green because there was no package to examine is worse than no suite at all.
/// </remarks>
internal static class PackageEvidence
{
    /// <summary>Names the directory the packaging scripts wrote to, when it is not the default one.</summary>
    public const string RootVariable = "APSOLUTIONS_LOCALMEDIA_PACKAGE_ROOT";

    /// <summary>The same, for the ARM64 package, which is built and reported separately.</summary>
    public const string Arm64RootVariable = "APSOLUTIONS_LOCALMEDIA_PACKAGE_ROOT_ARM64";

    /// <summary>How to produce what is missing, said once so every failure can repeat it.</summary>
    public const string HowToProduce =
        "Run `pwsh ./eng/package-x64.ps1` and then `pwsh ./eng/verify-package.ps1 -Mode Verify`.";

    /// <summary>The same instruction for the ARM64 artifact.</summary>
    public const string HowToProduceArm64 = "Run `pwsh ./eng/package-arm64.ps1`.";

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root is not above the test assembly.");
    }

    public static string PackageRoot() =>
        Environment.GetEnvironmentVariable(RootVariable) is { } named && !string.IsNullOrWhiteSpace(named)
            ? Path.GetFullPath(named)
            : Path.Combine(RepositoryRoot(), "artifacts", "package");

    /// <summary>
    /// Where the ARM64 artifact and its reports are. It is a directory of its own rather than a
    /// second run into the same one: the two packages have to be comparable, and a build that
    /// overwrote the other's payload would leave nothing to compare.
    /// </summary>
    public static string Arm64PackageRoot() =>
        Environment.GetEnvironmentVariable(Arm64RootVariable) is { } named && !string.IsNullOrWhiteSpace(named)
            ? Path.GetFullPath(named)
            : Path.Combine(RepositoryRoot(), "artifacts", "package-arm64");

    /// <summary>
    /// What the versioned licence folder says the artifact has to carry, checked against a layout.
    /// </summary>
    /// <remarks>
    /// Both architectures are held to the same list from one place, because the failure to guard
    /// against is one packaging script updated and the other forgotten. Contents are compared, not
    /// just names: a licence that arrives truncated is not a copy of the licence.
    /// </remarks>
    public static string[] LicenceTextsMissingFrom(string layoutRoot)
    {
        var source = new DirectoryInfo(Path.Combine(RepositoryRoot(), "docs", "release", "licenses"));
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The versioned licence texts are not at {source.FullName}, so nothing states what the "
                    + "artifact owes.");
        }

        var problems = new List<string>();
        foreach (var file in source.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source.FullName, file.FullName);
            var shipped = Path.Combine(layoutRoot, "licenses", relative);
            if (!File.Exists(shipped))
            {
                problems.Add($"licenses/{relative} is not in the artifact");
            }
            else if (!File.ReadAllBytes(shipped).SequenceEqual(File.ReadAllBytes(file.FullName)))
            {
                problems.Add($"licenses/{relative} in the artifact is not the versioned text");
            }
        }

        return [.. problems.Order(StringComparer.Ordinal)];
    }

    /// <summary>The packaging project's directory, which is versioned and always present.</summary>
    public static string PackageProjectRoot() =>
        Path.Combine(RepositoryRoot(), "src", "ApSolutions.LocalMedia.Windows.Package");

    public static XDocument Manifest() =>
        XDocument.Load(Path.Combine(PackageProjectRoot(), "Package.appxmanifest"));

    /// <summary>The single SemVer the whole release is cut from.</summary>
    public static string DeclaredVersion()
    {
        var properties = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Build.props"));
        var version = properties
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Version")?.Value;
        return string.IsNullOrWhiteSpace(version)
            ? throw new InvalidOperationException("Directory.Build.props declares no <Version>.")
            : version.Trim();
    }

    /// <summary>SemVer maps to the four-part package version by appending the reserved revision.</summary>
    public static string PackageVersionFor(string semanticVersion) =>
        string.Create(CultureInfo.InvariantCulture, $"{semanticVersion}.0");

    public static JsonElement ReadReport(string fileName) => ReadReportFrom(PackageRoot(), fileName, HowToProduce);

    /// <summary>The ARM64 counterpart, which names its own script when what it needs is not there.</summary>
    public static JsonElement ReadArm64Report(string fileName) =>
        ReadReportFrom(Arm64PackageRoot(), fileName, HowToProduceArm64);

    private static JsonElement ReadReportFrom(string root, string fileName, string howToProduce)
    {
        var path = Path.Combine(root, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{fileName} is not in {root}, so nothing can be verified about the artifact. {howToProduce}",
                path);
        }

        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    public static string RequiredString(this JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidOperationException($"The report has no string '{property}'.");
}
