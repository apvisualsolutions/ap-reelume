// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Collections.Concurrent;
using System.Runtime.Intrinsics;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Takes the compression blocks out of the luma of a packed 4:2:2 frame, in place, before the tone
/// curve stretches them into view.
/// </summary>
/// <remarks>
/// <para>
/// <b>The method is the thresholded overlapped DCT</b>: 8×8 windows of the picture, overlapping, are
/// taken to the frequency domain, the coefficients too small to be anything but noise are dropped,
/// and the windows are taken back and averaged where they overlap. It is written from Guoshen Yu and
/// Guillermo Sapiro, <i>DCT Image Denoising: a Simple and Effective Image Denoising Algorithm</i>,
/// Image Processing On Line 1 (2011), on the idea of Onur Nosratinia, <i>Enhancement of
/// JPEG-compressed images by re-application of JPEG</i> (1999). Their reference code is GPL and was
/// not read; this is the article's algorithm and nothing else.
/// </para>
/// <para>
/// <b>Why this one</b>, measured on the file that prompted it before a line was written: it was the
/// only reducer that took the blocks away and left the detail where it was. The spatial part does
/// the work, so each frame is filtered on its own, which is also what keeps it from smearing motion.
/// </para>
/// <para>
/// <b>Only the luma is filtered</b>, and the chroma bytes are never written: the grid the eye sees
/// after a gamma lift is in the brightness, and the curve that lifts it reads nothing else.
/// </para>
/// <para>
/// <b>The step is how far apart the windows start.</b> One is the article's method, a window at every
/// position; two and four keep a quarter and a sixteenth of the windows and cost about that much less.
/// The caller chooses it from what a frame costs on the machine it runs on: measured on 2026-09-27, a
/// 1080p frame at a step of one took 11 ms on 28 threads and 24 ms on four, and a picture that misses
/// its moment on screen is worse than one a little less clean. The last row and column of windows are
/// always included, whatever the step, so no edge of the picture goes unfiltered.
/// </para>
/// <para>
/// <b>How it is made fast enough to run on every frame.</b> The two-dimensional transform is a
/// transform of the rows followed by one of the columns, and the row half of a window is the row
/// half of eight pixel rows that the windows above and below share. So each pixel row is transformed
/// once, the windows do only the column half on the way in and on the way out, and what comes back is
/// gathered still in row coefficients and turned into pixels once per row.
/// </para>
/// <para>
/// The work is split into horizontal bands of window rows, processed in two passes so that no two
/// bands running at once write the same row: a band writes seven rows past its own, and bands of
/// eight with one skipped between them never meet. Eight and not more because the bands are the unit
/// the threads share out, and with sixteen the second wave of each pass left most of them idle.
/// </para>
/// </remarks>
public sealed class OverlappedDctDenoiser
{
    /// <summary>The strongest setting accepted, in luma levels of noise: the control's own maximum.</summary>
    public const float MaximumStrength = (float)PictureAdjustment.MaximumDenoise;

    /// <summary>The coarsest step accepted: one window every four pixels each way.</summary>
    public const int CoarsestStep = DenoiseStepPolicy.CoarsestStep;

    private const int Size = 8;
    private const int BandRows = 8;

    /// <summary>
    /// How many noise levels a coefficient has to stand above to be kept: three, as in the article,
    /// because an orthonormal transform leaves white noise with the same spread in every coefficient.
    /// </summary>
    private const float ThresholdInSigmas = 3f;

    /// <summary>The orthonormal DCT-II of size eight, row k holding the k-th cosine.</summary>
    private static readonly float[] Basis = BuildBasis();

    /// <summary>Row k of the basis: what coefficient k contributes to the eight pixels.</summary>
    private static readonly Vector256<float>[] BasisRows = Rows(Basis);

    /// <summary>Column n of the basis: what pixel n contributes to the eight coefficients.</summary>
    private static readonly Vector256<float>[] BasisColumns = Rows(Transpose(Basis));

    /// <summary>Every entry of the basis as a vector, row after row, ready to multiply.</summary>
    private static readonly Vector256<float>[] BasisBroadcast = Array.ConvertAll(Basis, Vector256.Create);

    /// <summary>The same, for the transposed basis the way back multiplies by.</summary>
    private static readonly Vector256<float>[] TransposedBroadcast = Array.ConvertAll(Transpose(Basis), Vector256.Create);

