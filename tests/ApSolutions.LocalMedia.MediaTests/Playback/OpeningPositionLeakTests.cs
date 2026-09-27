// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
/// previous video goes on playing — and announcing its position about every 260 ms — until then. Slow
/// openings let seventeen and twenty-two positions of the previous video out, with no duration, to
/// the session already listening for the next one. On a local disk an opening is usually shorter than
/// that interval, which is why the leak was looked for once and not seen; from a network share it is
/// routinely longer.
/// </para>
/// <para>
/// <b>The slow opening is fabricated out of work, not out of a slow disk.</b> The first version used a
/// large file of noise, and it was slow here only because this repository lives on a network share:
/// on the server's local disk the same opening took 6 ms and the test, rightly, refused to call that a
/// measurement. A Matroska file with two hundred thousand chapters makes the engine read every one of
/// them while it analyses, which costs time in proportion — 80,000 took 365 ms on a local disk — on
/// any machine. And before the result counts, the test checks that one opening really did last longer
/// than the longest gap measured between two positions: only then is a clean result the engine keeping
/// quiet and not the scene failing to happen.
/// </para>
/// </remarks>
[Collection(ProcessResourceSuites.Name)]
public sealed class OpeningPositionLeakTests
{
    private const string PlayingRelativePath = "low-res-enhancement/mpeg2-480p-noisy.mkv";

    private const string PlayingRecipe =
        "-f lavfi -i testsrc2=size=720x480:rate=25,noise=alls=10:allf=t -t 6 " +
        "-f lavfi -i sine=frequency=440:duration=6 " +
        "-c:v mpeg2video -b:v 900k -pix_fmt yuv420p -c:a mp2 -b:a 128k -shortest";

    private const int Chapters = 200_000;

    private const int Attempts = 3;

    [Fact]
    public async Task No_position_is_announced_while_the_next_video_is_opening()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var cancellation = TestContext.Current.CancellationToken;
        var playing = await MediaToolchain.EnsureSampleAsync(PlayingRelativePath, PlayingRecipe, cancellation);
        var slow = await SlowToOpenAsync(cancellation);

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
            progressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            stamps.Clear();
            await engine.OpenAsync(new PlaybackRequest(new MediaFileId(Guid.NewGuid()), playing), cancellation);
            await engine.PlayAsync(cancellation);
            Assert.True(
                await Task.WhenAny(progressed.Task, Task.Delay(TimeSpan.FromSeconds(20), cancellation)) == progressed.Task,
                "the video to switch away from never got past its first second.");

            var gaps = stamps.Zip(stamps.Skip(1), (first, second) => Stopwatch.GetElapsedTime(first, second)).ToArray();
            if (gaps.Length > 0 && gaps.Max() > widestGap)
            {
                widestGap = gaps.Max();
            }

            var clock = Stopwatch.StartNew();
            Volatile.Write(ref opening, 1);
            try
            {
                await engine.OpenAsync(new PlaybackRequest(new MediaFileId(Guid.NewGuid()), slow), cancellation);
            }
            finally
            {
                Volatile.Write(ref opening, 0);
            }

            if (clock.Elapsed > longest)
            {
                longest = clock.Elapsed;
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

    private static async Task<string> SlowToOpenAsync(CancellationToken cancellation)
    {
        var metadata = new StringBuilder(";FFMETADATA1\n");
        for (var chapter = 0; chapter < Chapters; chapter++)
        {
            metadata.Append(CultureInfo.InvariantCulture, $"[CHAPTER]\nTIMEBASE=1/1000\nSTART={chapter}\nEND={chapter + 1}\ntitle=Chapter {chapter}\n");
        }

        var chapters = await MediaToolchain.EnsureTextCompanionAsync(
            $"opening/chapters-{Chapters}.txt", metadata.ToString(), cancellation);
        return await MediaToolchain.EnsureSampleAsync(
            $"opening/chapters-{Chapters}.mkv",
            // The duration lives inside the source and not in a -t: placed before the second input, a
            // -t limits that input and leaves the test pattern endless, which is how the first run of
            // this recipe wrote 13.9 GB in 47 minutes before anyone stopped it.
            $"-f lavfi -i testsrc2=size=320x240:rate=25:duration=2 -i \"{chapters}\" -map 0 -map_metadata 1 -map_chapters 1 -c:v mpeg2video",
            cancellation);
    }
}
