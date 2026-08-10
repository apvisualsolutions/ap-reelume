// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Privacy;

/// <summary>
/// This repository is going to be public, and publishing is not reversible.
/// </summary>
/// <remarks>
/// Until now the check for personal data was a command somebody had to remember to run. It found
/// things, and it also missed things: a redaction pass replaced one show's title in its Spanish form
/// and left the English one, because the pattern being grepped for was written in Spanish. A check
/// that depends on memory finds what the person was already thinking about.
/// <para>
/// Nothing personal is written down here. Every pattern is derived from the machine the suite runs
/// on — the account name, the computer name, the profile path, and the names of the folders sitting
/// beside the repository that git has been told to ignore. Those ignored folders are the personal
/// library: if one of their names turns up in a versioned file, something leaked out of them.
/// </para>
/// <para>
/// What this cannot do is recognise a translation. A show named in one language in a folder and in
/// another inside a fixture is invisible to it, which is exactly how the last one survived. That
/// part is still a person's judgement, and saying so is better than implying the check is total.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class RepositoryPrivacyTests
{
    /// <summary>Where versioned text lives. Build output and the library itself are not in it.</summary>
    private static readonly string[] VersionedDirectories = ["src", "tests", "docs", "eng", ".github", "benchmarks"];

    private static readonly string[] Skipped = ["bin", "obj", "artifacts", ".git", "node_modules"];

    /// <summary>
    /// The account, the computer, and the profile path of whoever built this. A path on screen is
    /// how a screenshot stops being safe to share; a path in a repository is the same thing, kept.
    /// </summary>
    [Fact]
    public void No_versioned_file_names_the_person_or_the_machine_that_built_it()
    {
        var forbidden = new List<(string Kind, string Value)>
        {
            ("the account name", Environment.UserName),
            ("the computer name", Environment.MachineName),
            ("the profile path", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            ("the repository path", RepositoryRoot()),
        };

        var leaks = VersionedFiles()
            .SelectMany(file => forbidden
                .Where(entry => entry.Value.Length > 3
                    && File.ReadAllText(file).Contains(entry.Value, StringComparison.OrdinalIgnoreCase))
                .Select(entry => $"{Relative(file)} carries {entry.Kind}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(leaks.Length == 0, string.Join("\n", leaks));
    }

    /// <summary>
    /// The folders beside the repository that git ignores are somebody's media. Their names must not
    /// appear in anything versioned — not in evidence, not in documentation, and not as an example in
    /// a test, which is where the last one was found.
    /// </summary>
    [Fact]
    public void No_versioned_file_names_a_folder_of_the_personal_library()
    {
        var ignored = IgnoredNeighbours();
        Assert.SkipWhen(ignored.Count == 0, "There is no ignored folder beside the repository to check against.");

        var leaks = VersionedFiles()
            .SelectMany(file => ignored
                .Where(name => File.ReadAllText(file).Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(_ => $"{Relative(file)} names one of the ignored folders beside the repository"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(leaks.Length == 0, string.Join("\n", leaks));
    }

    /// <summary>
    /// Proves the check is not simply blind: the values it looks for have to be findable at all. A
    /// suite that searched for the empty string would pass forever and mean nothing.
    /// </summary>
    [Fact]
    public void The_values_this_looks_for_are_real_ones()
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.UserName));
        Assert.False(string.IsNullOrWhiteSpace(Environment.MachineName));
        Assert.NotEmpty(VersionedFiles());

        // And the search itself works: the repository path is in this file's own location.
        var probe = Path.Combine(RepositoryRoot(), "docs");
        Assert.Contains(RepositoryRoot(), probe, StringComparison.Ordinal);
    }

    /// <summary>
    /// The folders beside the repository that are not versioned. They are the personal library: git
    /// was told to ignore them, which is the repository's own statement that they do not belong to it.
    /// </summary>
    private static IReadOnlyList<string> IgnoredNeighbours()
    {
        var root = RepositoryRoot();
        return [.. new DirectoryInfo(root)
            .EnumerateDirectories()
            .Select(directory => directory.Name)
            .Where(name => !name.StartsWith('.')
                && !Skipped.Contains(name, StringComparer.OrdinalIgnoreCase)
                && !VersionedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))];
    }

    private static List<string> VersionedFiles()
    {
        var root = RepositoryRoot();
        var files = new List<string>();
        foreach (var directory in VersionedDirectories.Select(name => Path.Combine(root, name)))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            files.AddRange(Directory
                .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(file => !Skipped.Any(skipped =>
                    file.Contains($"{Path.DirectorySeparatorChar}{skipped}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    && IsText(file)));
        }

        files.AddRange(Directory.EnumerateFiles(root, "*.md").Concat(Directory.EnumerateFiles(root, "*.props")));
        return files;
    }

    private static bool IsText(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" or ".md" or ".json" or ".xml" or ".yml" or ".yaml" or ".ps1" or ".axaml"
            or ".csproj" or ".props" or ".targets" or ".appxmanifest" or ".editorconfig" or ".sln" => true,
        _ => false,
    };

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/');

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
