// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.DocumentationTests;

internal static class RepositoryLayout
{
    public static string Root { get; } = FindRoot();

    public static string PathFromRoot(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// The repository's own project files, without whatever else happens to live in the tree.
    /// </summary>
    /// <remarks>
    /// A working copy carries more than the sources: a self-hosted runner unpacks third-party actions
    /// under a dot-directory, and those ship sample projects with pinned-inline versions and no lock
    /// file. Scanning from the root without filtering turned two green gates red on the machine that
    /// had the runner and left them green in CI, which is the worst way for a gate to be wrong.
    /// Everything git ignores starts with a dot at the top level, so that is the line.
    /// </remarks>
    public static string[] ProjectFiles() =>
    [
        .. Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(Root, path).StartsWith('.'))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase)),
    ];

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "FEATURES.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root containing docs/FEATURES.md was not found.");
    }
}
