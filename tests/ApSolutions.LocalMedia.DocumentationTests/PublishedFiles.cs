// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The files this checkout would publish, which is not the same as the files on the disk.
/// </summary>
/// <remarks>
/// <para>
/// A working copy holds more than the repository publishes: build output, the media library a
/// person keeps beside the sources, and anything a machine chooses to keep out of the repository
/// with its own exclude rules. A documentation sweep that reads the disk answers a different
/// question on every machine, and the one that matters is the question a clone answers.
/// </para>
/// <para>
/// So the answer is git's: what it follows plus what it would follow if added now, minus anything
/// that the ignore rules in force exclude even though it is still in the index. Untracked files are
/// part of it on purpose — a document written and not yet committed is exactly the one a sweep has
/// to see before it is committed. Without a repository around it (an unpacked source archive) the
/// disk is all there is, and then the disk is the answer.
/// </para>
/// </remarks>
internal static class PublishedFiles
{
    private static readonly Lazy<IReadOnlyList<string>> Cached = new(() => Under(RepositoryLayout.Root));

    /// <summary>The published files of this checkout, as absolute paths.</summary>
    public static IReadOnlyList<string> All => Cached.Value;

    /// <summary>The published files of any directory, which is how the tests fabricate a repository.</summary>
    public static IReadOnlyList<string> Under(string root)
    {
        if (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git")))
        {
            return [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)];
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

    /// <summary>The published files under one folder of the checkout, matching a suffix.</summary>
    public static IEnumerable<string> Documents(string folder, string suffix)
    {
        var prefix = RepositoryLayout.PathFromRoot(folder) + Path.DirectorySeparatorChar;
        return All.Where(path =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

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
            ?? throw new InvalidOperationException("git could not be started, so nothing is known about what this checkout publishes.");
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // A git that failed and printed nothing would read as a repository with nothing in it, and a
        // sweep over nothing agrees with everything.
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} exited {process.ExitCode}: {errors.Result}");
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
