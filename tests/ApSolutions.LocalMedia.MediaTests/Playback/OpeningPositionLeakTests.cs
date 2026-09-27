// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Collections.Concurrent;
using System.Diagnostics;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// While the engine is opening the next video, it announces no position: the video still playing is
/// the one before, and whoever listens is already the next one's session.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 2026-09-27: the engine analyses the next file before it hands it to the player, and the
/// previous video goes on playing — and announcing its position about every 260 ms — until then. An
/// opening of 304 ms let one position of the previous video out, at 1.3 s and with no duration. The
/// next episode's progress is listening by then, so it is the next episode that is told where it is.
/// On local disk an opening is usually shorter than that interval, which is why the leak was looked
/// for once and not seen; from a network share it is routinely longer.
/// </para>
/// <para>
/// <b>The slow opening is fabricated with a large file of noise that nothing has read yet</b>: the
/// engine probes it before refusing it. How long that takes depends on the disk cache, so the scene is
/// repeated with a fresh file until one opening lasts longer than the longest gap measured between
/// two positions. Only then does a clean result mean anything: an opening that long is certain to have
/// straddled at least one position of the video still playing.
/// </para>
/// </remarks>
[Collection(ProcessResourceSuites.Name)]
public sealed class OpeningPositionLeakTests
{
    private const string SampleRelativePath = "low-res-enhancement/mpeg2-480p-noisy.mkv";

    private const string SampleRecipe =
        "-f lavfi -i testsrc2=size=720x480:rate=25,noise=alls=10:allf=t -t 6 " +
        "-f lavfi -i sine=frequency=440:duration=6 " +
        "-c:v mpeg2video -b:v 900k -pix_fmt yuv420p -c:a mp2 -b:a 128k -shortest";

    private const int Attempts = 8;

    [Fact]
    public async Task No_position_is_announced_while_the_next_video_is_opening()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var cancellation = TestContext.Current.CancellationToken;
        var playing = await MediaToolchain.EnsureSampleAsync(SampleRelativePath, SampleRecipe, cancellation);
        var folder = Path.Combine(MediaToolchain.OutputRoot, "opening");
        Directory.CreateDirectory(folder);

        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(cancellation);

        var opening = 0;
        var leaked = new ConcurrentQueue<PlaybackPositionChangedEventArgs>();
        var stamps = new ConcurrentQueue<long>();
        TaskCompletionSource? progressed = null;
        engine.PositionChanged += (_, args) =>
        {
            if (Volatile.Read(ref opening) == 1)
            {
                leaked.Enqueue(args);
                return;
            }

            stamps.Enqueue(Stopwatch.GetTimestamp());
            if (args.Position > TimeSpan.FromSeconds(1))
            {
                _ = progressed?.TrySetResult();
            }
        };

        var longest = TimeSpan.Zero;
        var widestGap = TimeSpan.Zero;
        for (var attempt = 0; attempt < Attempts && longest <= widestGap + TimeSpan.FromMilliseconds(50); attempt++)
        {
            var slow = Path.Combine(folder, $"slow-to-probe-{Guid.NewGuid():n}.ts");
            await WriteNoiseAsync(slow, cancellation);
            try
            {
                progressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                stamps.Clear();
                await engine.OpenAsync(new PlaybackRequest(new MediaFileId(Guid.NewGuid()), playing), cancellation);
                await engine.PlayAsync(cancellation);
                Assert.True(
                    await Task.WhenAny(progressed.Task, Task.Delay(TimeSpan.FromSeconds(20), cancellation)) == progressed.Task,
                    "the video to switch away from never got past its first second.");

                var gaps = stamps.Zip(stamps.Skip(1), (first, second) => Stopwatch.GetElapsedTime(first, second)).ToArray();
                if (gaps.Length > 0)
                {
                    widestGap = gaps.Max() > widestGap ? gaps.Max() : widestGap;
                }

                var clock = Stopwatch.StartNew();
                Volatile.Write(ref opening, 1);
                try
                {
                    await engine.OpenAsync(new PlaybackRequest(new MediaFileId(Guid.NewGuid()), slow), cancellation);
                }
                catch (PlaybackFailureException)
                {
                    // Expected: noise is not a video. What it says while finding that out is the test.
                }
                finally
                {
                    Volatile.Write(ref opening, 0);
                }

                longest = clock.Elapsed > longest ? clock.Elapsed : longest;
            }
            finally
            {
                File.Delete(slow);
            }
        }

        // The instrument first: without an opening longer than the gap between two positions, a clean
        // result is the scene failing to happen and not the engine keeping quiet.
        Assert.True(
            longest > widestGap + TimeSpan.FromMilliseconds(50),
            $"no opening in {Attempts} lasted longer than the {widestGap.TotalMilliseconds:F0} ms between two positions (longest {longest.TotalMilliseconds:F0} ms).");
        Assert.True(
            leaked.IsEmpty,
            $"{leaked.Count} position(s) of the previous video were announced while the next was opening, the first at {leaked.FirstOrDefault()?.Position} with duration {leaked.FirstOrDefault()?.Duration?.ToString() ?? "none"}.");
    }

    private static async Task WriteNoiseAsync(string path, CancellationToken cancellation)
    {
        var block = new byte[1024 * 1024];
        new Random(path.GetHashCode(StringComparison.Ordinal)).NextBytes(block);
        await using var output = File.Create(path);
        for (var megabyte = 0; megabyte < 192; megabyte++)
        {
            await output.WriteAsync(block, cancellation);
        }
    }
}
