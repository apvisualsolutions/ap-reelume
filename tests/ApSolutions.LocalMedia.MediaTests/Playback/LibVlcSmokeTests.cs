// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

[Trait("Category", "RealMedia")]
public sealed class LibVlcSmokeTests
{
    private const string H264Recipe =
        "-f lavfi -i testsrc2=size=320x240:rate=15:duration=3 " +
        "-f lavfi -i sine=frequency=440:duration=3 " +
        "-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -shortest";

    [Fact]
    public async Task Embedded_engine_plays_a_real_h264_sample_and_reports_its_tracks()
    {
        var path = await RequireSampleAsync("T18/h264-aac.mp4", H264Recipe);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var advanced = await WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(200));
        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        await engine.PauseAsync(TestContext.Current.CancellationToken);
        var paused = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        await engine.StopAsync(TestContext.Current.CancellationToken);
        var stopped = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.True(advanced, "Playback position never advanced for the H.264 sample.");
        Assert.True(engine.DecodedFrameCount > 0, "The decoder produced no video frame.");
        Assert.Equal(PlaybackState.Playing, snapshot.State);
        Assert.Contains(snapshot.Tracks, track => track.Kind == MediaTrackKind.Video);
        Assert.Contains(snapshot.Tracks, track => track.Kind == MediaTrackKind.Audio);
        Assert.NotNull(snapshot.Duration);
        Assert.InRange(snapshot.Duration!.Value.TotalSeconds, 2.0, 4.0);
        Assert.Equal(PlaybackState.Paused, paused.State);
        Assert.Equal(PlaybackState.Stopped, stopped.State);
    }

    [Fact]
    public async Task Opening_at_a_stored_position_starts_playback_there_and_not_at_zero()
    {
        var path = await RequireSampleAsync("T18/h264-aac.mp4", H264Recipe);
        var stored = TimeSpan.FromSeconds(1.5);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path, startPosition: stored),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var advanced = await WaitForPositionAsync(engine, TimeSpan.FromSeconds(1.0));
        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        await engine.StopAsync(TestContext.Current.CancellationToken);

        // The seek that start-time performs is coarse, so the assertion is that playback began
        // meaningfully past zero — resuming, not restarting — rather than at an exact millisecond.
        Assert.True(advanced, "Playback position never advanced for the resumed sample.");
        Assert.True(
            snapshot.Position >= TimeSpan.FromSeconds(1.0),
            $"Playback opened at {snapshot.Position.TotalSeconds:F2} s, not at the stored point.");
    }

    /// <summary>
    /// PLY-011's first link: the application can only offer the next episode if the engine says the
    /// current one ended on its own. Before WP-2 nothing observed EndReached, so the state machine
    /// stayed at Playing forever and the countdown had no moment to exist at.
    /// </summary>
    [Fact]
    public async Task Playing_to_the_end_of_the_media_reports_the_ended_state()
    {
        var path = await RequireSampleAsync("T18/h264-aac.mp4", H264Recipe);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.StateChanged += (_, args) =>
        {
            if (args.CurrentState == PlaybackState.Ended)
            {
                ended.TrySetResult();
            }
        };

        // Opening near the end keeps the wait about the wiring, not about sitting through the clip.
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path, startPosition: TimeSpan.FromSeconds(2.4)),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(
            ended.Task,
            Task.Delay(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));

        Assert.True(completed == ended.Task, "The engine never reported that the media ended.");
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fifty_open_and_close_cycles_release_every_engine_resource()
    {
        var path = await RequireSampleAsync("T18/h264-aac.mp4", H264Recipe);
        await using var factory = LibVlcFactory.CreateHeadless();
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var baselineHandles = process.HandleCount;
        var baselineWorkingSet = process.WorkingSet64;
        var samples = new List<string> { "cycle,handles,workingSetBytes,liveMedia,nativeInstances" };

        await using (var engine = new LibVlcMediaPlayerEngine(factory))
        {
            await engine.InitializeAsync(TestContext.Current.CancellationToken);
            for (var cycle = 0; cycle < 50; cycle++)
            {
                await engine.OpenAsync(
                    new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
                    TestContext.Current.CancellationToken);
                await engine.PlayAsync(TestContext.Current.CancellationToken);
                _ = await WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(120));
                await engine.StopAsync(TestContext.Current.CancellationToken);

                Assert.Equal(1, LibVlcFactory.NativeInstanceCount);
                Assert.Equal(0, engine.LiveMediaCount);
                process.Refresh();
                samples.Add(FormattableString.Invariant(
                    $"{cycle + 1},{process.HandleCount},{process.WorkingSet64},{engine.LiveMediaCount},{LibVlcFactory.NativeInstanceCount}"));
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        process.Refresh();
        var finalHandles = process.HandleCount;
        samples.Insert(1, FormattableString.Invariant($"0,{baselineHandles},{baselineWorkingSet},0,1"));
        samples.Add(FormattableString.Invariant(
            $"final,{finalHandles},{process.WorkingSet64},{0},{LibVlcFactory.NativeInstanceCount}"));
        WriteResourceSamples(samples);

        Assert.Equal(0, factory.LiveMediaPlayerCount);
        Assert.InRange(finalHandles - baselineHandles, int.MinValue, 200);
    }

    [Fact]
    public async Task Cancelling_during_opening_leaves_the_engine_idle_and_released()
    {
        var path = await RequireSampleAsync("T18/h264-aac.mp4", H264Recipe);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => engine.OpenAsync(new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path), cancellation.Token));
        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(PlaybackState.Playing, snapshot.State);
        Assert.Equal(0, engine.LiveMediaCount);
        Assert.Equal(1, LibVlcFactory.NativeInstanceCount);
    }

    [Fact]
    public async Task A_missing_file_fails_with_an_actionable_domain_code_and_keeps_the_engine_reusable()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        var missing = Path.Combine(MediaToolchain.OutputRoot, "T18", "does-not-exist.mp4");

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(
            () => engine.OpenAsync(
                new PlaybackRequest(new MediaFileId(Guid.NewGuid()), missing),
                TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.FileNotFound, failure.Failure.Code);
        Assert.Equal(0, engine.LiveMediaCount);

        var path = await RequireSampleAsync("T18/h264-aac.mp4", H264Recipe);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, engine.LiveMediaCount);
    }

    [Fact]
    public async Task Forced_disposal_during_playback_never_leaks_a_native_session()
    {
        var path = await RequireSampleAsync("T18/h264-aac.mp4", H264Recipe);
        await using var factory = LibVlcFactory.CreateHeadless();
        var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        _ = await WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(120));

        await engine.DisposeAsync();
        await engine.DisposeAsync();

        Assert.Equal(0, engine.LiveMediaCount);
        Assert.Equal(0, factory.LiveMediaPlayerCount);
        Assert.Equal(1, LibVlcFactory.NativeInstanceCount);
    }

    /// <summary>Records the per-cycle resource profile so evidence can show a flat, non-growing trend.</summary>
    private static void WriteResourceSamples(IEnumerable<string> samples)
    {
        var destination = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "T18",
            "green",
            "engine-resource-cycles.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllLines(destination, samples);
    }

    private static async Task<string> RequireSampleAsync(string relativePath, string recipe)
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        return await MediaToolchain.EnsureSampleAsync(
            relativePath,
            recipe,
            TestContext.Current.CancellationToken);
    }

    private static async Task<bool> WaitForPositionAsync(LibVlcMediaPlayerEngine engine, TimeSpan minimum)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
            if (snapshot.Position >= minimum)
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }
}
