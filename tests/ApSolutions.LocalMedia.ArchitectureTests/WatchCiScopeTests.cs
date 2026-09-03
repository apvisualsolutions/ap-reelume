// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// A checkout whose local branch is not the branch its commit was pushed to, with a <c>gh</c> that
/// answers the way the real one was measured to answer. It is the shape every worktree has.
/// </summary>
/// <remarks>
/// The stub is not a convenience: it is the only way to hold the watcher to a behaviour that
/// otherwise needs the network, an authenticated <c>gh</c> and a run that happens to be in the
/// right state. Its three answers are each a measurement of the real thing, taken 2026-09-02
/// against this repository:
/// <list type="bullet">
/// <item><c>--commit</c> with all forty characters returns the run — for <c>in_progress</c>,
/// <c>success</c> and <c>failure</c> alike. A note in circulation said this filter never returns
/// anything here; it was wrong.</item>
/// <item><c>--commit</c> with a prefix returns <c>[]</c> and exits <b>0</b>. That is the trap: it
/// has exactly the shape of "no run yet", and the push hook hands out short shas.</item>
/// <item><c>--branch</c> naming a branch with no runs returns <c>[]</c> and exits 0 too.</item>
/// </list>
/// </remarks>
public sealed class WatchCiScene : IDisposable
{
    /// <summary>The branch the worktree is on. ci.yml triggers on codex/**, so it has no runs.</summary>
    public const string LocalBranch = "claude/local-only-9f2c1a";

    /// <summary>The branch the commit was pushed to, and the only one the stub knows runs for.</summary>
    public const string PushedBranch = "codex/pushed-somewhere-else";

    private readonly string _root;

    public WatchCiScene()
    {
        _root = Path.Combine(Path.GetTempPath(), "watch-ci-scene-" + Guid.NewGuid().ToString("n")[..12]);
        RepoPath = Path.Combine(_root, "repo");
        Directory.CreateDirectory(RepoPath);
        Directory.CreateDirectory(Path.Combine(_root, "bin"));

        Git("init --quiet --initial-branch=" + LocalBranch);
        Git("config user.email scene@example.invalid");
        Git("config user.name scene");
        Git("-c commit.gpgsign=false commit --quiet --allow-empty -m scene");
        FullSha = Git("rev-parse HEAD").Trim();
        ShortSha = FullSha[..7];

        File.WriteAllText(
            Path.Combine(_root, "bin", "gh.cmd"),
            string.Join(
                "\r\n",
                "@echo off",
                "echo %*>>\"%GH_STUB_LOG%\"",
                // `run view <id> --json jobs` carries no --commit and would otherwise fall through
                // to the run list, so it is answered first.
                "echo %*| findstr /C:\"--json jobs\" >nul",
                "if %errorlevel%==0 (type \"%GH_STUB_JOBS%\" & exit /b 0)",
                "echo %*| findstr /C:\"--commit %GH_STUB_SHA%\" >nul",
                "if %errorlevel%==0 (type \"%GH_STUB_RUNS%\" & exit /b 0)",
                "echo %*| findstr /C:\"--commit \" >nul",
                "if %errorlevel%==0 (echo [] & exit /b 0)",
                "echo %*| findstr /C:\"--branch %GH_STUB_BRANCH%\" >nul",
                "if %errorlevel%==0 (type \"%GH_STUB_RUNS%\" & exit /b 0)",
                "echo %*| findstr /C:\"--branch \" >nul",
                "if %errorlevel%==0 (echo [] & exit /b 0)",
                "type \"%GH_STUB_RUNS%\"",
                "exit /b 0",
                string.Empty));
    }

    public string RepoPath { get; }

    public string FullSha { get; }

    public string ShortSha { get; }

    /// <summary>The run body the stub serves wherever it serves a run at all.</summary>
    public string CompletedRun(string conclusion) =>
        $"[{{\"conclusion\":\"{conclusion}\",\"databaseId\":42,\"headSha\":\"{FullSha}\",\"status\":\"completed\"}}]";

    /// <summary>A run that is still going, which is the only state step events can be seen in.</summary>
    public string RunningRun() =>
        $"[{{\"conclusion\":\"\",\"databaseId\":42,\"headSha\":\"{FullSha}\",\"status\":\"in_progress\"}}]";

