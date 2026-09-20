// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// Guards ENG-016: <c>eng/preview-coverage-floors.ps1</c> has to see every source file CI will
/// measure as new, and a commit range does not — it only names what is already IN a commit, which
/// is precisely what has not happened yet when somebody runs a preview.
/// </summary>
/// <remarks>
/// <para>
/// The silence was measured twice and cost a red the second time. On 2026-09-13, with three
/// uncommitted new files, the script said nothing fell short while two of the three were under
/// 96/96. On 2026-09-19, with <c>TitleFramePass.cs</c> new and uncommitted, it named one short floor
/// and said nothing about the new file reading 100/50 — which is what took run 35471110732 down.
/// </para>
/// <para>
/// The scene is a lying repository rather than this one BECAUSE OF WHAT IT HAS TO ASSERT. Asked
/// against the real tree the answer changes with whatever anybody happens to have uncommitted, so
/// the negative case could never be trusted; and an assertion that read the script for the words
/// "ls-files" would stay green while the query it names ran nowhere, which is the blind gate this
/// repository keeps finding. Every case therefore has its opposite beside it: a search that always
/// finds something is no search.
/// </para>
/// </remarks>
public sealed class PreviewCoverageFloorsTests
{
    [Fact]
    public void A_new_file_that_git_does_not_track_yet_is_listed()
    {
        using var scene = new PreviewScene();
        scene.WriteSource("Untracked.cs");

        Assert.Equal(["src/Untracked.cs"], scene.ListNewFiles());
    }

    [Fact]
    public void A_new_file_that_is_staged_but_not_committed_is_listed()
    {
        using var scene = new PreviewScene();
        scene.WriteSource("Staged.cs");
        scene.Git("add src/Staged.cs");

        Assert.Equal(["src/Staged.cs"], scene.ListNewFiles());
    }

    /// <summary>
    /// The source the script always had. Widening the search must not lose it, and nothing else in
    /// this file would notice if it did.
    /// </summary>
    [Fact]
    public void A_new_file_already_committed_against_the_base_ref_is_still_listed()
    {
        using var scene = new PreviewScene();
        scene.WriteSource("Committed.cs");
        scene.Git("add src/Committed.cs");
        scene.Git("-c commit.gpgsign=false commit --quiet -m added");

        Assert.Equal(["src/Committed.cs"], scene.ListNewFiles());
    }

    /// <summary>
    /// The control. Without it the three cases above pass just as well against a script that
    /// answers with every file it can find, or with a constant.
    /// </summary>
    [Fact]
    public void A_tree_with_no_new_source_lists_nothing()
    {
        using var scene = new PreviewScene();

        Assert.Empty(scene.ListNewFiles());
    }

    /// <summary>
    /// A file that is not source does not become one by being new: the script measures coverage,
    /// and there is none to measure here.
    /// </summary>
    [Fact]
    public void A_new_file_that_is_not_csharp_source_is_not_listed()
    {
        using var scene = new PreviewScene();
        scene.WriteSource("notes.md", "# nothing to cover");

        Assert.Empty(scene.ListNewFiles());
    }
}

/// <summary>
/// A repository built to be lied to: one base commit, and whatever each case adds on top. The
/// script under test is the one this repository ships — only the tree it is asked about is fake.
/// </summary>
public sealed class PreviewScene : IDisposable
{
    private readonly string _root;

    public PreviewScene()
    {
        _root = Path.Combine(Path.GetTempPath(), "preview-floors-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(Path.Combine(_root, "src"));

        Git("init --quiet --initial-branch=scene");
        Git("config user.email scene@example.invalid");
        Git("config user.name scene");
        Git("-c commit.gpgsign=false commit --quiet --allow-empty -m base");
        BaseSha = Git("rev-parse HEAD").Trim();
    }

    /// <summary>The commit every case measures against, standing in for origin/main.</summary>
    public string BaseSha { get; }

    public void WriteSource(string name, string? body = null) =>
        File.WriteAllText(
            Path.Combine(_root, "src", name),
            body ?? "// SPDX-License-Identifier: LicenseRef-APSolutions" + Environment.NewLine);

    /// <summary>
    /// Runs the real script with -ListNewFiles, which is the seam that lets this be measured by
    /// effect instead of by reading the file.
    /// </summary>
    public string[] ListNewFiles()
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(RepositoryLayout.PathFromRoot("eng/preview-coverage-floors.ps1"));
        start.ArgumentList.Add("-ListNewFiles");
        start.ArgumentList.Add("-BaseRef");
        start.ArgumentList.Add(BaseSha);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("pwsh did not start; the scene cannot be measured without it.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        Assert.True(
            process.ExitCode == 0,
            $"preview-coverage-floors.ps1 -ListNewFiles exited {process.ExitCode}: {stderr}");

        return stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    public string Git(string arguments)
    {
        var start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git did not start; the scene cannot be built without it.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);
        Assert.True(process.ExitCode == 0, $"git {arguments} failed: {stderr}");
        return stdout;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
