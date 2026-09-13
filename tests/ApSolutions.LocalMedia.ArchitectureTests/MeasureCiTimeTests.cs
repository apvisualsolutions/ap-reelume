// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using System.Diagnostics;
using System.Text;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// That <c>eng/measure-ci-time.ps1</c> compares the clock the cut actually runs on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on 2026-09-12, and it did not.</b> The handover of the night before said «the CI
/// margin is in the negative: the slowest of the last ten runs took 94.4 minutes with the cut at
/// 90». The run existed and was green — <c>b1baf7d</c>, 2026-09-11 — and 44 of those 94 minutes
/// were spent <b>waiting for a machine</b>: it was created at 08:51:48 and a runner did not pick it
/// up until 09:35:43. The job itself took 50 minutes against its 90, which is 40 minutes of margin
/// and not minus four.
/// </para>
/// <para>
/// The script measured the run's wall clock (<c>updatedAt - createdAt</c>) and compared it against
/// <c>timeout-minutes</c>, which is a <b>job</b> ceiling that only starts counting when a runner
/// takes the job. Two different clocks, and the difference is however long GitHub's queue happens
/// to be. <c>ci.yml</c> already said so in a comment — «a job carries its own clock, so this one
/// cannot eat into the margin eng/measure-ci-time.ps1 reports» — so the workflow knew and the
/// script did not.
/// </para>
/// <para>
/// Why it matters more than the arithmetic: the script <b>warns on its own</b> below ten minutes of
/// margin, and its warning ends «measure before raising the ceiling». Fed a queue, it would demand
/// exactly the change its own text argues against. A guard that fires on someone else's queue is a
/// guard people learn to switch off.
/// </para>
/// <para>
/// So the pair below is the point: one scene where a long <i>wait</i> must stay silent, and one
/// where a long <i>job</i> must sound. Either one alone would pass on a script that always warned,
/// or on one that never did.
/// </para>
/// </remarks>
public sealed class MeasureCiTimeTests
{
    /// <summary>The x64 job's ceiling in <c>ci.yml</c>, read rather than repeated.</summary>
    private static int VerifyCeiling => CeilingOf("verify");

