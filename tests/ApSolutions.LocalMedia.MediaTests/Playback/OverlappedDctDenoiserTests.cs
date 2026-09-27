// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// The noise reducer that runs on the luma of every frame before the tone curve, measured against a
/// picture a real encoder broke into blocks and against an independent implementation of the same
/// method.
/// </summary>
/// <remarks>
/// <para>
/// <b>The blocks come from a real encoder, not from a drawing.</b> A test that painted its own
/// eight-pixel steps would measure a filter against the idea of a block; MPEG-4 at a coarse
/// quantiser leaves the grid, the ringing and the flat patches a lifted gamma shows, and the uncompressed
/// source of the same frame is the truth the result is scored against.
/// </para>
/// <para>
/// <b>The second opinion is FFmpeg's <c>dctdnoiz</c></b>, which implements the same thresholded
/// overlapped DCT and is only an instrument here. It works on colour, not on luma: it turns RGB into
/// three decorrelated planes with an orthonormal three-point DCT and thresholds each at three sigma.
/// Fed a grey picture, the first plane is the grey level times √3 and the other two are empty, so its
/// sigma on grey is that sigma divided by √3 here. <b>That mapping is read, not measured</b>: the
/// agreement test is what measures it.
/// </para>
/// </remarks>
public sealed class OverlappedDctDenoiserTests
{
    private const int Width = 320;
    private const int Height = 240;
    private const int Stride = Width * PackedYuvConverter.SourceBytesPerPixel;
    // Drawn larger and scaled down, so that its edges fall between the encoder's blocks and not on
    // them: drawn at 320×240, testsrc2 is built of squares on the same eight-pixel grid, and its
    // uncompressed frame measured 2.26 on the blockiness scale where a clean picture should read 1.
    private const string Source = "-f lavfi -i testsrc2=size=331x247:rate=25,scale=320:240 -frames:v 1";

    // Every sample of the scene lives under one folder named after it: the toolchain keeps what it
    // made once, so a changed recipe under an old name would quietly hand back the old picture.
    private const string Scene = "denoise/testsrc2-331x247";

    // Strong enough that the reference visibly moves this picture, which is mostly flat colour:
    // at 3 it moved it 0.81 levels, too little for an agreement to mean anything.
    private const int ReferenceSigma = 6;

    [Fact]
    public void A_strength_of_zero_leaves_every_byte_where_it_was()
    {
        var frame = NoisyFrame(seed: 1);
        var before = frame.ToArray();

        new OverlappedDctDenoiser().Denoise(frame, Width, Height, Stride, strength: 0f);

        Assert.Equal(before, frame);
    }

    [Fact]
    public void The_chroma_is_never_touched_and_the_luma_is()
    {
        var frame = NoisyFrame(seed: 2);
        var before = frame.ToArray();

        new OverlappedDctDenoiser().Denoise(frame, Width, Height, Stride, strength: 3f);

        var lumaChanged = 0;
        for (var at = 0; at < frame.Length; at++)
        {
            if (at % 2 == 0)
            {
                Assert.True(frame[at] == before[at], $"chroma byte {at} moved from {before[at]} to {frame[at]}.");
            }
            else if (frame[at] != before[at])
            {
                lumaChanged++;
            }
        }

        // The other half of the control: a reducer that returned early would pass the chroma check
        // above perfectly.
        Assert.True(lumaChanged > Width * Height / 2, $"only {lumaChanged} luma samples moved.");
    }

    [Fact]
    public void Padding_past_the_visible_width_is_left_alone()
    {
        const int paddedStride = Stride + 16;
        var frame = new byte[paddedStride * Height];
        var noisy = NoisyFrame(seed: 3);
        for (var row = 0; row < Height; row++)
        {
            noisy.AsSpan(row * Stride, Stride).CopyTo(frame.AsSpan(row * paddedStride));
            frame.AsSpan((row * paddedStride) + Stride, 16).Fill(0xAB);
        }

        new OverlappedDctDenoiser().Denoise(frame, Width, Height, paddedStride, strength: 3f);

        for (var row = 0; row < Height; row++)
        {
            Assert.All(frame.AsSpan((row * paddedStride) + Stride, 16).ToArray(), value => Assert.Equal(0xAB, value));
        }
    }

