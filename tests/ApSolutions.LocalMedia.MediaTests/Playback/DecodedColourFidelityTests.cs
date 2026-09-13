// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Runtime.InteropServices;

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;

using LibVLCSharp.Shared;

using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// What a pure colour looks like after the whole decode, through the real engine and the real
/// frame source rather than the converter on its own.
/// </summary>
/// <remarks>
/// <para>
/// This is the test the defect needed and did not have. Until 2026-09-12 every picture was decoded
/// with the BT.601 matrix, so anything 720 lines or taller — which is BT.709 — came out wrong:
/// measured on the samples below, pure red arrived at 231 instead of 253. The converter's own suite
/// could not catch it, because it fed the converter samples and never asked where they came from.
/// </para>
/// <para>
/// The standard definition case is not decoration. Decoding SD with the HD matrix is the same
/// defect pointing the other way, and a suite that only proved the HD case would sign it off.
/// </para>
/// </remarks>
[Trait("Category", "RealMedia")]
public sealed class DecodedColourFidelityTests
{
    private const string HighDefinitionSample = "COLOUR/hd-red-bt709.mp4";

    private const string StandardDefinitionSample = "COLOUR/sd-red-bt601.mp4";

    // 716 visible lines, which H.264 encodes in a 720-line buffer and crops. It is the only sample
    // here where the picture's height and the decoder's aligned buffer land on opposite sides of the
    // threshold, and without it choosing the matrix from the buffer instead of the picture passes
    // every other test in this file.
    private const string CroppedSample = "COLOUR/cropped-red-bt601.mp4";

    // A flat field of pure red, which is the colour the two matrices disagree about most, encoded
    // with the space each resolution really uses.
    //
    // The conversion is forced through the scale filter, and that is the whole difference between a
    // sample and a decoration. Measured on 2026-09-12: asking for -colorspace bt709 alone only
    // *labels* the stream — the picture inside stayed BT.601, ffprobe happily reported bt709, and a
    // first version of this suite spent an afternoon proving that LibVLC normalises colour, which it
    // does not. A label is not a sample. What is inside has to be converted, and the test below
    // checks that it was.
    private const string HighDefinitionRecipe =
        "-f lavfi -i color=c=red:s=1280x720:r=25:d=2 " +
        "-vf format=rgb24,scale=out_color_matrix=bt709:out_range=tv,format=yuv420p " +
        "-c:v libx264 -preset ultrafast " +
        "-colorspace bt709 -color_primaries bt709 -color_trc bt709 -x264-params colormatrix=bt709 -frames:v 50";

    private const string StandardDefinitionRecipe =
        "-f lavfi -i color=c=red:s=720x576:r=25:d=2 " +
        "-vf format=rgb24,scale=out_color_matrix=bt601:out_range=tv,format=yuv420p " +
        "-c:v libx264 -preset ultrafast " +
        "-colorspace smpte170m -color_primaries bt470bg -color_trc smpte170m " +
        "-x264-params colormatrix=smpte170m -frames:v 50";

    private const string CroppedRecipe =
        "-f lavfi -i color=c=red:s=1280x716:r=25:d=2 " +
        "-vf format=rgb24,scale=out_color_matrix=bt601:out_range=tv,format=yuv420p " +
        "-c:v libx264 -preset ultrafast " +
        "-colorspace smpte170m -color_primaries bt470bg -color_trc smpte170m " +
        "-x264-params colormatrix=smpte170m -frames:v 50";

