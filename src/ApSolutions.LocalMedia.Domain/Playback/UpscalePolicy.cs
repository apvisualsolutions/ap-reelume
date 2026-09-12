// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// Whether a decoded picture is worth enlarging before it is drawn, and to exactly what size.
/// </summary>
public readonly record struct UpscaleDecision(bool ShouldUpscale, int TargetWidth, int TargetHeight);

/// <summary>
/// PLY-016's decision, kept away from the graphics card that carries it out. Which picture gains
/// from being enlarged is arithmetic that runs on any machine; whether a particular card can do the
/// enlarging is not, and the two are deliberately separate.
/// </summary>
/// <remarks>
/// The target is the box the picture occupies on screen, never the surface around it. A 4:3 episode
/// on a 16:9 display is drawn in a box narrower than the display, and enlarging to the display's
/// width would hand the scaler a stretched picture; a 720p episode in a 1280-wide window is already
/// as large as it will be drawn, and enlarging it spends a graphics card to throw the result away.
/// </remarks>
public static class UpscalePolicy
{
    /// <summary>
    /// The decision for a picture of <paramref name="sourceWidth"/> by <paramref name="sourceHeight"/>
    /// drawn into a surface of <paramref name="surfaceWidth"/> by <paramref name="surfaceHeight"/>,
    /// all in whole pixels. A degenerate size on either side enlarges nothing rather than guessing.
    /// </summary>
    public static UpscaleDecision Decide(
        int sourceWidth,
        int sourceHeight,
        int surfaceWidth,
        int surfaceHeight)
    {
        var box = VideoFitPolicy.Fit(sourceWidth, sourceHeight, surfaceWidth, surfaceHeight);

        // Rounded before it is compared, because the target is what the scaler will be asked for:
        // a box a fraction of a pixel wider than the source rounds back to the source, and calling
        // that an enlargement would light the indicator over a picture nothing happened to.
        var width = (int)Math.Round(box.Width, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(box.Height, MidpointRounding.AwayFromZero);

        // An empty box is asked about first, and not because it is tidy: a negative source width
        // makes the box empty while staying below every positive target, so comparing the two
        // sizes alone answered "enlarge this to nothing at all".
        //
        // Past that, the box keeps the shape the picture was decoded with, so both of its axes grow
        // by the same factor and either one of them settles the question.
        return box.Width <= 0 || width <= sourceWidth
            ? default
            : new UpscaleDecision(true, width, height);
    }
}
