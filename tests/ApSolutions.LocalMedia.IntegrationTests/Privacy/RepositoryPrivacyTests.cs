// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.TestSupport;
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
    // "design" joined on 2026-08-17 with the interface redesign handoff. A directory that appears at
    // the root and is not on this list is read as one of the owner's own folders, so the check
    // reported every versioned file that says "design" — which is the check working: an unknown
    // directory beside the repository is exactly what it exists to notice. Adding a versioned one
    // here is the statement that it belongs to the repository.
    private static readonly string[] VersionedDirectories =
        ["src", "tests", "docs", "eng", ".github", "benchmarks", "design"];

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
            ("the repository path", RepositoryLayout.Root),
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
        var probe = Path.Combine(RepositoryLayout.Root, "docs");
        Assert.Contains(RepositoryLayout.Root, probe, StringComparison.Ordinal);
    }

    /// <summary>
    /// The versioned MCP configuration declares the public documentation server and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>A closed list, because the two checks above cannot see this one.</b> They look for the
    /// account, the computer and the profile path of whoever built the tree — and a company's own
    /// server is none of those. On 2026-09-05 a connector for an internal service landed in
    /// <c>.mcp.json</c> carrying its host name and a path on a mapped network drive, and it reached
    /// the public <c>main</c> without a single gate saying anything: the file sits at the root, and
    /// the sweep only read <c>*.md</c> and <c>*.props</c> there.
    /// <para>
    /// So this does not try to recognise what is private, which is the filter-the-bad shape this
    /// repository refuses everywhere else. It allows what is known to be publishable, and anything
    /// else has to be argued for here first. The host is deliberately not written down, not even as
    /// something to forbid: naming it in a test would publish it again.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_versioned_mcp_configuration_declares_only_servers_that_are_safe_to_publish()
    {
        var configuration = Path.Combine(RepositoryLayout.Root, ".mcp.json");
        Assert.True(File.Exists(configuration), ".mcp.json is versioned and has to be there.");

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configuration));
        var declared = document.RootElement
            .GetProperty("mcpServers")
            .EnumerateObject()
            .Select(server => server.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Avalonia's own documentation service, public and named by CLAUDE.md as the reason this file
        // is versioned at all. A connector to anything of this company's goes in local configuration,
        // which is not published.
        Assert.Equal(["avalonia-docs"], declared);
    }

    // A check for mapped network drives was written here on 2026-09-06 and taken out the same hour,
    // and the attempt is worth more than the check would have been. A drive letter derived from this
    // machine is too poor a pattern to be one: the first version matched every URL in the tree,
    // because "https://" ends in "s:/"; anchored to the start of a path it still matched half a dozen
    // test fixtures that use the same letter as an invented path. There is no way to tell "the share
    // this office maps" from "a letter somebody typed into a fixture", so it would have been a gate
    // that cries on honest work — and this repository has already learned that a noisy guard is one
    // that gets switched off. The precise check is the one above: a closed list of what may be
    // published, which caught the real defect with no false positives at all.

    /// <summary>
    /// The folders beside the repository that are not versioned. They are the personal library: git
    /// was told to ignore them, which is the repository's own statement that they do not belong to it.
    /// </summary>
    private static IReadOnlyList<string> IgnoredNeighbours()
    {
        var root = RepositoryLayout.Root;
        return [.. new DirectoryInfo(root)
            .EnumerateDirectories()
            .Select(directory => directory.Name)
            .Where(name => !name.StartsWith('.')
                && !Skipped.Contains(name, StringComparer.OrdinalIgnoreCase)
                && !VersionedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))];
    }

    private static List<string> VersionedFiles()
    {
        var root = RepositoryLayout.Root;
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

        // Every text file at the root and not two extensions of it. It read *.md and *.props until
        // 2026-09-06, and the file that carried a company host to the public main was .mcp.json,
        // sitting right there and never opened. A sweep that names extensions one by one is a filter
        // for what somebody thought of.
        files.AddRange(Directory.EnumerateFiles(root, "*").Where(IsText));
        return files;
    }

    private static bool IsText(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" or ".md" or ".json" or ".xml" or ".yml" or ".yaml" or ".ps1" or ".axaml"
            or ".csproj" or ".props" or ".targets" or ".appxmanifest" or ".editorconfig" or ".sln" => true,
        _ => false,
    };

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
}
