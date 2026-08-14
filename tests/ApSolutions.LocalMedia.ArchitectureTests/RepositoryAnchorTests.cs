// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// One anchor for the repository root, and one place that finds it (ARQ-012).
/// </summary>
/// <remarks>
/// The same walk up from the output directory was pasted into fifty-eight files, and it was not even
/// the same walk: two copies anchored on <c>docs/FEATURES.md</c> and the rest on the solution file,
/// so this repository held two definitions of its own root. The shared one is
/// <c>tests/Shared/RepositoryLayout.cs</c>, linked into every test project from
/// <c>tests/Directory.Build.targets</c>.
/// <para>
/// The list works like the orphan list in <see cref="ServiceConsumptionTests"/> and the queue list in
/// <see cref="NativeInstanceOwnershipTests"/>: it may only shrink, and a file that stops offending
/// has to leave it.
/// </para>
/// </remarks>
public sealed class RepositoryAnchorTests
{
    /// <summary>The one file allowed to look for the anchor.</summary>
    private const string Owner = "tests/Shared/RepositoryLayout.cs";

    /// <remarks>
    /// The pattern is the walk's opening statement rather than every mention of the output directory:
    /// <c>ForcedShutdownTests</c> reads the build configuration from <c>…BaseDirectory).Parent?.Name</c>
    /// and is not looking for a root. Anything that does look for one has to name a marker file, and
    /// <see cref="The_anchor_is_named_in_one_place"/> is what catches that.
    /// </remarks>
    [Fact]
    public void The_repository_root_is_found_in_one_place()
    {
        string[] stillFindItThemselves = [];

        var offenders = TestFilesMatching(@"new\s+DirectoryInfo\s*\(\s*AppContext\.BaseDirectory\s*\)\s*;");

        Assert.True(
            offenders.Except(stillFindItThemselves, StringComparer.Ordinal).ToArray() is [],
            "The repository root is found once, in " + Owner + "; these walk up to it themselves: "
                + string.Join(", ", offenders.Except(stillFindItThemselves, StringComparer.Ordinal))
                + ".");
        Assert.True(
            stillFindItThemselves.Except(offenders, StringComparer.Ordinal).ToArray() is [],
            "These are declared as still finding the root themselves and no longer do: "
                + string.Join(", ", stillFindItThemselves.Except(offenders, StringComparer.Ordinal))
                + ". Remove them from the list — it is only allowed to shrink.");
    }

    /// <summary>
    /// The anchor is a decision, not a string to be repeated. A file naming it has either found a
    /// second root or hard-coded the first one, and both end the same way.
    /// </summary>
    [Fact]
    public void The_anchor_is_named_in_one_place()
    {
        var offenders = TestFilesMatching(@"ApSolutions\.LocalMedia\.sln");

        Assert.True(
            offenders is [],
            "Only " + Owner + " names the anchor; these name it too: " + string.Join(", ", offenders) + ".");
    }

    private static string[] TestFilesMatching(string pattern) =>
    [
        .. Directory
            .EnumerateFiles(RepositoryLayout.PathFromRoot("tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepositoryLayout.Root, file).Replace('\\', '/'))
            .Where(relative => !relative.Equals(Owner, StringComparison.Ordinal))
            .Where(relative => Regex.IsMatch(
                File.ReadAllText(RepositoryLayout.PathFromRoot(relative)),
                pattern,
                RegexOptions.None,
                TimeSpan.FromSeconds(2)))
            .Order(StringComparer.Ordinal),
    ];
}
