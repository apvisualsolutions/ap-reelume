// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.TestSupport;

/// <summary>
/// Where this checkout begins. One anchor, found once, for every test project.
/// </summary>
/// <remarks>
/// The walk up from the output directory was pasted into fifty-eight files, and two of those copies
/// anchored on a document while the rest anchored on the solution: two definitions of
/// "the repository root" in one repository, which is one too many. The solution file is the anchor
/// because it *is* the definition of this checkout, and because a document can be moved — moving
/// that document would have broken the root itself.
/// </remarks>
internal static class RepositoryLayout
{
    private const string Anchor = "ApSolutions.LocalMedia.sln";

    /// <summary>
    /// Whether each directory already asked about holds its own <c>.git</c>, so a sweep over
    /// thousands of files asks the disk once per directory rather than once per file and level.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> CheckoutRoots =
        new(StringComparer.OrdinalIgnoreCase);

    public static string Root { get; } = FindRoot();

    /// <summary>
    /// The solution file itself, for the rules that read it as a document rather than as an anchor.
    /// Exposed here so its name stays in one place: a test that spelled it out would be the very
    /// duplication the anchor rule exists to prevent, and would trip that rule saying so.
    /// </summary>
    public static string SolutionPath { get; } = Path.Combine(Root, Anchor);

    /// <summary>A path under the root, written with forward slashes wherever it is used.</summary>
    public static string PathFromRoot(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>A path under the root from its segments, for callers that already have them apart.</summary>
    public static string PathFromRoot(params string[] segments) =>
        Path.Combine([Root, .. segments]);

    /// <summary>
    /// Whether a path belongs to a different checkout of this repository rather than to this one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Another checkout is any directory below the root that carries its own <c>.git</c>: a
    /// directory for a nested clone, a file for a git worktree. Such copies are not part of this
    /// checkout and a CI runner does not have them at all, so a sweep that reads them answers a
    /// different question depending on how many copies happen to exist and what each of them is
    /// holding — <b>red on the machine of whoever is writing and green in CI</b>, which is the worst
    /// way for a gate to be wrong. Measured three times, the last with <c>EvidenceLinkTests</c> red
    /// over a worktree's copy of the one document allowed to quote the defect it guards against.
    /// </para>
    /// <para>
    /// <b>Relative to the root and never absolute</b>, which the first version of this got wrong.
    /// Only the directories between this root and the path are asked: a checkout that itself lives
    /// inside another one would otherwise find that one's <c>.git</c> above its own root and exclude
    /// <b>every one of its own</b> documents, so the sweep would read nothing and agree with
    /// everything. Counted from its own root, a worktree reads itself and nobody else.
    /// </para>
    /// <para>
    /// The rule is the marker and never a folder name: a directory called <c>worktrees</c>, or
    /// anything else, with no <c>.git</c> of its own is this repository's own and has to be read.
    /// </para>
    /// </remarks>
    public static bool IsInsideAnotherCheckout(string path)
    {
        var relative = Path.GetRelativePath(Root, path);
        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments[0] == "..")
        {
            return false;
        }

        var directory = Root;
        foreach (var segment in segments)
        {
            directory = Path.Combine(directory, segment);
            if (CheckoutRoots.GetOrAdd(directory, candidate => Path.Exists(Path.Combine(candidate, ".git"))))
            {
                return true;
            }
        }

        return false;
    }


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
