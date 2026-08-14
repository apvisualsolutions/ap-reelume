// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using ApSolutions.LocalMedia.TestSupport;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Continuity;

/// <summary>
/// Progress must survive a process that is killed without warning. A child process plays a simulated
/// media on a compressed clock while writing what it actually reached; the parent kills it at a random
/// moment and compares that trace against what the database committed.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ChildProcessSuites.Name)]
public sealed class CrashResumeTests
{
    private const string ChildFlagVariable = "AP_LOCALMEDIA_RESUME_CHILD";
    private const string DatabaseVariable = "AP_LOCALMEDIA_RESUME_DB";
    private const string TraceVariable = "AP_LOCALMEDIA_RESUME_TRACE";
    private const string SignalVariable = "AP_LOCALMEDIA_RESUME_SIGNAL";
    private const string ContentVariable = "AP_LOCALMEDIA_RESUME_CONTENT";

    /// <summary>One simulated second costs this many real milliseconds, so a trial lasts seconds.</summary>
    private const int RealMillisecondsPerVirtualSecond = 20;

    private const int Trials = 20;
    private const int VirtualSeconds = 900;
    private const int RandomSeed = 20260802;

    private static readonly TimeSpan SimulatedDuration = TimeSpan.FromMinutes(50);

    private static readonly string[] ProfilerVariables =
    [
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "CORECLR_PROFILER_PATH",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "COR_PROFILER_PATH",
    ];

    [Fact]
    public async Task A_committed_position_round_trips_and_upserts_instead_of_duplicating()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new WatchStateRepository(factory);
        var content = ContentKey.ForEpisode(
            new TitleId(Guid.Parse("2b7f0001-0000-4000-8000-000000000001")),
            new EpisodeId(Guid.Parse("2b7f0001-0000-4000-8000-0000000000e1")));
        var source = new MediaFileId(Guid.Parse("2b7f0001-0000-4000-8000-0000000000f1"));
        var started = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

        await repository.SaveAsync(
            new WatchState
            {
                Content = content,
                Position = TimeSpan.FromMinutes(3),
                ObservedDuration = SimulatedDuration,
                SourceMediaFileId = source,
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = started,
                UpdatedUtc = started,
            },
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            new WatchState
            {
                Content = content,
                Position = TimeSpan.FromMinutes(9),
                ObservedDuration = SimulatedDuration,
                SourceMediaFileId = source,
                Status = WatchStatus.InProgress,
                IsManualOverride = true,
                StartedUtc = started,
                UpdatedUtc = started.AddMinutes(6),
            },
            TestContext.Current.CancellationToken);