    [Fact]
    public async Task Blocks_a_real_encoder_left_are_flattened_and_the_picture_moves_towards_the_truth()
    {
        var (blocky, truth) = await EncodedFrameAndTruthAsync();
        var medium = blocky.ToArray();
        var strong = blocky.ToArray();

        new OverlappedDctDenoiser().Denoise(medium, Width, Height, Stride, strength: 3f);
        new OverlappedDctDenoiser().Denoise(strong, Width, Height, Stride, OverlappedDctDenoiser.MaximumStrength);

        // The instrument first: the uncompressed frame has to read clean and the encoded one blocky,
        // or the numbers below measure the scene and not the reducer.
        var clean = Blockiness(Luma(truth));
        var before = Blockiness(Luma(blocky));
        Assert.True(clean < 1.0 && before > 1.5, $"the scale read {clean:F2} on the clean frame and {before:F2} on the encoded one.");

        // Measured on 2026-09-27: 1.66 → 1.28 at 3 and 1.10 at 5, with the clean frame at 0.73. The
        // encoder at this quantiser threw most of the detail away, so the distance to the truth
        // shrinks by little; what has to hold is that it shrinks and does not grow.
        var atMedium = Blockiness(Luma(medium));
        var atStrong = Blockiness(Luma(strong));
        Assert.True(atMedium < before * 0.85, $"blockiness only went from {before:F2} to {atMedium:F2} at 3.");
        Assert.True(atStrong < atMedium, $"the strongest setting left {atStrong:F2}, no better than {atMedium:F2} at 3.");

        var errorBefore = RootMeanSquare(Luma(blocky), Luma(truth));
        var errorAfter = RootMeanSquare(Luma(strong), Luma(truth));
        Assert.True(errorAfter < errorBefore, $"the distance to the uncompressed frame went from {errorBefore:F2} to {errorAfter:F2}.");
    }

    [Fact]
    public async Task It_agrees_with_an_independent_implementation_of_the_same_method()
    {
        var (blocky, _) = await EncodedFrameAndTruthAsync();
        var reference = await ReferenceLumaAsync(blocky);
        var denoised = blocky.ToArray();

        new OverlappedDctDenoiser().Denoise(denoised, Width, Height, Stride, strength: ReferenceSigma / MathF.Sqrt(3f));

        // Every pixel, border included, within one level of the reference. Not an average: measured
        // on 2026-09-27 the two differ by 0 or 1 everywhere and ours sits half a level higher on
        // average, because FFmpeg truncates when it writes the byte and this rounds, as the tone
        // curve does. An RMS would call that 0.7 and hide a real disagreement of the same size.
        var ours = Luma(denoised);
        var theirs = reference;
        var original = Luma(blocky);
        var apart = 0;
        var movedByReference = 0;
        for (var at = 0; at < ours.Length; at++)
        {
            apart += Math.Abs(ours[at] - theirs[at]) > 1f ? 1 : 0;
            movedByReference += Math.Abs(original[at] - theirs[at]) > 1f ? 1 : 0;
        }

        Assert.True(movedByReference > ours.Length / 20, $"the reference moved only {movedByReference} pixels by more than a level: nothing to agree on.");
        Assert.True(apart == 0, $"{apart} pixels sit more than one level from dctdnoiz.");
    }

