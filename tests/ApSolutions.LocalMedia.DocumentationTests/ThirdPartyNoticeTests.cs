// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The third-party notices name every package the artifact actually carries.
/// </summary>
/// <remarks>
/// The notices were written by hand and drifted: they listed the eight packages somebody remembered
/// asking for, while the package carried thirty. A licence obligation does not care whether a
/// dependency was requested directly or arrived pulled by another, so the check reads the lock file
/// of the project that gets packaged and demands a line for each resolved package in both languages.
/// </remarks>
public sealed class ThirdPartyNoticeTests
{
    /// <summary>The project whose closure becomes the published payload.</summary>
    private const string PackagedProject = "src/ApSolutions.LocalMedia.Windows/packages.lock.json";

    /// <summary>
    /// Native assets for other operating systems are restored and then stripped from the layout, so
    /// they are not distributed and must not be claimed as distributed.
    /// </summary>
    private static readonly string[] ForeignRuntimeMarkers =
        [".NativeAssets.Linux", ".NativeAssets.macOS", ".NativeAssets.WebAssembly"];

    public static TheoryData<string> NoticeFiles() =>
    [
        "docs/release/THIRD-PARTY-NOTICES.es.md",
        "docs/release/THIRD-PARTY-NOTICES.en.md",
    ];

    [Theory]
    [MemberData(nameof(NoticeFiles))]
    public void Every_distributed_package_is_named_in_the_notices(string noticePath)
    {
        var notice = File.ReadAllText(RepositoryLayout.PathFromRoot(noticePath));
        var missing = DistributedPackages()
            .Where(package => !notice.Contains(package, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"{noticePath} does not name: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// The two languages list the same packages. A component added to one table and forgotten in the
    /// other leaves half the readers with an incomplete notice.
    /// </summary>
    [Fact]
    public void The_two_languages_name_the_same_packages()
    {
        var packages = DistributedPackages();
        var spanish = File.ReadAllText(RepositoryLayout.PathFromRoot("docs/release/THIRD-PARTY-NOTICES.es.md"));
        var english = File.ReadAllText(RepositoryLayout.PathFromRoot("docs/release/THIRD-PARTY-NOTICES.en.md"));

        foreach (var package in packages)
        {
            Assert.Equal(
                spanish.Contains(package, StringComparison.Ordinal),
                english.Contains(package, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// The resolved versions are stated, not just the package names: a notice that names a component
    /// without its version describes some build, not the one in the reader's hands.
    /// </summary>
    [Theory]
    [MemberData(nameof(NoticeFiles))]
    public void The_notices_state_the_resolved_versions(string noticePath)
    {
        var notice = File.ReadAllText(RepositoryLayout.PathFromRoot(noticePath));
        var stale = ResolvedPackages()
            .Where(entry => !notice.Contains($"| {entry.Key} | {entry.Value} |", StringComparison.Ordinal))
            .Select(entry => $"{entry.Key} {entry.Value}")
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"{noticePath} does not carry the resolved version for: {string.Join(", ", stale)}.");
    }

    private static string[] DistributedPackages() =>
        [.. ResolvedPackages().Keys];

    private static SortedDictionary<string, string> ResolvedPackages()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(RepositoryLayout.PathFromRoot(PackagedProject)));

        var resolved = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (var package in framework.Value.EnumerateObject())
            {
                var type = package.Value.GetProperty("type").GetString();
                if (type == "Project")
                {
                    continue;
                }

                if (ForeignRuntimeMarkers.Any(marker =>
                    package.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                resolved[package.Name] = package.Value.GetProperty("resolved").GetString()!;
            }
        }

        Assert.NotEmpty(resolved);
        return resolved;
    }
}
