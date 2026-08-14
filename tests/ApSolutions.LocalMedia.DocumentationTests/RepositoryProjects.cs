// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The repository's own project files, without whatever else happens to live in the tree.
/// </summary>
/// <remarks>
/// A working copy carries more than the sources: a self-hosted runner unpacks third-party actions
/// under a dot-directory, and those ship sample projects with pinned-inline versions and no lock
/// file. Scanning from the root without filtering turned two green gates red on the machine that had
/// the runner and left them green in CI, which is the worst way for a gate to be wrong. Everything
/// git ignores starts with a dot at the top level, so that is the line.
/// <para>
/// This lived on the project's own copy of <c>RepositoryLayout</c> until ARQ-012 left one anchor for
/// every test project. Finding the root is shared; deciding which projects count is this suite's.
/// </para>
/// </remarks>
internal static class RepositoryProjects
{
    public static string[] All() =>
    [
        .. Directory.EnumerateFiles(RepositoryLayout.Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(RepositoryLayout.Root, path).StartsWith('.'))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase)),
    ];
}