    /// <summary>One job with the steps given, shaped the way `gh run view --json jobs` returns them.</summary>
    public static string Jobs(params (int Number, string Status, string Conclusion, string Name)[] steps)
    {
        var body = string.Join(
            ",",
            steps.Select(step =>
                $"{{\"number\":{step.Number},\"status\":\"{step.Status}\",\"conclusion\":\"{step.Conclusion}\",\"name\":\"{step.Name}\"}}"));

        return $"{{\"jobs\":[{{\"name\":\"verify\",\"steps\":[{body}]}}]}}";
    }

    /// <summary>
    /// Runs the real script against the scene. -PollSeconds 0 and -MissingLimit 1 collapse the
    /// waiting; nothing else about the script is changed, because the point is to measure the
    /// script the repository actually ships.
    /// </summary>
    public WatchResult Watch(
        string sha,
        string? branch,
        string runsJson,
        string? jobsJson = null,
        bool noStepEvents = false,
        int? timeoutMinutes = null)
    {
        var runsPath = Path.Combine(_root, "runs.json");
        var jobsPath = Path.Combine(_root, "jobs.json");
        var logPath = Path.Combine(_root, "gh-calls.log");
        File.WriteAllText(runsPath, runsJson + Environment.NewLine);
        File.WriteAllText(jobsPath, (jobsJson ?? "{\"jobs\":[]}") + Environment.NewLine);
        File.Delete(logPath);

        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = RepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(RepositoryLayout.PathFromRoot("eng/watch-ci.ps1"));
        start.ArgumentList.Add("-Sha");
        start.ArgumentList.Add(sha);
        start.ArgumentList.Add("-PollSeconds");
        start.ArgumentList.Add("0");
        start.ArgumentList.Add("-MissingLimit");
        start.ArgumentList.Add("1");
        start.ArgumentList.Add("-HeartbeatMinutes");
        start.ArgumentList.Add("0");
        if (branch is not null)
        {
            start.ArgumentList.Add("-Branch");
            start.ArgumentList.Add(branch);
        }

        if (noStepEvents)
        {
            start.ArgumentList.Add("-NoStepEvents");
        }

        if (timeoutMinutes is not null)
        {
            start.ArgumentList.Add("-TimeoutMinutes");
            start.ArgumentList.Add(timeoutMinutes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        start.Environment["PATH"] = Path.Combine(_root, "bin") + Path.PathSeparator + start.Environment["PATH"];
        start.Environment["GH_STUB_LOG"] = logPath;
        start.Environment["GH_STUB_RUNS"] = runsPath;
        start.Environment["GH_STUB_JOBS"] = jobsPath;
        start.Environment["GH_STUB_SHA"] = FullSha;
        start.Environment["GH_STUB_BRANCH"] = PushedBranch;

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("pwsh did not start; the watcher cannot be measured without it.");
        var output = new StringBuilder();
        output.Append(process.StandardOutput.ReadToEnd());
        output.Append(process.StandardError.ReadToEnd());
        Assert.True(
            process.WaitForExit(120_000),
            "eng/watch-ci.ps1 did not finish in two minutes against the scene, which means it is looping.");

        var calls = File.Exists(logPath) ? File.ReadAllLines(logPath) : [];
        return new WatchResult(process.ExitCode, output.ToString().Trim(), calls);
    }

    public void Dispose()
    {
        try
        {
            // git leaves read-only objects behind, and a scene that cannot be deleted is not a
            // failure of what is being measured.
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string Git(string arguments)
    {
        var start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = RepoPath,
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

    public sealed record WatchResult(int ExitCode, string Output, string[] GhCalls);
}

/// <summary>
/// Holds eng/watch-ci.ps1 to looking where the run is, rather than where the checkout happens to be
/// standing.
/// </summary>
/// <remarks>
/// Written 2026-09-02 from a false negative in the field. A worktree on
/// <c>claude/goofy-aryabhata-1e2f4a</c> had pushed its commit to
/// <c>codex/shell-assembly-isolation</c>; the watcher listed runs with <c>--branch</c>, defaulting
/// to the local branch, found none — ci.yml only triggers on codex/** — and announced that the push
/// had not triggered the workflow. The run existed and was <c>in_progress</c>.
/// <para>
/// That is a worse failure than the silence the script was written against, and the script says so
/// itself: a silent watcher is indistinguishable from a run that is still going, so it gets waited
/// on; a confident wrong answer gets acted on, and what gets acted on here is "CI never ran".
/// </para>
/// <para>
/// Both repairs are measured by mutation rather than asserted from the source, because the source
/// can be right and the behaviour wrong: reverting the default to the local branch, and handing
/// <c>gh</c> the short sha instead of resolving it, each put the false negative back — measured the
/// same day, both answering <c>NO RUN EXISTS</c> against a scene that held a run.
/// </para>
/// </remarks>
public sealed class WatchCiScopeTests(WatchCiScene scene) : IClassFixture<WatchCiScene>
{
    /// <summary>
    /// A step that finishes while the run is still going is announced when it finishes, not when the
    /// run ends.
    /// </summary>
    /// <remarks>
    /// The whole point of the step events, added 2026-09-03: this workflow's heaviest step runs for
    /// over half an hour, and a failure inside it used to be knowable only from the run's own
    /// conclusion forty minutes later.
    /// <para>
    /// <b>Steps and not jobs, because jobs say nothing here.</b> Measured against a live run that
    /// day: this workflow has exactly one job, so a job-level event lands in the same second the run
    /// ends. The thirteen real gates are steps, and <c>gh run view --json jobs</c> returns each one
    /// with its own status while the run is still <c>in_progress</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_step_that_finishes_is_announced_before_the_run_ends()
    {
        var result = scene.Watch(
            scene.ShortSha,
            branch: null,
            scene.RunningRun(),
            WatchCiScene.Jobs(
                (1, "completed", "success", "Set up job"),
                (5, "completed", "success", "Verify both architectures"),
                (6, "in_progress", string.Empty, "Accessibility gate")),
            timeoutMinutes: 2);

        Assert.Contains("step ok 'Verify both architectures'", result.Output, StringComparison.Ordinal);

        // The scaffolding is filtered while it passes: it is the same four lines on every run and
        // says nothing about this repository's code.
        Assert.DoesNotContain("Set up job", result.Output, StringComparison.Ordinal);

        // And a step that has not finished is not announced as if it had.
        Assert.DoesNotContain("Accessibility gate", result.Output, StringComparison.Ordinal);
    }

    /// <summary>A step is announced once, however many times the watcher looks at it.</summary>
    /// <remarks>
    /// The watcher polls once a minute for the better part of an hour, so a reader that reported
    /// what it saw rather than what had changed would emit the same line forty times. That is the
    /// noise this repository has already written down as the thing that teaches people to ignore an
    /// alert.
    /// </remarks>
    [Fact]
    public void A_step_is_announced_once_and_not_on_every_poll()
    {
        var result = scene.Watch(
            scene.ShortSha,
            branch: null,
            scene.RunningRun(),
            WatchCiScene.Jobs((5, "completed", "success", "Verify both architectures")),
            timeoutMinutes: 4);

        var announcements = Regex.Count(
            result.Output,
            "step ok 'Verify both architectures'",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        // Anti-blindness floor: a run that never announced anything would satisfy "not more than
        // once" by announcing nothing.
        Assert.True(announcements > 0, $"the step was never announced at all: {result.Output}");
        Assert.True(announcements == 1, $"the step was announced {announcements} times: {result.Output}");
    }

    /// <summary>
    /// A step that fails is announced whatever it is called, including the scaffolding the passing
    /// case filters out.
    /// </summary>
    /// <remarks>
    /// Scaffolding that fails is the run failing, and it is the one case where the noise is the
    /// news: a checkout that cannot check out produces a red run whose cause is in the step nobody
    /// wanted to hear about.
    /// </remarks>
    [Fact]
    public void A_failing_step_is_announced_even_when_it_is_scaffolding()
    {
        var result = scene.Watch(
            scene.ShortSha,
            branch: null,
            scene.RunningRun(),
            WatchCiScene.Jobs(
                (2, "completed", "failure", "Run actions/checkout@abc"),
                (5, "completed", "success", "Verify both architectures")),
            timeoutMinutes: 2);

        Assert.Contains("STEP FAILED 'Run actions/checkout@abc'", result.Output, StringComparison.Ordinal);
        Assert.Contains("failure", result.Output, StringComparison.Ordinal);
    }

    /// <summary>The step events can be turned off, and then nothing about steps is emitted.</summary>
    /// <remarks>
    /// Asserted because a switch nobody measures is a switch that stops working quietly, and this
    /// one is the escape hatch for a reader who wants the outcome and nothing else.
    /// </remarks>
    [Fact]
    public void NoStepEvents_emits_no_step_lines_at_all()
    {
        var result = scene.Watch(
            scene.ShortSha,
            branch: null,
            scene.CompletedRun("success"),
            WatchCiScene.Jobs((5, "completed", "success", "Verify both architectures")),
            noStepEvents: true);

        Assert.DoesNotContain("step ok", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("STEP FAILED", result.Output, StringComparison.Ordinal);

        // The outcome still arrives: turning off the progress must not turn off the verdict.
        Assert.Contains("success", result.Output, StringComparison.Ordinal);
        Assert.True(result.ExitCode == 0, result.Output);
    }

    /// <summary>
    /// The step that failed is named above the line saying the run failed, not below it.
    /// </summary>
    /// <remarks>
    /// Order is the whole value here. The run's conclusion and the failing step can land in the same
    /// poll — the cycle that sees the run complete is often the first to see the last step — and a
    /// reader scrolling to the bottom for the verdict finds the cause already above it, or does not
    /// find it at all.
    /// </remarks>
    [Fact]
    public void The_failing_step_is_named_above_the_runs_own_verdict()
    {
        var result = scene.Watch(
            scene.ShortSha,
            branch: null,
            scene.CompletedRun("failure"),
            WatchCiScene.Jobs((6, "completed", "failure", "Accessibility gate")));

        var step = result.Output.IndexOf("STEP FAILED 'Accessibility gate'", StringComparison.Ordinal);
        var verdict = result.Output.LastIndexOf("failure", StringComparison.Ordinal);

        Assert.True(step >= 0, $"the failing step was never named: {result.Output}");
        Assert.True(verdict > step, $"the verdict came before its cause: {result.Output}");
    }

    [Fact]
    public void A_run_on_a_branch_this_checkout_is_not_on_is_found_anyway()
    {
        var result = scene.Watch(scene.ShortSha, branch: null, scene.CompletedRun("success"));

        Assert.True(
            result.ExitCode == 0,
            $"The run is there and the watcher missed it, which is the 2026-09-02 false negative: {result.Output}");
        Assert.Contains("success", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("NO RUN EXISTS", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(
            WatchCiScene.LocalBranch,
            string.Join(' ', result.GhCalls),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The second way to give the same wrong answer, and the one a repair aimed only at the first
    /// walks straight into: <c>gh run list --commit</c> wants all forty characters and answers
    /// <c>[]</c> with exit 0 to a prefix, while <c>.claude/hooks/post-push.sh</c> hands out
    /// <c>rev-parse --short</c>.
    /// </summary>
    [Fact]
    public void The_commit_filter_is_never_handed_a_prefix_gh_would_answer_nothing_to()
    {
        var result = scene.Watch(scene.ShortSha, branch: null, scene.CompletedRun("success"));

        var asked = Regex.Matches(
                string.Join('\n', result.GhCalls),
                @"--commit\s+(?<sha>\S+)",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))
            .Select(match => match.Groups["sha"].Value)
            .ToArray();

        Assert.NotEmpty(asked);
        foreach (var sha in asked)
        {
            Assert.True(
                sha.Length == 40,
                $"The watcher asked gh for '--commit {sha}', which gh answers [] and exit 0 to. "
                + "A prefix has to be resolved through git before it is asked about.");
        }
    }

    /// <summary>
    /// The repair must not buy its green by never saying no. A commit that really has no run still
    /// has to be reported, or the watcher has stopped watching for the outcome it was written for.
    /// </summary>
    [Fact]
    public void A_commit_that_really_has_no_run_is_still_reported_as_having_none()
    {
        var result = scene.Watch(scene.ShortSha, branch: null, "[]");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("NO RUN EXISTS", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A named branch is still honoured — the default changed, the parameter did not — and the
    /// message now names where it looked, because "NO RUN EXISTS" was read as a fact about the push
    /// when it was only ever an answer about one branch.
    /// </summary>
    [Fact]
    public void A_named_branch_is_still_searched_and_the_message_says_which_one()
    {
        var found = scene.Watch(scene.ShortSha, WatchCiScene.PushedBranch, scene.CompletedRun("failure"));
        Assert.Equal(1, found.ExitCode);
        Assert.Contains("failure", found.Output, StringComparison.Ordinal);

        var missed = scene.Watch(scene.ShortSha, WatchCiScene.LocalBranch, scene.CompletedRun("success"));
        Assert.Contains("NO RUN EXISTS", missed.Output, StringComparison.Ordinal);
        Assert.Contains(WatchCiScene.LocalBranch, missed.Output, StringComparison.Ordinal);
        Assert.Contains("landed on another branch", missed.Output, StringComparison.Ordinal);
    }
}
