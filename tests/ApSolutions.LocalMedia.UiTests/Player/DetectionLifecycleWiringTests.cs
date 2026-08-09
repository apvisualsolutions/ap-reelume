using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Background detection is a guest in somebody's session: it has to stop when asked. The deep audit
/// found it uncancellable — the use case accepts a token nobody passed — and immortal, surviving the
/// window it was scheduled from because nothing on the exit path told it to stop (BUG-004).
/// </summary>
public sealed class DetectionLifecycleWiringTests
{
    [Fact]
    public void The_scheduled_detection_runs_under_a_token_that_shutdown_cancels()
    {
        var source = SchedulerSource();

        // ExecuteAsync has taken a CancellationToken since the day it was written; calling it bare
        // is what made a detection in flight impossible to stop.
        Assert.DoesNotContain(
            ".ExecuteAsync(new DetectSeriesSegmentsCommand(showId, seriesId))",
            source,
            StringComparison.Ordinal);
        Assert.Contains("CancellationToken", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaving_the_application_stops_the_detection_still_running()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs"));

        // Without this, closing the window leaves a process decoding through LibVLC and writing to
        // SQLite with nobody watching it.
        Assert.Contains(
            "GetRequiredService<SegmentDetectionScheduler>().Stop()",
            source,
            StringComparison.Ordinal);
    }

    private static string SchedulerSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "Playback",
            "SegmentDetectionBackground.cs");
        Assert.True(File.Exists(path), "SegmentDetectionBackground.cs was not found where the assembly keeps it.");
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
