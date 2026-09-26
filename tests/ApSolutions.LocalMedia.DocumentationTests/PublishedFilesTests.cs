// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// What the documentation sweeps call "published", measured against a repository made for the
/// purpose rather than against this one.
/// </summary>
/// <remarks>
/// Asked of this checkout, the answer changes with whatever anyone has left uncommitted, so a case
/// that must stay silent could never be believed. A fabricated repository holds one file of each
/// kind that matters and nothing else.
/// </remarks>
public sealed class PublishedFilesTests
{
    [Fact]
    public void Published_is_what_git_follows_or_would_follow_and_not_what_it_is_told_to_leave_out()
    {
        var root = Path.Combine(Path.GetTempPath(), "published-files-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            Git(root, "init", "--quiet");
            Write(root, ".gitignore", "*.log\n");
            Write(root, "tracked.md", "kept\n");
            Write(root, "left-out.md", "tracked, then excluded on this machine\n");
            Write(root, "new.md", "written and not yet added\n");
            Write(root, "build.log", "ignored by the published rules\n");
            Write(root, "gone.md", "tracked and then deleted from the disk\n");
            Git(root, "add", ".gitignore", "tracked.md", "left-out.md", "gone.md");
            File.Delete(Path.Combine(root, "gone.md"));
            Write(root, Path.Combine(".git", "info", "exclude"), "/left-out.md\n");

            var published = PublishedFiles.Under(root)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal([".gitignore", "new.md", "tracked.md"], published);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Without_a_repository_the_disk_is_the_answer()
    {
        var root = Path.Combine(Path.GetTempPath(), "published-files-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            Write(root, "a.md", "a\n");
            Write(root, Path.Combine("docs", "b.md"), "b\n");

            Assert.Equal(2, PublishedFiles.Under(root).Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-c", "safe.directory=*", "-C", root, .. arguments])
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {errors.Result}");
    }
}