    [Fact]
    public async Task Pure_red_in_a_high_definition_picture_arrives_as_pure_red()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            HighDefinitionSample,
            HighDefinitionRecipe,
            TestContext.Current.CancellationToken);

        var centre = await DecodeCentrePixelAsync(path);

        // 253 rather than 255: the round trip through limited-range chroma costs two levels, and
        // ffmpeg decoding the same file lands in the same place. What is being refused is the 231
        // the BT.601 matrix produced on this sample.
        Assert.InRange(centre.Red, 248, 255);
        Assert.InRange(centre.Green, 0, 6);
        Assert.InRange(centre.Blue, 0, 6);
    }

    [Fact]
    public async Task Pure_red_in_a_standard_definition_picture_still_arrives_as_pure_red()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            StandardDefinitionSample,
            StandardDefinitionRecipe,
            TestContext.Current.CancellationToken);

        var centre = await DecodeCentrePixelAsync(path);

        Assert.InRange(centre.Red, 248, 255);
        Assert.InRange(centre.Green, 0, 6);
        Assert.InRange(centre.Blue, 0, 6);
    }

    [Fact]
    public async Task A_picture_just_under_the_threshold_follows_its_own_height_and_not_the_decoders_buffer()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            CroppedSample,
            CroppedRecipe,
            TestContext.Current.CancellationToken);

        var centre = await DecodeCentrePixelAsync(path);

        // 716 visible lines is standard definition; the 720-line buffer the decoder offers is not
        // the picture. Choosing from the buffer decodes this with BT.709 and green climbs to 25.
        Assert.InRange(centre.Red, 248, 255);
        Assert.InRange(centre.Green, 0, 6);
        Assert.InRange(centre.Blue, 0, 6);
    }

    [Fact]
    public async Task The_engine_decodes_each_picture_with_the_matrix_its_height_calls_for()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var hd = await MediaToolchain.EnsureSampleAsync(
            HighDefinitionSample,
            HighDefinitionRecipe,
            TestContext.Current.CancellationToken);
        var sd = await MediaToolchain.EnsureSampleAsync(
            StandardDefinitionSample,
            StandardDefinitionRecipe,
            TestContext.Current.CancellationToken);

        var fromHd = await DecodeCentrePixelAsync(hd);
        var fromSd = await DecodeCentrePixelAsync(sd);

        // The control that makes the two tests above mean something. The samples are encoded with
        // different matrices, so an engine that used one matrix for both would land the two reds in
        // different places — and one of them would be the defect. Agreeing here is the evidence that
        // each file was decoded with its own.
        Assert.InRange(Math.Abs(fromHd.Red - fromSd.Red), 0, 4);
        Assert.InRange(Math.Abs(fromHd.Green - fromSd.Green), 0, 4);
        Assert.InRange(Math.Abs(fromHd.Blue - fromSd.Blue), 0, 4);
    }

    [Fact]
    public async Task The_two_samples_really_carry_different_matrices_or_none_of_this_measures_anything()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var hd = await MediaToolchain.EnsureSampleAsync(
            HighDefinitionSample,
            HighDefinitionRecipe,
            TestContext.Current.CancellationToken);
        var sd = await MediaToolchain.EnsureSampleAsync(
            StandardDefinitionSample,
            StandardDefinitionRecipe,
            TestContext.Current.CancellationToken);

        var fromHd = await DecodeCentreSampleAsync(hd);
        var fromSd = await DecodeCentreSampleAsync(sd);

        // The control on the instrument rather than on the code, and the one this suite was missing
        // when it first went green for the wrong reason. Pure red is Y=63 U=102 in BT.709 and Y=81
        // U=90 in BT.601, so two samples that really differ hand LibVLC different bytes (measured
        // through the encoder: Y=62 U=102 and Y=81 U=90). If a future
        // recipe goes back to labelling instead of converting, the two collapse onto the same
        // numbers and every test above keeps passing while proving nothing.
        Assert.True(
            Math.Abs(fromHd.Luma - fromSd.Luma) >= 10,
            $"the two samples reached the decoder with the same luma ({fromHd.Luma} and {fromSd.Luma}); " +
            "the recipes are labelling a colour space rather than encoding one");
        Assert.True(
            Math.Abs(fromHd.U - fromSd.U) >= 6,
            $"the two samples reached the decoder with the same chroma ({fromHd.U} and {fromSd.U})");
    }

    /// <summary>
    /// Plays the sample through the production engine and reads the centre pixel of the first frame
    /// that is not the black one a decoder can publish before it has anything to show.
    /// </summary>
    private static async Task<(int Red, int Green, int Blue)> DecodeCentrePixelAsync(string path)
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        using var collector = new CentrePixelCollector();
        engine.FrameRendered += collector.OnFrameRendered;

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var pixel = await collector.CollectAsync();
        await engine.StopAsync(TestContext.Current.CancellationToken);
        return pixel;
    }

    /// <summary>
    /// Reads the packed sample LibVLC hands out before any conversion of ours touches it, through a
    /// sink wired the way the engine wires its own. This is what tells a real BT.709 file from one
    /// that merely says so.
    /// </summary>
    private static async Task<(int Luma, int U, int V)> DecodeCentreSampleAsync(string path)
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        var player = factory.CreateMediaPlayer();
        var media = factory.CreateMedia(path);
        var found = new TaskCompletionSource<(int Luma, int U, int V)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        nint buffer = 0;
        var width = 0;
        var height = 0;
        var stride = 0;

        var format = new MediaPlayer.LibVLCVideoFormatCb(
            (ref nint _, nint chroma, ref uint w, ref uint h, ref uint pitches, ref uint lines) =>
            {
                w = (uint)PackedYuvConverter.AlignWidth((int)w);
                Marshal.Copy("UYVY"u8.ToArray(), 0, chroma, 4);
                pitches = w * PackedYuvConverter.SourceBytesPerPixel;
                lines = h;
                (width, height, stride) = ((int)w, (int)h, (int)pitches);
                buffer = Marshal.AllocHGlobal(stride * height);
                return 1;
            });
        var cleanup = new MediaPlayer.LibVLCVideoCleanupCb((ref nint _) => { });
        var locker = new MediaPlayer.LibVLCVideoLockCb((nint _, nint planes) =>
        {
            Marshal.WriteIntPtr(planes, buffer);
            return nint.Zero;
        });
        var display = new MediaPlayer.LibVLCVideoDisplayCb((nint _, nint _) =>
        {
            var packed = new byte[stride * height];
            Marshal.Copy(buffer, packed, 0, packed.Length);
            var at = ((height / 2) * stride) + (((width / 2) / 2) * 4);

            // Same reason as the BGRA collector: a leading black frame is not a measurement.
            if (packed[at + 1] < 20 && packed[at] is > 120 and < 136)
            {
                return;
            }

            _ = found.TrySetResult((packed[at + 1], packed[at], packed[at + 2]));
        });

        player.SetVideoFormatCallbacks(format, cleanup);
        player.SetVideoCallbacks(locker, null, display);
        player.Media = media;
        try
        {
            _ = player.Play();
            var reached = await Task.WhenAny(
                found.Task,
                Task.Delay(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
            player.Stop();
            Assert.True(ReferenceEquals(reached, found.Task), $"no packed frame arrived for '{path}'");
            return await found.Task;
        }
        finally
        {
            factory.DeferRelease(media);
            factory.ReleaseMediaPlayer(player);
            if (buffer != 0)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private sealed class CentrePixelCollector : IDisposable
    {
        private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(20);
        private readonly Lock _sync = new();
        private readonly TaskCompletionSource<(int Red, int Green, int Blue)> _lit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnFrameRendered(object? sender, VideoFrameEventArgs args)
        {
            var span = args.Pixels.Span;
            var at = ((args.Height / 2) * args.Stride) + ((args.Width / 2) * 4);
            if (at + 3 >= span.Length)
            {
                return;
            }

            var pixel = (Red: (int)span[at + 2], Green: (int)span[at + 1], Blue: (int)span[at]);

            // A decoder can publish a black frame before it has decoded anything, and reading it
            // would measure nothing at all while looking exactly like a measurement.
            if (pixel is { Red: < 16, Green: < 16, Blue: < 16 })
            {
                return;
            }

            lock (_sync)
            {
                _ = _lit.TrySetResult(pixel);
            }
        }

        public async Task<(int Red, int Green, int Blue)> CollectAsync()
        {
            var reached = await Task.WhenAny(_lit.Task, Task.Delay(FirstFrameTimeout));
            Assert.True(
                ReferenceEquals(reached, _lit.Task),
                "no frame with any colour in it arrived before the timeout");
            return await _lit.Task;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _ = _lit.TrySetCanceled();
            }
        }
    }
}