    [Fact]
    public async Task At_a_step_of_two_it_agrees_with_the_reference_at_the_same_overlap()
    {
        // The step is how far apart the windows start, and FFmpeg calls the same thing an overlap of
        // eight minus the step. At this size every row and column of windows ends exactly on the
        // border, so the two implementations cover the same pixels the same number of times.
        var (blocky, _) = await EncodedFrameAndTruthAsync();
        var reference = await ReferenceLumaAsync(blocky, overlap: 6);
        var denoised = blocky.ToArray();

        new OverlappedDctDenoiser().Denoise(denoised, Width, Height, Stride, ReferenceSigma / MathF.Sqrt(3f), step: 2);

        var ours = Luma(denoised);
        var original = Luma(blocky);
        var apart = 0;
        var movedByReference = 0;
        for (var at = 0; at < ours.Length; at++)
        {
            apart += Math.Abs(ours[at] - reference[at]) > 1f ? 1 : 0;
            movedByReference += Math.Abs(original[at] - reference[at]) > 1f ? 1 : 0;
        }

        Assert.True(movedByReference > ours.Length / 20, $"the reference moved only {movedByReference} pixels by more than a level.");
        Assert.True(apart == 0, $"{apart} pixels sit more than one level from dctdnoiz at an overlap of 6.");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task A_coarser_step_still_takes_the_blocks_out(int step)
    {
        var (blocky, _) = await EncodedFrameAndTruthAsync();
        var denoised = blocky.ToArray();

        new OverlappedDctDenoiser().Denoise(denoised, Width, Height, Stride, OverlappedDctDenoiser.MaximumStrength, step);

        var before = Blockiness(Luma(blocky));
        var after = Blockiness(Luma(denoised));
        Assert.True(after < before * 0.85, $"at a step of {step} blockiness only went from {before:F2} to {after:F2}.");
    }

    [Fact]
    public void The_last_columns_and_rows_are_filtered_when_the_step_does_not_land_on_them()
    {
        // 322 wide and 242 high leave 314 and 234 pixels before the last window, neither a multiple
        // of four, so a step of four that only started windows on its own grid would never reach the
        // right edge nor the bottom one.
        const int width = 322;
        const int height = 242;
        var frame = new byte[width * 2 * height];
        new Random(8).NextBytes(frame);
        for (var at = 1; at < frame.Length; at += 2)
        {
            frame[at] = (byte)(120 + (frame[at] % 24));
        }

        var before = frame.ToArray();
        new OverlappedDctDenoiser().Denoise(frame, width, height, width * 2, 3f, step: 4);

        var rightEdgeMoved = 0;
        var bottomEdgeMoved = 0;
        for (var row = 0; row < height; row++)
        {
            var at = (row * width * 2) + ((width - 1) * 2) + 1;
            rightEdgeMoved += frame[at] != before[at] ? 1 : 0;
        }

        for (var x = 0; x < width; x++)
        {
            var at = ((height - 1) * width * 2) + (x * 2) + 1;
            bottomEdgeMoved += frame[at] != before[at] ? 1 : 0;
        }

        Assert.True(rightEdgeMoved > height / 2, $"only {rightEdgeMoved} of the right edge's {height} pixels moved.");
        Assert.True(bottomEdgeMoved > width / 2, $"only {bottomEdgeMoved} of the bottom edge's {width} pixels moved.");
    }

    [Fact]
    public void A_film_never_makes_more_working_rows_than_it_has_threads()
    {
        // What the first version got wrong: it rented its working rows from the shared pool, and
        // until that pool had filled, every frame allocated about 1.6 MB and the collections that
        // caused showed up as frames three times slower than the rest. Now each thread keeps one set
        // for the whole film. «None after the first frame» is not the promise and was measured
        // false: the thread pool adds threads gradually, and inside the test run the count went from
        // 8 to 10 after twenty frames. The promise is the ceiling.
        var frame = NoisyFrame(seed: 10);
        var denoiser = new OverlappedDctDenoiser();
        for (var frameIndex = 0; frameIndex < 40; frameIndex++)
        {
            denoiser.Denoise(frame, Width, Height, Stride, 3f);
        }

        Assert.InRange(denoiser.WorkingBuffersCreated, 1, Math.Min(Environment.ProcessorCount, 16));
    }

    [Fact]
    public void Changing_the_step_on_the_same_size_filters_as_a_fresh_reducer_would()
    {
        // The player changes the step mid-film when a frame does not fit, on a picture of the same
        // size, and the windows' positions and weights have to change with it: kept from the old
        // step, the picture would be averaged over windows that were never filtered.
        var reused = new OverlappedDctDenoiser();
        reused.Denoise(NoisyFrame(seed: 11), Width, Height, Stride, 3f, step: 1);
        var afterChange = NoisyFrame(seed: 12);
        reused.Denoise(afterChange, Width, Height, Stride, 3f, step: 2);

        var fresh = NoisyFrame(seed: 12);
        new OverlappedDctDenoiser().Denoise(fresh, Width, Height, Stride, 3f, step: 2);

        Assert.Equal(fresh, afterChange);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(8)]
    public void A_step_other_than_one_two_or_four_is_refused(int step)
    {
        var frame = NoisyFrame(seed: 9);

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new OverlappedDctDenoiser().Denoise(frame, Width, Height, Stride, 3f, step));
    }

