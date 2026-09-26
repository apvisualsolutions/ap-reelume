// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Privacy;

/// <summary>
/// This repository is public, and publishing is not reversible.
/// </summary>
/// <remarks>
/// A check for personal data that somebody has to remember to run finds what that person was already
/// thinking about. One such pass replaced a show's title in its Spanish form and left the English one,
/// because the pattern being searched for was written in Spanish.
/// <para>
/// Nothing personal is written down here. Every pattern is derived from the machine the suite runs
/// on — the account name, the computer name, the profile path, and the names of the folders sitting
/// beside the repository that git has been told to ignore. Those ignored folders are the personal
/// library: if one of their names turns up in a published file, something leaked out of them.
/// </para>
/// <para>
/// <b>What is read is what git publishes, not what the disk holds.</b> A working copy also holds
/// build output, the library itself and whatever a machine keeps out with its own exclude rules;
/// none of that reaches a clone, and a sweep of the disk would report it or, worse, would be the
/// only thing looking at a file while the file that actually ships goes unread.
/// </para>
/// <para>
/// What this cannot do is recognise a translation. A show named in one language in a folder and in
/// another inside a fixture is invisible to it. That part is still a person's judgement, and saying
/// so is better than implying the check is total.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed partial class RepositoryPrivacyTests
{
    /// <summary>
    /// The folders of the working copy that belong to the repository, published or not. Any other
    /// folder at the root is read as somebody's own media.
    /// </summary>
    /// <remarks>
    /// A folder that belongs to the working copy and is missing here is read as a library folder, and
    /// then every published file that happens to contain its name is reported — which is the check
    /// working: an unknown directory beside the repository is exactly what it exists to notice.
    /// Adding one here is the statement that it belongs to the repository.
    /// </remarks>
    private static readonly string[] OwnDirectories =
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

        var leaks = VersionedTexts()
            .SelectMany(file => forbidden
                .Where(entry => entry.Value.Length > 3
                    && file.Text.Contains(entry.Value, StringComparison.OrdinalIgnoreCase))
                .Select(entry => $"{Relative(file.Path)} carries {entry.Kind}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(leaks.Length == 0, string.Join("\n", leaks));
    }

    /// <summary>
    /// The folders beside the repository that git ignores are somebody's media. Their names must not
    /// appear in anything published — not in documentation, and not as an example in a test, which
    /// is where the last one was found.
    /// </summary>
    [Fact]
    public void No_versioned_file_names_a_folder_of_the_personal_library()
    {
        var ignored = IgnoredNeighbours();
        Assert.SkipWhen(ignored.Count == 0, "There is no ignored folder beside the repository to check against.");

        var leaks = VersionedTexts()
            .SelectMany(file => ignored
                .Where(name => file.Text.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(_ => $"{Relative(file.Path)} names one of the ignored folders beside the repository"))
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

        // Hundreds of files are published: the sources alone are more than this. Fewer means the
        // sweep is reading an empty or truncated list and agreeing with everything in it.
        var versioned = VersionedFiles();
        Assert.True(versioned.Count >= 500, $"only {versioned.Count} published text files were read.");
        Assert.Contains(versioned, file => string.Equals(
            Path.GetFullPath(file), Path.GetFullPath(RepositoryLayout.SolutionPath), StringComparison.OrdinalIgnoreCase));

        // And the search itself works: the repository path is in this file's own location.
        var probe = Path.Combine(RepositoryLayout.Root, "docs");
        Assert.Contains(RepositoryLayout.Root, probe, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing the ignore rules exclude is still published.
    /// </summary>
    /// <remarks>
    /// A file that git follows keeps being published after a rule starts ignoring it: ignoring only
    /// stops a file from being added, never removes one. So a path somebody decided to keep out of
    /// the repository is only out of it once it has also left the index, and until then every push
    /// publishes it whole. That is the most serious leak there is, because nothing in the file has to
    /// be wrong for it to happen.
    /// </remarks>
    [Fact]
    public void Nothing_the_ignore_rules_exclude_is_still_published()
    {
        Assert.SkipUnless(IsRepository(RepositoryLayout.Root), "This tree is not a git checkout, so nothing is published from it.");

        var excluded = Git(RepositoryLayout.Root, "ls-files", "-z", "--cached", "--ignored", "--exclude-standard");

        Assert.True(
            excluded.Length == 0,
            $"{excluded.Length} file(s) are excluded by the ignore rules and still in the index, so they "
                + "are published anyway. Remove them from the index: "
                + string.Join(", ", excluded.Take(20)));
    }

    /// <summary>
    /// The two rules above against a repository made for them: one file published normally, one
    /// kept in the index after a rule excluded it, and one written and not yet added.
    /// </summary>
    [Fact]
    public void The_published_list_is_what_a_clone_would_hold()
    {
        var root = Path.Combine(Path.GetTempPath(), "repository-privacy-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            Git(root, "init", "--quiet");
            File.WriteAllText(Path.Combine(root, "kept.md"), "kept\n");
            File.WriteAllText(Path.Combine(root, "left-in.md"), "excluded and never removed\n");
            File.WriteAllText(Path.Combine(root, "new.md"), "not yet added\n");
            Git(root, "add", "kept.md", "left-in.md");
            File.WriteAllText(Path.Combine(root, ".git", "info", "exclude"), "/left-in.md\n");

            Assert.Equal(
                ["kept.md", "new.md"],
                PublishedFiles(root).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray());
            Assert.Equal(["left-in.md"], Git(root, "ls-files", "-z", "--cached", "--ignored", "--exclude-standard"));
        }
        finally
        {
            // Git writes its objects read-only, and a recursive delete refuses a read-only file.
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    // A check for mapped network drives was written here once and taken out the same hour, and the
    // attempt is worth more than the check would have been. A drive letter derived from this machine
    // is too poor a pattern to be one: the first version matched every URL in the tree, because
    // "https://" ends in "s:/"; anchored to the start of a path it still matched half a dozen test
    // fixtures that use the same letter as an invented path. There is no way to tell "the share this
    // office maps" from "a letter somebody typed into a fixture", so it would have been a gate that
    // cries on honest work, and a noisy guard is one that gets switched off.

    /// <summary>
    /// The folders beside the repository that are not its own. They are the personal library: git
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
                && !OwnDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Every published text file, root files included. The root used to be read for two extensions
    /// only, and the one file that carried a company host out was a JSON file sitting right there and
    /// never opened: a sweep that names extensions one by one is a filter for what somebody thought of.
    /// </summary>
    private static List<string> VersionedFiles() =>
        [.. PublishedFiles(RepositoryLayout.Root).Where(IsText)];

    /// <summary>Each published text file read once, rather than once per value looked for.</summary>
    private static IEnumerable<(string Path, string Text)> VersionedTexts() =>
        VersionedFiles().Select(path => (path, File.ReadAllText(path)));

    /// <summary>
    /// What git follows or would follow if added now, minus what the ignore rules exclude although it
    /// is still in the index. Without a repository around the tree, the disk is all there is.
    /// </summary>
    private static List<string> PublishedFiles(string root)
    {
        if (!IsRepository(root))
        {
            return [.. Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(file => !Skipped.Any(skipped =>
                    file.Contains($"{Path.DirectorySeparatorChar}{skipped}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))];
        }

        var excluded = Git(root, "ls-files", "-z", "--cached", "--ignored", "--exclude-standard")
            .ToHashSet(StringComparer.Ordinal);
        return [.. Git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard")
            .Where(relative => !excluded.Contains(relative))
            .Distinct(StringComparer.Ordinal)
            .Select(relative => Path.GetFullPath(Path.Combine(root, relative)))
            .Where(File.Exists)];
    }

    private static bool IsRepository(string root) =>
        Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git"));

    private static string[] Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-c", "safe.directory=*", "-c", "core.quotepath=off", "-C", root, .. arguments])
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git could not be started, so nothing is known about what is published.");
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // A git that failed would otherwise read as a repository with nothing in it.
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} exited {process.ExitCode}: {errors.Result}");
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsText(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" or ".md" or ".json" or ".xml" or ".yml" or ".yaml" or ".ps1" or ".axaml"
            or ".csproj" or ".props" or ".targets" or ".appxmanifest" or ".editorconfig" or ".sln"
            or ".txt" or ".sh" or ".sql" or ".runsettings" or ".gitignore" or ".gitattributes" => true,
        _ => false,
    };

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
}
