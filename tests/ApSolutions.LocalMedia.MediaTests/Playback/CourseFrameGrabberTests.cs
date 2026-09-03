// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
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
public sealed class CourseFrameGrabberTests : IAsyncLifetime
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

        var taken = await new LibVlcCourseFrameGrabber(_factory)
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

        var taken = await new LibVlcCourseFrameGrabber(_factory).TryCaptureAsync(
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
        var taken = await new LibVlcCourseFrameGrabber(_factory).TryCaptureAsync(
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
        var taken = await new LibVlcCourseFrameGrabber(_ => new SilentCapture())
            .TryCaptureAsync(video, TimeSpan.Zero, Path.Combine(_root, "out.png"), TestContext.Current.CancellationToken);

        Assert.False(taken);
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(10),
            $"it waited {watch.Elapsed} for a frame that was never coming.");
    }

    /// <summary>Opens fine and never hands over a frame, which is the failure the deadline is for.</summary>
    private sealed class SilentCapture : LibVlcCourseFrameGrabber.IFrameCapture
    {
        public bool Start(TimeSpan at) => true;

        public LibVlcCourseFrameGrabber.CapturedFrame? WaitForFrame(TimeSpan deadline)
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
