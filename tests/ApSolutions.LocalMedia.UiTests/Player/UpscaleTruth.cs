// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The truth an enlargement is measured against, and the decoded frame made from it, shared by
/// every harness that ranks an enlargement so that they all rank against the same picture.
/// </summary>
/// <remarks>
/// It lived inside <see cref="VideoUpscaleFidelityTests"/> until `ENG-022` needed a second harness
/// to measure candidates the player does not draw. A copy would have let the two drift apart, and
/// then the second harness's control — that it reproduces the first one's figures — would compare
/// two different pictures and mean nothing.
/// </remarks>
internal static class UpscaleTruth
{
    /// <summary>A four-times enlargement, which is 1080p on a 4K screen and worse for 720p.</summary>
    public const int Width = 320;
    public const int Height = 240;
    public const int Scale = 4;
    public const int SourceWidth = Width / Scale;
    public const int SourceHeight = Height / Scale;

    /// <summary>
    /// The truth: greyscale, off the source's grid on purpose, and with detail no shrink can keep.
    /// </summary>
    /// <remarks>
    /// Drawn by supersampling sixteen points per pixel, because a real frame is antialiased and a
    /// hard binary truth would let a hard threshold score well for the wrong reason.
    /// </remarks>
    public static byte[] Draw()
    {
        var truth = new byte[Width * Height];
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                var total = 0d;
                for (var subRow = 0; subRow < 4; subRow++)
                {
                    for (var subColumn = 0; subColumn < 4; subColumn++)
                    {
                        var x = column + ((subColumn + 0.5) / 4);
                        var y = row + ((subRow + 0.5) / 4);
                        total += Sample(x, y);
                    }
                }

                truth[(row * Width) + column] = (byte)Math.Clamp(total / 16 * 255, 0, 255);
            }
        }

        return truth;
    }

    /// <summary>
    /// The truth shrunk by four with a box filter, which is what a decoded frame of it would be.
    /// </summary>
    public static byte[] Shrink(byte[] truth)
    {
        var small = new byte[SourceWidth * SourceHeight];
        for (var row = 0; row < SourceHeight; row++)
        {
            for (var column = 0; column < SourceWidth; column++)
            {
                var total = 0;
                for (var subRow = 0; subRow < Scale; subRow++)
                {
                    for (var subColumn = 0; subColumn < Scale; subColumn++)
                    {
                        total += truth[
                            (((row * Scale) + subRow) * Width) + (column * Scale) + subColumn];
                    }
                }

                small[(row * SourceWidth) + column] = (byte)(total / (Scale * Scale));
            }
        }

        return small;
    }

    /// <summary>What the truth is at one point, in quarters of the frame.</summary>
    private static double Sample(double x, double y) => x switch
    {
        // A vertical edge off the source grid: 41.7 is not a multiple of four, so shrinking loses it.
        < Width / 4.0 => x >= 41.7 ? 1 : 0,

        // A diagonal, which is where a hard threshold draws its staircase.
        < Width / 2.0 => (y - 37.3) > (0.62 * (x - (Width / 4.0))) ? 1 : 0,

        // Rings whose frequency climbs outwards, so some of them are finer than the source.
        < Width * 3 / 4.0 =>
            0.5 + (0.5 * Math.Sin(
                (Math.Pow(x - 200.4, 2) + Math.Pow(y - 120.9, 2)) / 190.0)),

        // Fine diagonal stripes, the classic thing an enlargement smears.
        _ => 0.5 + (0.5 * Math.Sin((x * 0.47) + (y * 0.11))),
    };
}