    /// <summary>
    /// Past sixteen threads a 1080p frame got no faster, measured on 2026-09-27 on 28, and every
    /// thread that takes part holds its own working rows.
    /// </summary>
    private static readonly ParallelOptions Parallelism = new()
    {
        MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 16),
    };

    /// <summary>
    /// Working rows the bands borrow and give back, owned here rather than rented from the shared
    /// pool: renting them made the first second of every film pay for filling that pool, and the
    /// garbage collections it caused showed up as frames of 33 to 49 ms among frames of 14.
    /// </summary>
    private readonly ConcurrentBag<BandBuffers> _buffers = [];

    private Layout? _layout;
    private int _buffersCreated;
    private float[] _luma = [];
    private float[] _sum = [];

    /// <summary>
    /// Filters the luma of <paramref name="height"/> rows of UYVY, in place.
    /// </summary>
    /// <param name="packedFrame">The packed picture, at least <paramref name="stride"/> per row.</param>
    /// <param name="width">Visible pixels per row, even as the format demands.</param>
    /// <param name="height">Rows.</param>
    /// <param name="stride">Bytes from one row to the next; anything past the width is not touched.</param>
    /// <param name="strength">The noise to remove, in luma levels; zero leaves every byte alone.</param>
    /// <param name="step">Pixels between one window and the next: 1, 2 or 4.</param>
    public void Denoise(byte[] packedFrame, int width, int height, int stride, float strength, int step = 1)
    {
        ArgumentNullException.ThrowIfNull(packedFrame);
        ArgumentOutOfRangeException.ThrowIfNotEqual(width, PackedYuvConverter.AlignWidth(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width * PackedYuvConverter.SourceBytesPerPixel);
        if (float.IsNaN(strength) || strength is < 0f or > MaximumStrength)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "The strength runs from zero to the maximum.");
        }

        if (step is not (1 or 2 or CoarsestStep))
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "The step is one, two or four pixels.");
        }

        if (packedFrame.Length < stride * height)
        {
            throw new ArgumentException("The reducer was given fewer rows than it was told to filter.", nameof(packedFrame));
        }

        // A picture narrower or shorter than one window has no window to filter, and zero is the
        // promise that the frame comes out exactly as it went in.
        if (strength == 0f || width < Size || height < Size)
        {
            return;
        }

        var layout = Prepare(width, height, step);

        // The copy in and the copy out are split by rows too: done on one thread they cost five of
        // the milliseconds a 1080p frame took, measured on 2026-09-27.
        var luma = _luma;
        var sum = _sum;
        _ = Parallel.For(0, height, Parallelism, row =>
        {
            var source = packedFrame.AsSpan(row * stride, width * 2);
            var target = luma.AsSpan(row * width, width);
            sum.AsSpan(row * width, width).Clear();
            for (var x = 0; x < width; x++)
            {
                target[x] = source[(x * 2) + 1];
            }
        });

        var threshold = ThresholdInSigmas * strength;
        for (var pass = 0; pass < 2; pass++)
        {
            var first = pass;
            _ = Parallel.For(0, (layout.Bands.Length - first + 1) / 2, Parallelism, index =>
            {
                var buffers = _buffers.TryTake(out var taken) ? taken : CreateBuffers(layout);
                try
                {
                    FilterBand(layout, layout.Bands[first + (index * 2)], luma, sum, threshold, buffers);
                }
                finally
                {
                    _buffers.Add(buffers);
                }
            });
        }

        _ = Parallel.For(0, height, Parallelism, row =>
        {
            var vertical = layout.RowWeights[row];
            var target = packedFrame.AsSpan(row * stride, width * 2);
            var total = sum.AsSpan(row * width, width);
            var horizontal = layout.ColumnWeights;
            for (var x = 0; x < width; x++)
            {
                var level = MathF.Round(total[x] * vertical * horizontal[x]);
                target[(x * 2) + 1] = (byte)Math.Clamp(level, 0f, 255f);
            }
        });
    }

    /// <summary>
    /// How many sets of working rows this reducer has ever made. It exists for the same reason the
    /// engine counts its live media: the saving is invisible from outside. Counting the process's
    /// allocated bytes instead was tried and could not be believed — inside the test run it read
    /// 77 KB a frame one time in three while a process of its own read 13, because whatever else the
    /// process was doing counted too.
    /// </summary>
    public int WorkingBuffersCreated => Volatile.Read(ref _buffersCreated);

    private BandBuffers CreateBuffers(Layout layout)
    {
        _ = Interlocked.Increment(ref _buffersCreated);
        return new BandBuffers(layout.BufferLength);
    }

    /// <summary>
    /// The windows' positions and weights for this size and step, worked out once and kept until
    /// either changes; the working rows sized for the old layout go with it.
    /// </summary>
    private Layout Prepare(int width, int height, int step)
    {
        if (_layout is { } current && current.Width == width && current.Height == height && current.Step == step)
        {
            return current;
        }

        _buffers.Clear();
        var pixels = width * height;
        if (_luma.Length != pixels)
        {
            _luma = new float[pixels];
            _sum = new float[pixels];
        }

        var layout = new Layout(width, height, step);
        _layout = layout;
        return layout;
    }

    /// <summary>
    /// Filters every window of <paramref name="band"/> and adds what comes back to the sum, over the
    /// band's own rows and the seven below it.
    /// </summary>
    private static void FilterBand(Layout layout, Band band, float[] luma, float[] sum, float threshold, BandBuffers buffers)
    {
        var width = layout.Width;
        var starts = layout.ColumnStarts;
        var columns = starts.Length;
        var pixelRows = band.PixelRows;
        var forward = buffers.Forward;
        var backward = buffers.Backward;
        Array.Clear(backward, 0, pixelRows * columns);

        // The row half, once per pixel row: coefficient k of the eight pixels starting at each start.
        for (var row = 0; row < pixelRows; row++)
        {
            var pixels = luma.AsSpan((band.Top + row) * width, width);
            var coefficients = forward.AsSpan(row * columns, columns);
            for (var column = 0; column < columns; column++)
            {
                var x = starts[column];
                var accumulated = Vector256<float>.Zero;
                for (var n = 0; n < Size; n++)
                {
                    accumulated += Vector256.Create(pixels[x + n]) * BasisColumns[n];
                }

                coefficients[column] = accumulated;
            }
        }

        // The column half, per window, both ways, with the threshold in between. The eight rows are
        // read once into locals and summed as a tree: written as a loop that read them again for
        // every output and added them one after another, each sum waited for the last, and this half
        // cost 143 ms of a 172 ms frame on one thread. Written out, 55.
        var limit = Vector256.Create(threshold);
        Span<Vector256<float>> block = stackalloc Vector256<float>[Size];
        foreach (var windowTop in band.WindowTops)
        {
            var y = windowTop - band.Top;
            for (var column = 0; column < columns; column++)
            {
                var at = (y * columns) + column;
                var f0 = forward[at];
                var f1 = forward[at + columns];
                var f2 = forward[at + (2 * columns)];
                var f3 = forward[at + (3 * columns)];
                var f4 = forward[at + (4 * columns)];
                var f5 = forward[at + (5 * columns)];
                var f6 = forward[at + (6 * columns)];
                var f7 = forward[at + (7 * columns)];
                for (var i = 0; i < Size; i++)
                {
                    var weights = BasisBroadcast.AsSpan(i * Size, Size);
                    var coefficients =
                        Vector256.FusedMultiplyAdd(f1, weights[1], f0 * weights[0])
                        + Vector256.FusedMultiplyAdd(f3, weights[3], f2 * weights[2])
                        + (Vector256.FusedMultiplyAdd(f5, weights[5], f4 * weights[4])
                           + Vector256.FusedMultiplyAdd(f7, weights[7], f6 * weights[6]));

                    // The average level needs no protection from this: it is eight times the window's
                    // mean, and limited-range luma never goes below 16.
                    block[i] = Vector256.ConditionalSelect(
                        Vector256.GreaterThanOrEqual(Vector256.Abs(coefficients), limit),
                        coefficients,
                        Vector256<float>.Zero);
                }

                var c0 = block[0];
                var c1 = block[1];
                var c2 = block[2];
                var c3 = block[3];
                var c4 = block[4];
                var c5 = block[5];
                var c6 = block[6];
                var c7 = block[7];
                for (var r = 0; r < Size; r++)
                {
                    var weights = TransposedBroadcast.AsSpan(r * Size, Size);
                    backward[at + (r * columns)] +=
                        Vector256.FusedMultiplyAdd(c1, weights[1], c0 * weights[0])
                        + Vector256.FusedMultiplyAdd(c3, weights[3], c2 * weights[2])
                        + (Vector256.FusedMultiplyAdd(c5, weights[5], c4 * weights[4])
                           + Vector256.FusedMultiplyAdd(c7, weights[7], c6 * weights[6]));
                }
            }
        }

        // And the row half back, once per pixel row, into the eight pixels each window covers.
        for (var row = 0; row < pixelRows; row++)
        {
            var total = sum.AsSpan((band.Top + row) * width, width);
            var coefficients = backward.AsSpan(row * columns, columns);
            for (var column = 0; column < columns; column++)
            {
                var accumulated = Vector256<float>.Zero;
                var window = coefficients[column];
                for (var k = 0; k < Size; k++)
                {
                    accumulated += Vector256.Create(window.GetElement(k)) * BasisRows[k];
                }

                var target = total.Slice(starts[column], Size);
                (Vector256.Create((ReadOnlySpan<float>)target) + accumulated).CopyTo(target);
            }
        }
    }

    /// <summary>
    /// Every <paramref name="step"/>-th start from zero, and the last one possible whether or not the
    /// step lands on it.
    /// </summary>
    private static int[] Starts(int length, int step)
    {
        var last = length - Size;
        var starts = new List<int>((last / step) + 2);
        for (var start = 0; start <= last; start += step)
        {
            starts.Add(start);
        }

        if (starts[^1] != last)
        {
            starts.Add(last);
        }

        return [.. starts];
    }

    /// <summary>One over the number of windows covering each pixel along one axis.</summary>
    private static float[] Weights(int length, int[] starts)
    {
        var counts = new int[length];
        foreach (var start in starts)
        {
            for (var at = start; at < start + Size; at++)
            {
                counts[at]++;
            }
        }

        return Array.ConvertAll(counts, count => 1f / count);
    }

    private static float[] BuildBasis()
    {
        var basis = new float[Size * Size];
        for (var k = 0; k < Size; k++)
        {
            var scale = k == 0 ? Math.Sqrt(1d / Size) : Math.Sqrt(2d / Size);
            for (var n = 0; n < Size; n++)
            {
                basis[(k * Size) + n] = (float)(scale * Math.Cos(Math.PI * ((2 * n) + 1) * k / (2 * Size)));
            }
        }

        return basis;
    }

    private static float[] Transpose(float[] matrix)
    {
        var transposed = new float[Size * Size];
        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                transposed[(column * Size) + row] = matrix[(row * Size) + column];
            }
        }

        return transposed;
    }

    private static Vector256<float>[] Rows(float[] matrix)
    {
        var rows = new Vector256<float>[Size];
        for (var row = 0; row < Size; row++)
        {
            rows[row] = Vector256.Create(matrix.AsSpan(row * Size, Size));
        }

        return rows;
    }

    /// <summary>Where the windows of one frame size and step start, and how each pixel is weighed.</summary>
    private sealed class Layout
    {
        public Layout(int width, int height, int step)
        {
            Width = width;
            Height = height;
            Step = step;
            ColumnStarts = Starts(width, step);
            ColumnWeights = Weights(width, ColumnStarts);
            var rowStarts = Starts(height, step);
            RowWeights = Weights(height, rowStarts);

            // Bands of window rows eight apart, each holding the window tops that fall inside it. None
            // is ever empty: the coarsest step is four, so every eight rows hold at least one start,
            // and the last band holds the last start, which is always included.
            var windowRows = height - Size + 1;
            var bandCount = (windowRows + BandRows - 1) / BandRows;
            Bands = new Band[bandCount];
            for (var band = 0; band < bandCount; band++)
            {
                var top = band * BandRows;
                var tops = Array.FindAll(rowStarts, start => start >= top && start < top + BandRows);
                Bands[band] = new Band(top, tops, tops[^1] - top + Size);
            }

            BufferLength = (BandRows - 1 + Size) * ColumnStarts.Length;
        }

        public int Width { get; }

        public int Height { get; }

        public int Step { get; }

        public int[] ColumnStarts { get; }

        public float[] ColumnWeights { get; }

        public float[] RowWeights { get; }

        public Band[] Bands { get; }

        public int BufferLength { get; }
    }

    /// <summary>The window rows of one band, and how many pixel rows they reach.</summary>
    private sealed record Band(int Top, int[] WindowTops, int PixelRows);

    /// <summary>The row coefficients one band works in, both ways.</summary>
    private sealed class BandBuffers(int length)
    {
        public Vector256<float>[] Forward { get; } = new Vector256<float>[length];

        public Vector256<float>[] Backward { get; } = new Vector256<float>[length];
    }
}