    [Fact]
    public void A_picture_smaller_than_one_block_is_left_alone()
    {
        var frame = new byte[6 * 2 * 6];
        Random.Shared.NextBytes(frame);
        var before = frame.ToArray();

        new OverlappedDctDenoiser().Denoise(frame, 6, 6, 12, strength: 3f);

        Assert.Equal(before, frame);
    }

    [Fact]
    public void A_frame_of_another_size_after_the_first_is_filtered_in_full()
    {
        // The reducer keeps its working planes between frames; a second size that reused the first
        // one's would read and write past the rows it was given, or leave half of them untouched.
        var denoiser = new OverlappedDctDenoiser();
        denoiser.Denoise(NoisyFrame(seed: 4), Width, Height, Stride, strength: 3f);

        const int smallWidth = 64;
        const int smallHeight = 48;
        var small = new byte[smallWidth * 2 * smallHeight];
        new Random(5).NextBytes(small);
        var before = small.ToArray();

        denoiser.Denoise(small, smallWidth, smallHeight, smallWidth * 2, strength: 3f);

        var lastRow = (smallHeight - 1) * smallWidth * 2;
        Assert.NotEqual(before.AsSpan(lastRow).ToArray(), small.AsSpan(lastRow).ToArray());
    }

    [Theory]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    [InlineData(OverlappedDctDenoiser.MaximumStrength + 0.5f)]
    public void A_strength_outside_the_range_is_refused(float strength)
    {
        var frame = NoisyFrame(seed: 6);

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new OverlappedDctDenoiser().Denoise(frame, Width, Height, Stride, strength));
    }

    [Fact]
    public void A_geometry_the_buffer_does_not_hold_is_refused()
    {
        var frame = NoisyFrame(seed: 7);
        var denoiser = new OverlappedDctDenoiser();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => denoiser.Denoise(frame, Width - 1, Height, Stride, 3f));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => denoiser.Denoise(frame, Width, 0, Stride, 3f));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => denoiser.Denoise(frame, Width, Height, Stride - 2, 3f));
        _ = Assert.Throws<ArgumentException>(() => denoiser.Denoise(frame[1..], Width, Height, Stride, 3f));
        _ = Assert.Throws<ArgumentNullException>(() => denoiser.Denoise(null!, Width, Height, Stride, 3f));
    }

    private static byte[] NoisyFrame(int seed)
    {
        var random = new Random(seed);
        var frame = new byte[Stride * Height];
        for (var row = 0; row < Height; row++)
        {
            for (var x = 0; x < Width; x++)
            {
                var at = (row * Stride) + (x * 2);
                frame[at] = (byte)(96 + random.Next(64));
                frame[at + 1] = (byte)Math.Clamp(((x / 8) + (row / 8)) % 2 == 0 ? 80 + random.Next(-12, 13) : 160 + random.Next(-12, 13), 16, 235);
            }
        }

        return frame;
    }

    private static async Task<(byte[] Blocky, byte[] Truth)> EncodedFrameAndTruthAsync()
    {
        Assert.SkipWhen(!MediaToolchain.HasEncoder("mpeg4"), MediaToolchain.MissingEncoderReason);
        var cancellation = TestContext.Current.CancellationToken;
        var encoded = await MediaToolchain.EnsureSampleAsync(
            $"{Scene}/blocky.avi", $"{Source} -c:v mpeg4 -q:v 24", cancellation);
        var blocky = await MediaToolchain.EnsureSampleAsync(
            $"{Scene}/blocky.uyvy", $"-i \"{encoded}\" -frames:v 1 -pix_fmt uyvy422 -f rawvideo", cancellation);
        var truth = await MediaToolchain.EnsureSampleAsync(
            $"{Scene}/truth.uyvy", $"{Source} -vf format=yuv420p,format=uyvy422 -f rawvideo", cancellation);
        return (await File.ReadAllBytesAsync(blocky, cancellation), await File.ReadAllBytesAsync(truth, cancellation));
    }

    /// <summary>
    /// What <c>dctdnoiz</c> makes of the same luma. It goes in as RGB with the three channels equal
    /// because that is the filter's own format: handing it grey or YUV makes FFmpeg convert first,
    /// and the first attempt did exactly that — the conversion stretched limited range to full and
    /// the «reference» sat 9.5 levels away from a picture it had barely filtered.
    /// <para>
    /// And it runs on one thread. FFmpeg cuts the picture into one horizontal slice per thread and
    /// starts the grid of windows again at the top of each, so with a step above one its windows no
    /// longer sat where the article puts them: measured on 2026-09-27, rows 0 to 29 agreed and from
    /// row 30 — 240 rows over eight threads — 4 % of the pixels did not.
    /// </para>
    /// </summary>
    private static async Task<float[]> ReferenceLumaAsync(byte[] blocky, int overlap = 7)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var luma = Luma(blocky);
        var grey = new byte[Width * Height * 3];
        for (var at = 0; at < luma.Length; at++)
        {
            grey[at * 3] = grey[(at * 3) + 1] = grey[(at * 3) + 2] = (byte)luma[at];
        }

        var input = Path.Combine(MediaToolchain.OutputRoot, Scene, "blocky-luma.rgb");
        await File.WriteAllBytesAsync(input, grey, cancellation);
        var reference = await MediaToolchain.EnsureSampleAsync(
            $"{Scene}/dctdnoiz-{ReferenceSigma}-overlap-{overlap}-one-thread.rgb",
            $"-filter_threads 1 -f rawvideo -pix_fmt rgb24 -s {Width}x{Height} -i \"{input}\" -vf dctdnoiz=sigma={ReferenceSigma}:overlap={overlap} -pix_fmt rgb24 -f rawvideo",
            cancellation);
        var bytes = await File.ReadAllBytesAsync(reference, cancellation);
        Assert.Equal(grey.Length, bytes.Length);
        var green = new float[Width * Height];
        for (var at = 0; at < green.Length; at++)
        {
            green[at] = bytes[(at * 3) + 1];
        }

        return green;
    }

    private static float[] Luma(byte[] packed)
    {
        var luma = new float[Width * Height];
        for (var row = 0; row < Height; row++)
        {
            for (var x = 0; x < Width; x++)
            {
                luma[(row * Width) + x] = packed[(row * Stride) + (x * 2) + 1];
            }
        }

        return luma;
    }

    /// <summary>
    /// How much larger a step across an eight-pixel block boundary is than a step anywhere else, in
    /// both directions: 1 is a picture with no grid in it.
    /// </summary>
    private static double Blockiness(float[] luma)
    {
        double across = 0, within = 0;
        long acrossCount = 0, withinCount = 0;
        for (var row = 0; row < Height; row++)
        {
            for (var x = 1; x < Width; x++)
            {
                var step = Math.Abs(luma[(row * Width) + x] - luma[(row * Width) + x - 1]);
                if (x % 8 == 0)
                {
                    across += step;
                    acrossCount++;
                }
                else
                {
                    within += step;
                    withinCount++;
                }
            }
        }

        for (var row = 1; row < Height; row++)
        {
            for (var x = 0; x < Width; x++)
            {
                var step = Math.Abs(luma[(row * Width) + x] - luma[((row - 1) * Width) + x]);
                if (row % 8 == 0)
                {
                    across += step;
                    acrossCount++;
                }
                else
                {
                    within += step;
                    withinCount++;
                }
            }
        }

        return across / acrossCount / (within / withinCount);
    }

    private static double RootMeanSquare(float[] left, float[] right, int margin = 0)
    {
        double total = 0;
        long count = 0;
        for (var row = margin; row < Height - margin; row++)
        {
            for (var x = margin; x < Width - margin; x++)
            {
                var difference = left[(row * Width) + x] - right[(row * Width) + x];
                total += difference * difference;
                count++;
            }
        }

        return Math.Sqrt(total / count);
    }
}