        var restored = await new WatchStateRepository(factory).GetAsync(
            content,
            TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.Equal(TimeSpan.FromMinutes(9), restored!.Position);
        Assert.Equal(SimulatedDuration, restored.ObservedDuration);
        Assert.Equal(source, restored.SourceMediaFileId);
        Assert.True(restored.IsManualOverride);
        Assert.Equal(started, restored.StartedUtc);
        Assert.Equal(content, restored.Content);

        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1L, await SqliteBootstrapTests.ScalarInt64Async(connection, "SELECT COUNT(*) FROM watch_state;"));
    }

    [Fact]
    public async Task Every_stored_state_can_be_read_back_for_a_threshold_change()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new WatchStateRepository(factory);
        var titles = new[]
        {
            new TitleId(Guid.Parse("2b7f0002-0000-4000-8000-000000000002")),
            new TitleId(Guid.Parse("2b7f0003-0000-4000-8000-000000000003")),
        };

        Assert.Empty(await repository.GetAllAsync(TestContext.Current.CancellationToken));
        foreach (var title in titles)
        {
            await repository.SaveAsync(
                new WatchState
                {
                    Content = ContentKey.ForTitle(title),
                    Position = TimeSpan.FromMinutes(30),
                    ObservedDuration = SimulatedDuration,
                    SourceMediaFileId = new MediaFileId(title.Value),
                    Status = WatchStatus.InProgress,
                    IsManualOverride = false,
                    StartedUtc = DateTimeOffset.UnixEpoch,
                    UpdatedUtc = DateTimeOffset.UnixEpoch,
                },
                TestContext.Current.CancellationToken);
        }

        var all = await new WatchStateRepository(factory).GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
        Assert.All(all, state => Assert.Equal(TimeSpan.FromMinutes(30), state.Position));
        Assert.Equal(
            [.. titles.Select(title => ContentKey.ForTitle(title).Value).Order(StringComparer.Ordinal)],
            all.Select(state => state.Content.Value));
    }

    [Fact]
    public async Task An_absent_content_reports_no_progress_instead_of_failing()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);

        var missing = await new WatchStateRepository(factory).GetAsync(
            ContentKey.ForTitle(new TitleId(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        Assert.Null(missing);
    }

    [Fact]
    public async Task Twenty_forced_closes_resume_within_five_seconds()
    {
        // The ±5 s promise is measured under a five-second write cadence a shared CI runner cannot
        // hold: the starved child process writes late and the trial reads as a product failure. The
        // gate keeps running on the physical harness and in eng/run-recovery.ps1.
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true",
            "Shared-runner scheduling cannot hold the write cadence this promise is measured under.");

        var random = new Random(RandomSeed);
        var rows = new List<string> { "trial,killAfterMs,reachedSeconds,persistedSeconds,absoluteErrorSeconds" };
        var worstError = 0.0;

        for (var trial = 1; trial <= Trials; trial++)
        {
            using var directory = new DatabaseTestDirectory();
            var factory = new SqliteConnectionFactory(directory.DatabasePath);
            await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
            var content = ContentKey.ForTitle(new TitleId(Guid.NewGuid()));
            var tracePath = Path.Combine(directory.Path, "reached.trace");
            var signalPath = Path.Combine(directory.Path, "started.signal");
            var killAfter = random.Next(300, 2_500);

            var child = StartChild(directory.DatabasePath, tracePath, signalPath, content.Value);
            try
            {
                await WaitForSignalAsync(child, signalPath);
                await Task.Delay(killAfter, TestContext.Current.CancellationToken);
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                }

                await child.WaitForExitAsync(TestContext.Current.CancellationToken);
                child.Dispose();
            }

            var reached = ReadReachedSeconds(tracePath);
            var persisted = await ReadPersistedSecondsAsync(factory, content);
            var error = Math.Abs(reached - persisted);
            worstError = Math.Max(worstError, error);
            rows.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{trial},{killAfter},{reached:F0},{persisted:F0},{error:F0}"));

            Assert.True(
                reached > 0,
                $"Trial {trial} produced no simulated playback to compare against.");
            Assert.True(
                error <= 5.0,
                $"Trial {trial} resumed {error:F0} s away from the point it reached.");

            await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        }

        rows.Add(string.Create(CultureInfo.InvariantCulture, $"worst,,,,{worstError:F0}"));
        var report = Path.Combine(
            RepositoryLayout.Root,
            "artifacts",
            "test-results",
            "T25",
            "green",
            "forced-close-trials.csv");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(report, rows, TestContext.Current.CancellationToken);

        Assert.True(worstError <= 5.0, $"The worst forced-close error was {worstError:F0} s.");
    }

    /// <summary>
    /// Runs only inside the child process the trial starts. It plays a simulated media on a compressed
    /// clock, records what it actually reached, and waits to be killed.
    /// </summary>
    [Fact]
    public async Task Crash_resume_child_fixture()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(ChildFlagVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        var databasePath = Environment.GetEnvironmentVariable(DatabaseVariable)!;
        var tracePath = Environment.GetEnvironmentVariable(TraceVariable)!;
        var signalPath = Environment.GetEnvironmentVariable(SignalVariable)!;
        var content = ContentKey.Parse(Environment.GetEnvironmentVariable(ContentVariable)!);

        var factory = new SqliteConnectionFactory(databasePath);
        var repository = new WatchStateRepository(factory);
        await using var tracker = new PlaybackProgressTracker(
            repository,
            new CompressedClock(RealMillisecondsPerVirtualSecond));
        _ = await tracker.BeginAsync(
            content,
            new MediaFileId(Guid.Parse("2b7f0001-0000-4000-8000-0000000000ff")),
            TestContext.Current.CancellationToken);

        using var cancellation = new CancellationTokenSource();
        var loop = tracker.RunAsync(cancellation.Token);
        for (var second = 1; second <= VirtualSeconds; second++)
        {
            await Task.Delay(RealMillisecondsPerVirtualSecond, TestContext.Current.CancellationToken);
            tracker.Observe(TimeSpan.FromSeconds(second), SimulatedDuration);
            await File.AppendAllTextAsync(
                tracePath,
                string.Create(CultureInfo.InvariantCulture, $"{second}\n"),
                TestContext.Current.CancellationToken);
            if (second == 1)
            {
                await File.WriteAllTextAsync(
                    signalPath,
                    Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                    TestContext.Current.CancellationToken);
            }
        }

        await cancellation.CancelAsync();
        await loop;
    }

    private static Process StartChild(string databasePath, string tracePath, string signalPath, string content)
    {
        var executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows()
                ? "ApSolutions.LocalMedia.IntegrationTests.exe"
                : "ApSolutions.LocalMedia.IntegrationTests");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-method");
        startInfo.ArgumentList.Add(
            "ApSolutions.LocalMedia.IntegrationTests.Continuity.CrashResumeTests.Crash_resume_child_fixture");
        startInfo.ArgumentList.Add("-parallel");
        startInfo.ArgumentList.Add("none");
        startInfo.Environment[ChildFlagVariable] = "1";
        startInfo.Environment[DatabaseVariable] = databasePath;
        startInfo.Environment[TraceVariable] = tracePath;
        startInfo.Environment[SignalVariable] = signalPath;
        startInfo.Environment[ContentVariable] = content;

        // The child must not inherit the coverage profiler: twenty instrumented children write over
        // the parent's own coverage data, which silently reports covered code as uncovered.
        foreach (var profiling in ProfilerVariables)
        {
            startInfo.Environment[profiling] = string.Empty;
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the crash-resume child process.");
    }

    private static async Task WaitForSignalAsync(Process child, string signalPath)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!File.Exists(signalPath) && DateTime.UtcNow < deadline && !child.HasExited)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        if (!File.Exists(signalPath))
        {
            var output = await child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = await child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.Fail($"The crash-resume child never started playing. stdout={output}; stderr={error}");
        }
    }

    private static double ReadReachedSeconds(string tracePath)
    {
        if (!File.Exists(tracePath))
        {
            return 0;
        }

        var lines = File.ReadAllLines(tracePath);
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            if (double.TryParse(lines[index], CultureInfo.InvariantCulture, out var reached))
            {
                return reached;
            }
        }

        return 0;
    }

    private static async Task<double> ReadPersistedSecondsAsync(
        SqliteConnectionFactory factory,
        ContentKey content)
    {
        var stored = await new WatchStateRepository(factory).GetAsync(
            content,
            TestContext.Current.CancellationToken);
        return stored?.Position.TotalSeconds ?? 0;
    }

    /// <summary>A real clock whose waits are compressed so a five-second rule can be exercised fast.</summary>
    private sealed class CompressedClock(int realMillisecondsPerVirtualSecond) : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.Delay(
                TimeSpan.FromMilliseconds(delay.TotalSeconds * realMillisecondsPerVirtualSecond),
                cancellationToken);
    }
}
