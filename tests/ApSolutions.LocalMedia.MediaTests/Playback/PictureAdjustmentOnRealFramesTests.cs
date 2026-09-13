// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Runs the picture adjustment over a real frame through the production conversion, and writes both
/// pictures out so a person can look at them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The frame comes from outside the tree on purpose.</b> Judging this feature needs somebody's
/// own material — the file that prompted it measured a mean luma of 28 out of 235 — and a repository
/// that promises nothing personal inside it cannot carry one. So the frame arrives by environment
/// variable and the test skips without it, which also keeps it out of CI's way.
/// </para>
/// <para>
/// Prepare a frame with FFmpeg and point the variables at it:
/// <code>
/// ffmpeg -ss 00:47:00 -i &lt;file&gt; -frames:v 1 -pix_fmt uyvy422 -f rawvideo frame.uyvy
/// $env:APREELUME_DIAG_UYVY = 'frame.uyvy'; $env:APREELUME_DIAG_SIZE = '720x404'
/// </code>
/// The two BGRA files it writes convert back with
/// <c>ffmpeg -f rawvideo -pix_fmt bgra -s WxH -i out.bgra out.png</c>.
/// </para>
/// </remarks>
public sealed class PictureAdjustmentOnRealFramesTests
{
    [Fact]
    public void A_real_frame_comes_out_of_the_production_conversion_lifted_and_still_monotonic()
    {
        var path = Environment.GetEnvironmentVariable("APREELUME_DIAG_UYVY");
        var size = Environment.GetEnvironmentVariable("APREELUME_DIAG_SIZE");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(size),
            "No frame was supplied; set APREELUME_DIAG_UYVY and APREELUME_DIAG_SIZE to judge this one.");

        var parts = size!.Split('x');
        var width = PackedYuvConverter.AlignWidth(int.Parse(parts[0], CultureInfo.InvariantCulture));
        var height = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var packed = File.ReadAllBytes(path!);
        var sourceStride = width * PackedYuvConverter.SourceBytesPerPixel;
        var destinationStride = width * PackedYuvConverter.DestinationBytesPerPixel;

        Assert.True(
            packed.Length >= sourceStride * height,
            $"the frame is {packed.Length} bytes, short of the {sourceStride * height} that {size} needs.");

        var matrix = YuvMatrixPolicy.For(height);
        var plain = new byte[destinationStride * height];
        var lifted = new byte[plain.Length];

        PackedYuvConverter.UyvyToBgra(
            packed, plain, width, height, sourceStride, destinationStride, matrix);
        PackedYuvConverter.UyvyToBgra(
            packed,
            lifted,
            width,
            height,
            sourceStride,
            destinationStride,
            matrix,
            new PictureAdjustment(Brightness: 0d, Contrast: 1d, Gamma: 1.6d).BuildLookup());

        var directory = Path.Combine("artifacts", "test-results", "PLY-018");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "frame-plain.bgra"), plain);
        File.WriteAllBytes(Path.Combine(directory, "frame-lifted.bgra"), lifted);

        // The measurement, and it is the mean and not a pixel: one pixel can rise by accident, a
        // whole frame cannot. A file this dark is exactly where the difference has to show.
        var before = Mean(plain);
        var after = Mean(lifted);
        Assert.True(
            after > before * 1.4,
            $"the frame only went from a mean of {before:F1} to {after:F1}, which nobody would notice.");

        // And nothing clipped: a lift that pushes the bright end to white trades shadows for
        // highlights, which looks worse and not better.
        //
        // The budget counts PIXELS and so does Saturated, which is what this got wrong until an
        // audit measured it: dividing the byte length by 100 made the real bar four per cent of the
        // picture while the message said one. A diagnostic nobody runs is exactly where a number
        // four times looser than it claims survives.
        var pixels = lifted.Length / PackedYuvConverter.DestinationBytesPerPixel;
        Assert.True(
            Saturated(lifted) < Saturated(plain) + (pixels / 100),
            "the lift pushed more than a hundredth of the picture to pure white.");
    }

    private static double Mean(byte[] bgra)
    {
        var total = 0L;
        for (var at = 0; at < bgra.Length; at += 4)
        {
            total += bgra[at] + bgra[at + 1] + bgra[at + 2];
        }

        return total / (double)(bgra.Length / 4 * 3);
    }

    private static int Saturated(byte[] bgra)
    {
        var count = 0;
        for (var at = 0; at < bgra.Length; at += 4)
        {
            if (bgra[at] == 255 && bgra[at + 1] == 255 && bgra[at + 2] == 255)
            {
                count++;
            }
        }

        return count;
    }
}
