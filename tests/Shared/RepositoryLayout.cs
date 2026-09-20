// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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
    /// <c>.claude/worktrees/</c> holds whole copies of this repository belonging to other sessions.
    /// Git ignores them and a CI runner does not have them at all, so a sweep that reads them
    /// answers a different question depending on how many sessions happen to be open and what each
    /// of them is holding — <b>red on the machine of whoever is writing and green in CI</b>, which
    /// is the worst way for a gate to be wrong. Measured three times: on 2026-09-02 against the
    /// run-duration figure, on 2026-09-03 with three parallel sessions each holding its own
    /// <c>CLAUDE.md</c>, and on 2026-09-20 as ENG-009, where running the gate auditor in a worktree
    /// put <c>EvidenceLinkTests</c> red over a copy of the one document allowed to quote the defect
    /// it guards against.
    /// </para>
    /// <para>
    /// <b>Relative to the root and never absolute</b>, which the first version of this got wrong and
    /// a parallel session measured within the hour. Matched against the absolute path, a checkout
    /// that itself lives under <c>.claude/worktrees/</c> excludes <b>every one of its own</b>
    /// documents: the sweep reads nothing and agrees with everything. Relative to its own root, a
    /// worktree's files simply do not carry the prefix, so it reads itself and nobody else.
    /// </para>
    /// <para>
    /// The prefix is the whole path and not a lone <c>worktrees</c> segment: a directory of that
    /// name anywhere else in the tree is this repository's own and has to be read.
    /// </para>
    /// </remarks>
    public static bool IsInsideAnotherCheckout(string path) =>
        Path.GetRelativePath(Root, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .StartsWith(".claude/worktrees/", StringComparison.Ordinal);

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