    /// <summary>
    /// A run that waited three quarters of an hour for a machine has all of its margin.
    /// </summary>
    [Fact]
    public void Time_spent_waiting_for_a_machine_is_not_charged_against_the_cut()
    {
        using var scene = new MeasureCiScene();

        // The real run b1baf7d: created 08:51:48, a runner took it at 09:36:12, done at 10:26:12.
        // Ninety-four minutes of wall clock, fifty of work.
        scene.AddRun(
            id: 34581275390,
            created: "2026-09-11T08:51:48Z",
            updated: "2026-09-11T10:26:12Z",
            jobs: [("verify", "2026-09-11T09:36:12Z", "2026-09-11T10:26:12Z")]);

        var output = scene.Measure();

        Assert.Contains($"margen {VerifyCeiling - 50}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("AVISO", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the control that must sound: a job that really is close to its ceiling still warns.
    /// </summary>
    /// <remarks>
    /// Without this, the test above would pass on a script that had simply stopped warning at all,
    /// which is the failure this repository keeps meeting: a guard made quiet instead of correct.
    /// </remarks>
    [Fact]
    public void A_job_that_really_is_near_its_ceiling_still_warns()
    {
        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 1,
            created: "2026-09-11T07:50:00Z",
            updated: "2026-09-11T09:25:00Z",
            jobs: [("verify", "2026-09-11T08:00:00Z", "2026-09-11T09:25:00Z")]);

        var output = scene.Measure();

        Assert.Contains($"margen {VerifyCeiling - 85}", output, StringComparison.Ordinal);
        Assert.Contains("AVISO", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The wall clock does not disappear: it is what a person waits, so it is still reported —
    /// named as what it is, so nobody reads it as the number the cut watches.
    /// </summary>
    [Fact]
    public void The_wall_clock_is_still_reported_and_says_it_includes_the_wait()
    {
        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 34581275390,
            created: "2026-09-11T08:51:48Z",
            updated: "2026-09-11T10:26:12Z",
            jobs: [("verify", "2026-09-11T09:36:12Z", "2026-09-11T10:26:12Z")]);

        var line = MeasureCiScene.WallClockLine(scene.Measure());

        Assert.Contains("94.4", line, StringComparison.Ordinal);
        Assert.Contains("espera", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// The slowest job of the window is the one reported, which is what «slowest» has to mean.
    /// </summary>
    /// <remarks>
    /// Every scene above holds a single run, and with one run the accumulator takes the first job
    /// it meets and never compares. Turning its comparison round — reporting the <i>fastest</i> job
    /// of the window instead of the slowest — survived all four of them. Two runs, with the slow
    /// one second so a first-wins bug cannot pass either, is what closes it.
    /// </remarks>
    [Fact]
    public void The_slowest_job_of_the_window_is_the_one_reported()
    {
        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 10,
            created: "2026-09-11T08:00:00Z",
            updated: "2026-09-11T08:30:00Z",
            jobs: [("verify", "2026-09-11T08:00:00Z", "2026-09-11T08:30:00Z")]);
        scene.AddRun(
            id: 11,
            created: "2026-09-11T09:00:00Z",
            updated: "2026-09-11T10:10:00Z",
            jobs: [("verify", "2026-09-11T09:00:00Z", "2026-09-11T10:10:00Z")]);

        var output = scene.Measure();

        Assert.Contains($"más lento 70 min · margen {VerifyCeiling - 70}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("más lento 30 min", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ten minutes is where the warning starts, and both sides of it are pinned.
    /// </summary>
    /// <remarks>
    /// The scenes above leave margins of 40 and 5, which only says the threshold is somewhere in
    /// between: moving it to thirty — which would warn on every healthy run of the ARM64 job — went
    /// unnoticed. A margin of eleven must stay silent and one of nine must sound.
    /// </remarks>
    [Theory]
    [InlineData(79, false)]
    [InlineData(81, true)]
    public void The_warning_starts_at_ten_minutes_of_margin(int jobMinutes, bool warns)
    {
        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 12,
            created: "2026-09-11T08:00:00Z",
            updated: "2026-09-11T09:30:00Z",
            jobs: [("verify", "2026-09-11T08:00:00Z", $"2026-09-11T{8 + (jobMinutes / 60):00}:{jobMinutes % 60:00}:00Z")]);

        var output = scene.Measure();

        Assert.Contains($"margen {VerifyCeiling - jobMinutes} min", output, StringComparison.Ordinal);
        Assert.Equal(warns, output.Contains("AVISO", StringComparison.Ordinal));
    }

    /// <summary>
    /// A job that did not run is said out loud, and a job with no clock counts as not having run.
    /// </summary>
    /// <remarks>
    /// Both halves were removable in green. The sentence «no corrió en ninguno de estos runs» was
    /// printed by the script and read by nobody, and its own comment names the defect it prevents:
    /// a job that quietly stopped running is exactly what a silent margin cannot tell from a job
    /// with room to spare. And the clock filter behind it is not hypothetical — <c>arm64-matrix</c>
    /// carries <c>continue-on-error</c>, and a skipped or queued job comes back from
    /// <c>gh</c> stamped <c>0001-01-01T00:00:00Z</c>.
    /// </remarks>
    [Fact]
    public void A_job_with_no_clock_is_reported_as_not_having_run()
    {
        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 13,
            created: "2026-09-11T08:00:00Z",
            updated: "2026-09-11T08:50:00Z",
            jobs:
            [
                ("verify", "2026-09-11T08:00:00Z", "2026-09-11T08:50:00Z"),
                ("arm64-matrix", "0001-01-01T00:00:00Z", "0001-01-01T00:00:00Z"),
            ]);

        var output = scene.Measure();

        Assert.Contains("arm64-matrix  techo 30 min · no corrió", output, StringComparison.Ordinal);
        Assert.DoesNotContain("arm64-matrix  techo 30 min · más lento", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A job the workflow does not declare a ceiling for is named, not dropped.
    /// </summary>
    /// <remarks>
    /// Dropping it silently would be the house defect again: a job measured and never reported
    /// reads exactly like a job with nothing to report.
    /// </remarks>
    [Fact]
    public void A_job_with_no_declared_ceiling_is_named_rather_than_dropped()
    {
        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 14,
            created: "2026-09-11T08:00:00Z",
            updated: "2026-09-11T08:20:00Z",
            jobs: [("a-job-ci-yml-does-not-declare", "2026-09-11T08:00:00Z", "2026-09-11T08:20:00Z")]);

        var output = scene.Measure();

        Assert.Contains("a-job-ci-yml-does-not-declare", output, StringComparison.Ordinal);
        Assert.Contains("sin techo declarado", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// And when no job can be read at all, the script says so instead of printing a heading and
    /// then nothing.
    /// </summary>
    [Fact]
    public void No_readable_job_is_said_out_loud_rather_than_left_as_a_blank_heading()
    {
        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 15,
            created: "2026-09-11T08:00:00Z",
            updated: "2026-09-11T08:20:00Z",
            jobs: []);

        var output = scene.Measure();

        Assert.Contains("No se pudo leer ningún trabajo", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every job's own ceiling is read, not just the first one in the file.
    /// </summary>
    /// <remarks>
    /// The old code took the first <c>timeout-minutes:</c> the regex met, which happens to be the
    /// x64 job's 90. The ARM64 job's is 30, and a step's is 70 — so «the first one» was right only
    /// by the order somebody happened to write the file in.
    /// </remarks>
    [Fact]
    public void Each_job_is_measured_against_its_own_ceiling()
    {
        Assert.Equal(90, CeilingOf("verify"));
        Assert.Equal(30, CeilingOf("arm64-matrix"));

        using var scene = new MeasureCiScene();
        scene.AddRun(
            id: 2,
            created: "2026-09-11T08:00:00Z",
            updated: "2026-09-11T09:00:00Z",
            jobs:
            [
                ("verify", "2026-09-11T08:00:00Z", "2026-09-11T08:40:00Z"),
                ("arm64-matrix", "2026-09-11T08:00:00Z", "2026-09-11T08:06:00Z"),
            ]);

        var output = scene.Measure();

        Assert.Contains("verify", output, StringComparison.Ordinal);
        Assert.Contains("arm64-matrix", output, StringComparison.Ordinal);
        Assert.Contains("margen 50", output, StringComparison.Ordinal);
        Assert.Contains("margen 24", output, StringComparison.Ordinal);
    }

    /// <summary>The job-level ceiling declared for one job of <c>ci.yml</c>.</summary>
    /// <remarks>
    /// Job keys sit at two spaces and their <c>timeout-minutes</c> at four; a step's sits at eight.
    /// That is what separates a job's ceiling from a step's without parsing YAML.
    /// </remarks>
    private static int CeilingOf(string job)
    {
        var lines = File.ReadAllLines(RepositoryLayout.PathFromRoot(".github/workflows/ci.yml"));
        var inside = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("  " + job + ":", StringComparison.Ordinal))
            {
                inside = true;
                continue;
            }

            if (inside && line.Length > 2 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ')
            {
                break;
            }

            if (inside && line.StartsWith("    timeout-minutes:", StringComparison.Ordinal))
            {
                return int.Parse(
                    line["    timeout-minutes:".Length..].Trim(),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException($"ci.yml declares no job «{job}» with a ceiling of its own.");
    }

    /// <summary>
    /// A scene where <c>gh</c> is a script serving canned answers, so the measurement is of the
    /// repository's own script and not of whatever GitHub happens to be doing.
    /// </summary>
    private sealed class MeasureCiScene : IDisposable
    {
        private readonly string _root;
        private readonly List<string> _runs = [];
        private readonly Dictionary<long, string> _jobs = [];

        public MeasureCiScene()
        {
            _root = Path.Combine(Path.GetTempPath(), "measure-ci-" + Guid.NewGuid().ToString("n")[..12]);
            Directory.CreateDirectory(Path.Combine(_root, "bin"));

            File.WriteAllText(
                Path.Combine(_root, "bin", "gh.cmd"),
                string.Join(
                    "\r\n",
                    "@echo off",
                    // `run view <id> --json jobs` carries no --workflow and would otherwise fall
                    // through to the run list, so it is answered first — and by id, because the
                    // whole point is that each run has jobs of its own.
                    // `gh run view <id> --json jobs` puts the id third: %1 is «run», %2 is «view».
                    "echo %*| findstr /C:\"--json jobs\" >nul",
                    "if %errorlevel%==0 (type \"%GH_STUB_DIR%\\jobs-%3.json\" & exit /b 0)",
                    "type \"%GH_STUB_RUNS%\"",
                    "exit /b 0",
                    string.Empty));
        }

        public void AddRun(
            long id,
            string created,
            string updated,
            (string Name, string Started, string Completed)[] jobs)
        {
            _runs.Add(
                $$"""
                {"databaseId":{{id}},"conclusion":"success","status":"completed",
                 "createdAt":"{{created}}","updatedAt":"{{updated}}",
                 "headSha":"0123456789abcdef0123456789abcdef01234567","displayTitle":"scene"}
                """);

            var bodies = jobs.Select(job =>
                $$"""
                {"name":"{{job.Name}}","conclusion":"success",
                 "startedAt":"{{job.Started}}","completedAt":"{{job.Completed}}"}
                """);
            _jobs[id] = $$"""{"jobs":[{{string.Join(",", bodies)}}]}""";
        }

        /// <summary>Runs the real script against the scene and returns everything it said.</summary>
        public string Measure()
        {
            File.WriteAllText(
                Path.Combine(_root, "runs.json"),
                "[" + string.Join(",", _runs) + "]" + Environment.NewLine);
            foreach (var (id, body) in _jobs)
            {
                File.WriteAllText(Path.Combine(_root, $"jobs-{id}.json"), body + Environment.NewLine);
            }

            var start = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = RepositoryLayout.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(RepositoryLayout.PathFromRoot("eng/measure-ci-time.ps1"));
            start.ArgumentList.Add("-Limit");
            start.ArgumentList.Add(_runs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.Environment["PATH"] = Path.Combine(_root, "bin") + Path.PathSeparator + start.Environment["PATH"];
            start.Environment["GH_STUB_RUNS"] = Path.Combine(_root, "runs.json");
            start.Environment["GH_STUB_DIR"] = _root;

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("pwsh did not start, so nothing was measured.");
            var output = new StringBuilder();
            output.Append(process.StandardOutput.ReadToEnd());
            output.Append(process.StandardError.ReadToEnd());
            Assert.True(
                process.WaitForExit(120_000),
                "eng/measure-ci-time.ps1 did not finish in two minutes against the scene.");

            var text = output.ToString();

            // The floor, and it took an audit to get it right. «Duración de CI» is printed BEFORE
            // the first `gh run view --json jobs` call, so it only proves the run list answered:
            // with the stub broken so it never serves a job, one of these tests still passed. What
            // has to be true for anything below to mean something is that the job-by-job section
            // SAID something — a margin, or one of the two sentences it says when it cannot.
            Assert.True(
                text.Contains("margen ", StringComparison.Ordinal)
                    || text.Contains("no corrió en ninguno", StringComparison.Ordinal)
                    || text.Contains("sin techo declarado", StringComparison.Ordinal)
                    || text.Contains("No se pudo leer ningún trabajo", StringComparison.Ordinal),
                "The script never reached its job-by-job section, so the scene measured nothing. "
                    + "That is a broken stub, not a passing script:"
                    + Environment.NewLine
                    + text);
            return text;
        }

        /// <summary>The one line reporting the wall clock, so an assertion cannot land elsewhere.</summary>
        /// <remarks>
        /// Asserting on the whole output let «94.4» match the table's own cell, which
        /// <c>Format-Table</c> renders in the current culture: «94,40» here and «94.400» on a
        /// runner. That is red locally and green in CI, which is the worst shape a gate can take.
        /// The sentence below is built by string interpolation, which PowerShell does in the
        /// invariant culture, so it reads the same on both machines.
        /// </remarks>
        public static string WallClockLine(string output) =>
            output
                .Split('\n')
                .FirstOrDefault(line => line.Contains("Sobre los", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The script printed no wall-clock line at all:" + Environment.NewLine + output);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // A scene that will not delete is not a failure of what is being measured.
            }
        }
    }
}
