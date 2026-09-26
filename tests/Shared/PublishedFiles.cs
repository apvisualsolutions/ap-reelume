// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;

namespace ApSolutions.LocalMedia.TestSupport;

/// <summary>
/// The files this checkout would publish, which is not the same as the files on the disk.
/// </summary>
/// <remarks>
/// <para>
/// A working copy holds more than the repository publishes: build output, the media library a
/// person keeps beside the sources, and anything a machine chooses to keep out of the repository
/// with its own exclude rules. A sweep that reads the disk answers a different question on every
/// machine, and the one that matters is the question a clone answers.
/// </para>
/// <para>
/// So the answer is git's: what it follows plus what it would follow if added now, minus anything
/// that the ignore rules in force exclude even though it is still in the index. Untracked files are
/// part of it on purpose — a document written and not yet committed is exactly the one a sweep has
/// to see before it is committed. Without a repository around it (an unpacked source archive) the
/// disk is all there is, less the build output nobody publishes.
/// </para>
/// <para>
/// <b>One definition, linked into every suite that asks.</b> There were three — the documentation
/// sweeps, the privacy sweep and <c>eng/verify-docs.ps1</c> — and they had already drifted: one
/// counted a file the ignore rules exclude as published, and two disagreed about build output
/// outside a repository. The script cannot link a C# file, so it spells the same rule out and a
/// scene in the documentation suite holds it to it.
/// </para>
/// </remarks>
internal static class PublishedFiles
{
    /// <summary>What nobody publishes, skipped when there is no repository to ask.</summary>
    private static readonly string[] BuildOutput = ["bin", "obj", "artifacts", ".git", ".vs", ".vendor", "node_modules"];

    private static readonly Lazy<IReadOnlyList<string>> Cached = new(() => Under(RepositoryLayout.Root));

    /// <summary>The published files of this checkout, as absolute paths.</summary>
    public static IReadOnlyList<string> All => Cached.Value;

    /// <summary>The published files of any directory, which is how the tests fabricate a repository.</summary>
    public static IReadOnlyList<string> Under(string root)
    {
        if (!IsRepository(root))
        {
            return
            [
                .. Directory
                    .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(file => !Path.GetRelativePath(root, file)
                        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Any(segment => BuildOutput.Contains(segment, StringComparer.OrdinalIgnoreCase))),
            ];
        }

        var candidates = Git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard");
        var excluded = Git(root, "ls-files", "-z", "--cached", "--ignored", "--exclude-standard")
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. candidates
                .Where(relative => !excluded.Contains(relative))
                .Distinct(StringComparer.Ordinal)
                .Select(relative => Path.GetFullPath(Path.Combine(root, relative)))
                .Where(File.Exists),
        ];
    }

    /// <summary>Whether git has a repository at this directory, as a folder or as a worktree's file.</summary>
    public static bool IsRepository(string root) =>
        Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git"));

    /// <summary>
    /// Runs git in <paramref name="root"/> and hands back its NUL-separated answer. A git that fails
    /// throws: one that failed and printed nothing would read as a repository with nothing in it, and
    /// a sweep over nothing agrees with everything.
    /// </summary>
    public static string[] Git(string root, params string[] arguments)
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
            ?? throw new InvalidOperationException("git could not be started, so nothing is known about what this checkout publishes.");
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} exited {process.ExitCode}: {errors.Result}");
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
