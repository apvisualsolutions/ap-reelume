// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.TestSupport;

/// <summary>
/// Where this checkout begins. One anchor, found once, for every test project (ARQ-012).
/// </summary>
/// <remarks>
/// The walk up from the output directory was pasted into fifty-eight files, and two of those copies
/// anchored on <c>docs/FEATURES.md</c> while the rest anchored on the solution: two definitions of
/// "the repository root" in one repository, which is one too many. The solution file is the anchor
/// because it *is* the definition of this checkout, and because a document can be moved — moving
/// <c>docs/FEATURES.md</c> would have broken the root itself.
/// </remarks>
internal static class RepositoryLayout
{
    private const string Anchor = "ApSolutions.LocalMedia.sln";

    public static string Root { get; } = FindRoot();

    /// <summary>A path under the root, written with forward slashes wherever it is used.</summary>
    public static string PathFromRoot(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>A path under the root from its segments, for callers that already have them apart.</summary>
    public static string PathFromRoot(params string[] segments) =>
        Path.Combine([Root, .. segments]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, Anchor)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"No directory above '{AppContext.BaseDirectory}' holds {Anchor}, so there is no checkout to read.");
    }
}
