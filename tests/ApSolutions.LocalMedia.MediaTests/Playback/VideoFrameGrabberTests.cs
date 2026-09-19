// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// A frame really comes out of a real video, and the guards really refuse before anything opens
/// (CRS-006).
/// </summary>
/// <remarks>
/// The spike measured that this route works — «docs/evidence/stable/CRS-thumbnail-spike.md» — and
/// this is the gate that keeps it working. Everything about <b>which</b> frame is
/// <c>CourseThumbnailPolicy</c>'s and is covered without a decoder; what is here is the half that
/// needs one.
/// </remarks>
public sealed class VideoFrameGrabberTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ap-reelume-grab-" + Guid.NewGuid().ToString("N"));

    // The one native instance, borrowed the way the application borrows it. Building a second is
    // what NativeInstanceOwnershipTests refuses, and for the failure mode it names.
    private readonly LibVlcFactory _factory = LibVlcFactory.CreateHeadless();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>A real file gives a real picture, and it is a picture rather than an empty file.</summary>
    /// <remarks>
    /// The size floor is what separates «a file appeared» from «a frame was written». A zero-byte
    /// PNG would satisfy any check written against existence alone, and that is exactly what a
    /// snapshot that was asked for and never delivered leaves behind.
    /// </remarks>
    [Fact]
    public async Task A_frame_comes_out_of_a_real_video()
    {
        var descriptor = MediaManifest.Require("mp4-h264-aac");
        var missing = MediaManifest.MissingEncoders(descriptor);
        Assert.SkipWhen(missing.Count > 0, $"this machine's ffmpeg has no {string.Join(", ", missing)}.");

        var sample = await MediaManifest.MaterialiseAsync(descriptor, TestContext.Current.CancellationToken);

        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "course.png");
        var watch = Stopwatch.StartNew();

        var taken = await new LibVlcVideoFrameGrabber(_factory)
            .TryCaptureAsync(sample, TimeSpan.FromSeconds(0.3), destination, TestContext.Current.CancellationToken);

        Assert.True(taken, "no frame came out of a sample the codec matrix calls playable.");
        Assert.True(File.Exists(destination));
        Assert.True(
            new FileInfo(destination).Length > 1024,
            $"the picture is {new FileInfo(destination).Length} bytes, which is a file rather than a frame.");

        // Not a budget, a sanity floor: the spike measured 433-472 ms per file and the policy's
        // deadline is three seconds, so anything near the deadline means the wait, not the decode.
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(15),
            $"taking one frame took {watch.Elapsed}, which is not what was measured.");
    }

    /// <summary>
    /// What one batch of the frame pass costs (LIB-021): <c>CaptureTitleFrames.BatchSize</c> files,
    /// one after the other, the way the pass takes them.
    /// </summary>
    /// <remarks>
    /// The ceiling is the failure that matters and not a benchmark: a grabber that waited out its
    /// deadline on every file would spend 25 × 3 s on a batch and still take every frame, so no other
    /// test here would notice. The spike measured 433-472 ms a file; the figure goes to the output so
    /// the evidence can quote a measurement instead of that estimate.
    /// </remarks>
    [Fact]
    public async Task A_batch_of_twenty_five_frames_costs_seconds_not_the_sum_of_every_deadline()
    {
        var descriptor = MediaManifest.Require("mp4-h264-aac");
        var missing = MediaManifest.MissingEncoders(descriptor);
        Assert.SkipWhen(missing.Count > 0, $"this machine's ffmpeg has no {string.Join(", ", missing)}.");
        var sample = await MediaManifest.MaterialiseAsync(descriptor, TestContext.Current.CancellationToken);
        Directory.CreateDirectory(_root);
        var grabber = new LibVlcVideoFrameGrabber(_factory);
        const int Batch = Application.Metadata.CaptureTitleFrames.BatchSize;

        var watch = Stopwatch.StartNew();
        var taken = 0;
        for (var index = 0; index < Batch; index++)
        {
            var copy = Path.Combine(_root, $"title-{index}.mp4");
            File.Copy(sample, copy);
            if (await grabber.TryCaptureAsync(copy, TimeSpan.FromSeconds(0.3), Path.Combine(_root, $"title-{index}.png"), TestContext.Current.CancellationToken))
            {
                taken++;
            }
        }

        watch.Stop();
        TestContext.Current.SendDiagnosticMessage(
            $"LIB-021: {taken} of {Batch} frames in {watch.Elapsed.TotalSeconds:F1} s, {watch.Elapsed.TotalMilliseconds / Batch:F0} ms each.");
        Assert.Equal(Batch, taken);
        Assert.True(
            watch.Elapsed < CourseThumbnailPolicy.Deadline * Batch / 2,
            $"a batch of {Batch} took {watch.Elapsed}: that is waiting on deadlines, not decoding.");
    }

    /// <summary>
    /// A path outside the approved containers is refused before LibVLC is touched at all.
    /// </summary>
    /// <remarks>
    /// LibVLC decodes in-process in native code and is this application's largest residual risk; the
    /// trap notes forbid handing it a path nobody filtered, by name. This is called with paths out of
    /// the catalogue, so the check has to live in the adapter rather than in whoever calls it.
    /// </remarks>
    [Fact]
    public async Task A_file_outside_the_approved_containers_is_refused()
    {
        Directory.CreateDirectory(_root);
        var notAVideo = Path.Combine(_root, "readme.txt");
        await File.WriteAllTextAsync(notAVideo, "not a video", TestContext.Current.CancellationToken);

        var taken = await new LibVlcVideoFrameGrabber(_factory).TryCaptureAsync(
            notAVideo,
            TimeSpan.Zero,
            Path.Combine(_root, "out.png"),
            TestContext.Current.CancellationToken);

        Assert.False(taken);
        Assert.False(File.Exists(Path.Combine(_root, "out.png")));
    }

    /// <summary>A file that is not there is refused rather than opened.</summary>
    [Fact]
    public async Task A_file_that_is_not_there_is_refused()
    {
        var taken = await new LibVlcVideoFrameGrabber(_factory).TryCaptureAsync(
            Path.Combine(_root, "gone.mkv"),
            TimeSpan.Zero,
            Path.Combine(_root, "out.png"),
            TestContext.Current.CancellationToken);

        Assert.False(taken);
    }

    /// <summary>
    /// A decoder that accepts the ask and never writes anything is given up on, not waited for
    /// forever.
    /// </summary>
    /// <remarks>
    /// Measured against a double rather than against a real broken file, because what is asserted is
    /// the deadline itself: the spike's unsupported sample took 4.5 s to answer, and one file nobody
    /// can decode must not hold up every card behind it.
    /// </remarks>
    [Fact]
    public async Task A_snapshot_that_never_arrives_is_given_up_on()
    {
        Directory.CreateDirectory(_root);
        var video = Path.Combine(_root, "silent.mkv");
        await File.WriteAllTextAsync(video, "pretend", TestContext.Current.CancellationToken);

        var watch = Stopwatch.StartNew();
        var taken = await new LibVlcVideoFrameGrabber(_ => new SilentCapture())
            .TryCaptureAsync(video, TimeSpan.Zero, Path.Combine(_root, "out.png"), TestContext.Current.CancellationToken);

        Assert.False(taken);
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(10),
            $"it waited {watch.Elapsed} for a frame that was never coming.");
    }

    /// <summary>A decoder that will not even open the file answers at once, and writes nothing.</summary>
    /// <remarks>
    /// <b>The refusal before the wait, which is a different failure from the one above.</b> A file
    /// the container check approves can still be one this machine has no decoder for, and that has
    /// to cost nothing: waiting the deadline out for a file that never opened would make a folder of
    /// unsupported videos take three seconds a card. The double refuses because a real decoder that
    /// refuses is a decoder this machine does not have, which is the one state a test cannot arrange.
    /// </remarks>
    [Fact]
    public async Task A_decoder_that_will_not_open_the_file_answers_at_once()
    {
        Directory.CreateDirectory(_root);
        var video = Path.Combine(_root, "unopenable.mkv");
        await File.WriteAllTextAsync(video, "pretend", TestContext.Current.CancellationToken);
        var destination = Path.Combine(_root, "out.png");

        var watch = Stopwatch.StartNew();
        var taken = await new LibVlcVideoFrameGrabber(_ => new RefusingCapture())
            .TryCaptureAsync(video, TimeSpan.Zero, destination, TestContext.Current.CancellationToken);

        Assert.False(taken);
        Assert.False(File.Exists(destination));
        Assert.True(
            watch.Elapsed < CourseThumbnailPolicy.Deadline,
            $"it waited {watch.Elapsed} on a file that never opened.");
    }

    /// <summary>Will not open at all, which is a machine with no decoder for this container.</summary>
    private sealed class RefusingCapture : LibVlcVideoFrameGrabber.IFrameCapture
    {
        public bool Start(TimeSpan at) => false;

        public LibVlcVideoFrameGrabber.CapturedFrame? WaitForFrame(TimeSpan deadline) =>
            throw new InvalidOperationException("Nothing asks for a frame from a file that never opened.");

        public void Dispose()
        {
        }
    }

    /// <summary>Opens fine and never hands over a frame, which is the failure the deadline is for.</summary>
    private sealed class SilentCapture : LibVlcVideoFrameGrabber.IFrameCapture
    {
        public bool Start(TimeSpan at) => true;

        public LibVlcVideoFrameGrabber.CapturedFrame? WaitForFrame(TimeSpan deadline)
        {
            // It waits the deadline out rather than answering at once: what is being measured is
            // that the caller gives up, and an instant refusal would pass on a caller that waits
            // for ever.
            Thread.Sleep(deadline);
            return null;
        }

        public void Dispose()
        {
        }
    }
}
