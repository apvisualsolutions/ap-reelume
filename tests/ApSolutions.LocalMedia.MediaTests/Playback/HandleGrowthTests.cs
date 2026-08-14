// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Pins where the handle growth recorded in the C4 evidence actually comes from. Two paths must hold
/// no handles at all: opening media without playing, and playing with software decoding. What remains
/// after those two is the hardware decoder's own bookkeeping, which this adapter does not own.
/// <para>
/// The cycles run in a child process. Counting handles on the shared test host mixes in the coverage
/// collector and whatever the earlier media tests left behind, and that noise is larger than the
/// signal being measured.
/// </para>
/// <para>
/// The same child answers a second question since BUG-011: thirty opens and closes leave the shared
/// deferred-release queue empty, which is what states that the engine's teardown actually waited for
/// its media instead of walking away from them. The counter is process-wide, so this child is the
/// only place it can be read without the other media suites writing into the same number.
/// </para>
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class HandleGrowthTests
{
    private const string PhaseVariable = "AP_LOCALMEDIA_HANDLE_PHASE";
    private const string ReportVariable = "AP_LOCALMEDIA_HANDLE_REPORT";
    private const string AfterDisposeRow = "after-dispose";
    private const int Cycles = 30;

    /// <summary>
    /// A generous bound: the measured figure for both paths is around zero per cycle, and a regression
    /// introduced by this adapter would be one handle or more per cycle.
    /// </summary>
    private const double AllowedHandlesPerCycle = 0.5;

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
    public async Task Opening_media_without_playing_holds_no_handles_once_collected()
    {
        var measurement = await MeasureInChildAsync("open-only");

        Assert.InRange(measurement.HandlesPerCycle, double.MinValue, AllowedHandlesPerCycle);
        Assert.Equal(0, measurement.PendingReleasesAfterDispose);
    }

    [Fact]
    public async Task Software_decoding_holds_no_handles_across_cycles()
    {
        var measurement = await MeasureInChildAsync("software-play");

        Assert.InRange(measurement.HandlesPerCycle, double.MinValue, AllowedHandlesPerCycle);
        Assert.Equal(0, measurement.PendingReleasesAfterDispose);
    }

    /// <summary>Runs one phase in this process. Only the child started above enters here.</summary>
    [Fact]
    public async Task Handle_growth_child_fixture()
    {
        var phase = Environment.GetEnvironmentVariable(PhaseVariable);
        var report = Environment.GetEnvironmentVariable(ReportVariable);
        if (string.IsNullOrWhiteSpace(phase) || string.IsNullOrWhiteSpace(report))
        {
            return;
        }

        var samples = new List<string>();
        foreach (var id in new[] { "mp4-h264-aac", "mkv-hevc-eac3", "webm-vp9-opus", "mkv-audio-51" })
        {
            samples.Add(await CodecMatrixTests.RequireSampleAsync(MediaManifest.Require(id)));
        }

        var play = string.Equals(phase, "software-play", StringComparison.Ordinal);
        await using var factory = LibVlcFactory.CreateHeadless();
        var process = Process.GetCurrentProcess();
        var rows = new List<string> { "cycle,handles,workingSetBytes" };

        await using (var engine = new LibVlcMediaPlayerEngine(factory))
        {
            await engine.InitializeAsync(TestContext.Current.CancellationToken);
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                await engine.OpenAsync(
                    new PlaybackRequest(
                        new MediaFileId(Guid.NewGuid()),
                        samples[cycle % samples.Count],

                        // Hardware decoding is the path that does grow, and it is not this adapter's
                        // to fix; these phases measure the paths that are.
                        useHardwareAcceleration: false),
                    TestContext.Current.CancellationToken);
                if (play)
                {
                    await engine.PlayAsync(TestContext.Current.CancellationToken);
                    _ = await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(80));
                }

                await engine.StopAsync(TestContext.Current.CancellationToken);

                // The parse path keeps its handles until the objects it allocated are finalised, so a
                // measurement that never collects reports retention as if it were a leak.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                process.Refresh();
                rows.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{cycle + 1},{process.HandleCount},{process.WorkingSet64}"));
            }
        }

        // Read after the engine let go: its teardown flushes the shared queue before releasing the
        // player, so anything still resting here is a media the flush walked away from.
        process.Refresh();
        rows.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"{AfterDisposeRow},{LibVlcFactory.PendingDeferredReleaseCount},{process.WorkingSet64}"));

        _ = Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(report, rows, TestContext.Current.CancellationToken);
    }

    private static async Task<Measurement> MeasureInChildAsync(string phase)
    {
        var report = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "handles",
            $"{phase}.csv");
        if (File.Exists(report))
        {
            File.Delete(report);
        }

        var executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows()
                ? "ApSolutions.LocalMedia.MediaTests.exe"
                : "ApSolutions.LocalMedia.MediaTests");
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
            "ApSolutions.LocalMedia.MediaTests.Playback.HandleGrowthTests.Handle_growth_child_fixture");
        startInfo.ArgumentList.Add("-parallel");
        startInfo.ArgumentList.Add("none");
        startInfo.Environment[PhaseVariable] = phase;
        startInfo.Environment[ReportVariable] = report;
        foreach (var profiling in ProfilerVariables)
        {
            startInfo.Environment[profiling] = string.Empty;
        }

        using var child = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the handle-growth child process.");

        // Both pipes are drained at once: LibVLC is chatty on standard error, and reading one stream
        // to its end while the other fills its buffer deadlocks the child.
        var outputTask = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var errorTask = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        Assert.True(File.Exists(report), $"The child produced no measurement. stdout={output}; stderr={error}");
        var rows = File.ReadAllLines(report).Skip(1).Select(line => line.Split(',')).ToArray();
        var handles = rows
            .Where(columns => char.IsAsciiDigit(columns[0][0]))
            .Select(columns => int.Parse(columns[1], CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(Cycles, handles.Length);
        var pending = int.Parse(
            rows.Single(columns => columns[0] == AfterDisposeRow)[1],
            CultureInfo.InvariantCulture);

        // Two settled windows are compared rather than the first sample against the last, because the
        // first cycles pay the one-time cost of loading the decoders for four different formats.
        const int window = 10;
        var early = handles.Skip(5).Take(window).Average();
        var late = handles.TakeLast(window).Average();
        return new Measurement((late - early) / (Cycles - 5 - window), pending);
    }

    /// <summary>What one child run reports: handle growth per cycle, and media left resting.</summary>
    private sealed record Measurement(double HandlesPerCycle, int PendingReleasesAfterDispose);
}
