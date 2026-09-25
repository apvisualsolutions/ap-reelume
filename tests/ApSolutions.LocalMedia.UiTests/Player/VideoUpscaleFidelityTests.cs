// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// How faithfully an enlarged picture reconstructs the picture it came from, measured against a
/// truth this file draws itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a second yardstick exists at all, which is the whole point of this file.</b> The ramp width
/// in <see cref="VideoUpscaleQualityTests"/> counts how many pixels across a hard edge come back
/// neither black nor white, and <i>less is better</i> — so its perfect score is <b>zero</b>. Zero is
/// exactly what nearest-neighbour gives, and the test next door says so literally. A yardstick whose
/// best score belongs to the worst filter cannot be optimised against: pushing towards it pushes
/// towards a hard threshold, which scores beautifully and draws staircased edges. Measured on
/// 2026-09-13, trying to win on ramp width alone produced a picture the owner had already rejected
/// by eye.
/// </para>
/// <para>
/// <b>What replaces it is the protocol the super-resolution literature uses</b>, which is a distance
/// to a reference rather than a property of an edge: draw the truth, shrink it to make the decoded
/// frame, enlarge that back, and measure how far the result landed from the truth. The known
/// objection to it — that the reference images in published datasets are themselves of poor quality,
/// so metrics reward resembling a bad reference — does not reach here, because <b>the truth is
/// synthesised in this file</b> and is exact by construction.
/// </para>
/// <para>
/// <b>The trap that cost the first draft, and the reason for the second control below.</b> The first
/// truth drawn here had every edge on a multiple of four, so shrinking it by four lost nothing and
/// nearest-neighbour reconstructed it byte for byte — score <c>0.50</c> against bilinear's
/// <c>30.54</c>. A pattern aligned to the source grid rewards the same wrong thing the ramp does. So
/// the truth here is deliberately off-grid — an edge at <c>41.7</c>, a diagonal of slope
/// <c>0.62</c>, rings whose frequency climbs — and one test does nothing but assert that
/// nearest-neighbour scores <b>worse</b> than the composition. If that ever passes the other way,
/// this yardstick has turned into the one it replaced.
/// </para>
/// </remarks>
public sealed class VideoUpscaleFidelityTests
{
    /// <summary>A four-times enlargement, which is 1080p on a 4K screen and worse for 720p.</summary>
    private const int SurfaceWidth = UpscaleTruth.Width;
    private const int SurfaceHeight = UpscaleTruth.Height;
    private const int SourceWidth = UpscaleTruth.SourceWidth;
    private const int SourceHeight = UpscaleTruth.SourceHeight;

    /// <summary>
    /// The case the feature exists for, stated as fidelity rather than as edge width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The floor is <b>35 %</b> and it is a prediction that was written down before the code changed,
    /// which is the only reason it is a test and not a rubber stamp. Modelling the renderer's
    /// arithmetic offline reproduced the archived edge profile <c>3,86,169,252</c> exactly, and then
    /// said: sampling the shader's own input linearly and sharpening at 0.6 — what shipped earlier on
    /// 2026-09-13 — would land 29 % closer to the truth than the composition, while sampling it through
    /// a sharpened cubic would land meaningfully further. <b>This harness measured 30,4 % for the first
    /// and 35,7 % for the chain that replaced it</b>, and the floor sits between them. It was red before
    /// the change it guards.
    /// </para>
    /// <para>
    /// <b>It was 30 for half an hour and the detour is the lesson.</b> The owner reported tonal squares
    /// around lettering, the sharpened cubic's ringing was the obvious suspect, and this floor came down
    /// so a gentler coefficient could pass. Then he ran the control: <b>his gamma was at 1.5, and at 1.0
    /// the squares stop</b> — the artefact was banding from an 8-bit tone curve (`ENG-021`) and had
    /// nothing to do with this. <b>A gate loosened to accommodate a misdiagnosis is worse than the
    /// defect</b>, so it is back at 35, which the shipping chain clears at 35,7 %.
    /// </para>
    /// <para>
    /// <b>Why not higher.</b> An unbounded mask on the same cubic reaches <b>41,9 %</b> and overshoots
    /// 26 levels against the 22 that <c>SkiaUpscaleDrawOperationTests</c> allows. Those six points are
    /// spent on a halo that cannot happen, and raising this floor towards 42 would be asking for it back.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void An_enlarged_picture_lands_closer_to_the_truth_than_the_composition_puts_it()
    {
        var truth = UpscaleTruth.Draw();
        var composition = Distance(truth, upscale: false, BitmapInterpolationMode.LowQuality);
        var enhanced = Distance(truth, upscale: true, BitmapInterpolationMode.LowQuality);

        Assert.True(
            composition > 8,
            $"The composition lands {composition:F2} from the truth, and a truth the source can "
            + "already hold measures near zero for everything — so this scene is not testing an "
            + "enlargement at all.");

        var gained = (composition - enhanced) / composition * 100;
        Assert.True(
            gained >= 35,
            $"The enhancement lands {enhanced:F2} from the truth against the composition's "
            + $"{composition:F2}, which is {gained:F1} % closer. The floor is 35 % and the chain that "
            + "ships measures 35,7 %, so the margin is 0,7 points: one coefficient down, C=0.7, "
            + "measures 33,2 % and fails here. Below the floor the shader is sampling its input the "
            + "soft way — plain linear at this strength reaches only 13,7 %, measured.");
    }

    /// <summary>
    /// The control that proves this yardstick is not the ramp width wearing a different name.
    /// </summary>
    /// <remarks>
    /// Nearest-neighbour is the sharpest thing this renderer can draw and the worst thing it can
    /// draw, and only one of the two yardsticks can tell. The ramp calls it perfect; this one has to
    /// call it worse than the blur. The comparison is strict, so «they came out equal» — which is what
    /// a scene with no off-grid detail looks like — fails here too. The other half of the pair, that the
    /// ramp width calls the hard step perfect, is asserted next door in
    /// <c>VideoUpscaleQualityTests.Turning_interpolation_off_gives_the_hard_step_that_proves_the_others_are_smoothing</c>.
    /// </remarks>
    [AvaloniaFact]
    public void Nearest_neighbour_lands_further_from_the_truth_than_the_blur_does()
    {
        var truth = UpscaleTruth.Draw();
        var blocky = Distance(truth, upscale: false, BitmapInterpolationMode.None);
        var blurred = Distance(truth, upscale: false, BitmapInterpolationMode.LowQuality);

        Assert.True(
            blocky > blurred,
            $"Nearest-neighbour landed {blocky:F2} from the truth and the blur landed {blurred:F2}. "
            + "This yardstick has to punish the hard step the ramp width rewards; if it no longer "
            + "does, the truth has drifted onto the source's own grid and measures nothing.");
    }

    /// <summary>
    /// The instrument floor: a picture that needs no enlarging has to come back as itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this, every number above could be measuring a misalignment — one pixel of offset
    /// between the capture and the truth would charge every candidate the same toll and still rank
    /// them in the same order, so the ranking would look healthy while the distances were noise.
    /// Measured: shifting the comparison by one pixel takes this from 0 to <b>15,44</b>.
    /// </para>
    /// <para>
    /// <b>The enhancement is deliberately left off here, and saying so matters.</b> This read
    /// <c>upscale: true</c> until the gate auditor measured it on 2026-09-13 and found the distance
    /// identical either way — at 1:1 the chain answers <c>CompositionBilinear</c>, so the enhanced
    /// route is never entered and the argument was decoration that read like coverage. What this test
    /// measures is the yardstick's alignment, nothing else; that the chain leaves a 1:1 picture
    /// untouched is asserted byte for byte in <c>VideoUpscaleQualityTests</c>.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_picture_that_needs_no_enlarging_comes_back_as_itself()
    {
        var truth = UpscaleTruth.Draw();
        var distance = Distance(truth, upscale: false, BitmapInterpolationMode.LowQuality, enlarge: false);

        Assert.True(
            distance < 1,
            $"A picture handed over at the size it is drawn came back {distance:F2} away from "
            + "itself, so the yardstick is measuring alignment and not enlargement.");
    }

    /// <summary>
    /// The case the owner reported by eye: a grainy source must not come out worse than untouched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this test exists, which is the same reason the file does.</b> On 2026-09-13 the owner
    /// looked at the enhancement and said it had improved a lot but still showed «artefactos o ruido».
    /// Every measurement above is blind to that: the truth is drawn clean and the frame handed over is
    /// a clean shrink of it, so a chain that amplifies grain scores exactly as well as one that does
    /// not. <b>A decoded video is never clean</b> — compression leaves block noise and grain in every
    /// flat area.
    /// </para>
    /// <para>
    /// So the frame handed over here carries a deterministic grain the truth does not, and the distance
    /// is still measured against the <i>clean</i> truth. Amplifying the grain therefore costs distance,
    /// which is what makes it visible to a number. <b>AMD's own spatial sharpener says out loud that
    /// this is outside its design</b> — «Image should be noise free», and integrate it before film
    /// grain — so a chain copied from that design cannot be assumed to survive here.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_grainy_frame_does_not_come_out_further_from_the_truth_than_leaving_it_alone()
    {
        var truth = UpscaleTruth.Draw();
        var composition = Distance(truth, upscale: false, BitmapInterpolationMode.LowQuality, grain: 6);
        var enhanced = Distance(truth, upscale: true, BitmapInterpolationMode.LowQuality, grain: 6);

        Assert.True(
            composition > Distance(truth, upscale: false, BitmapInterpolationMode.LowQuality),
            "The grain cost the composition nothing, so it never reached the picture and this test is "
            + "measuring the clean case twice.");
        Assert.True(
            enhanced < composition,
            $"With grain in the source the enhancement lands {enhanced:F2} from the truth and leaving "
            + $"it alone lands {composition:F2}, so sharpening a real video makes it worse rather than "
            + "better. This is the «artefactos o ruido» the owner reported on 2026-09-13, which every "
            + "other test here is blind to because their sources are clean.");
    }

    /// <summary>
    /// Mean absolute distance per pixel between what the surface drew and the truth, 0 to 255.
    /// </summary>
    private static double Distance(
        byte[] truth,
        bool upscale,
        BitmapInterpolationMode mode,
        bool enlarge = true,
        int grain = 0)
    {
        var frames = enlarge
            ? new TruthFrames(Grain(UpscaleTruth.Shrink(truth), grain), SourceWidth, SourceHeight)
            : new TruthFrames(truth, SurfaceWidth, SurfaceHeight);

        var surface = new VideoFrameView
        {
            FrameSource = frames,
            Width = SurfaceWidth,
            Height = SurfaceHeight,
            IsUpscaleEnabled = upscale,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        RenderOptions.SetBitmapInterpolationMode(surface, mode);

        var window = new Window
        {
            Width = SurfaceWidth + 40,
            Height = SurfaceHeight + 40,
            Padding = new Thickness(0),
            Content = surface,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        frames.Publish();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        try
        {
            using var buffer = frame!.Lock();
            var bytes = new byte[buffer.RowBytes * frame.PixelSize.Height];
            System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            var total = 0L;
            for (var row = 0; row < SurfaceHeight; row++)
            {
                for (var column = 0; column < SurfaceWidth; column++)
                {
                    // The green channel only: the capture's byte order puts red and blue somewhere
                    // this file has no business knowing, and the truth is grey so green carries it.
                    var drawn = bytes[(row * buffer.RowBytes) + (column * 4) + 1];
                    total += Math.Abs(drawn - truth[(row * SurfaceWidth) + column]);
                }
            }

            return (double)total / (SurfaceWidth * SurfaceHeight);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The grain a decoder leaves behind, added to the frame and not to the truth.
    /// </summary>
    /// <remarks>
    /// Deterministic on purpose — a hash of the pixel's own index rather than a random source — because
    /// a yardstick that returns a different number every run cannot carry a floor. The pattern is
    /// signed and alternates, so it neither brightens nor darkens the picture on average.
    /// </remarks>
    private static byte[] Grain(byte[] frame, int amplitude)
    {
        if (amplitude == 0)
        {
            return frame;
        }

        var grainy = new byte[frame.Length];
        for (var index = 0; index < frame.Length; index++)
        {
            // A cheap integer hash: enough to look unstructured, and identical on every machine.
            var scrambled = (index * 2654435761u) >> 13;
            var offset = (int)(scrambled % (uint)((amplitude * 2) + 1)) - amplitude;
            grainy[index] = (byte)Math.Clamp(frame[index] + offset, 0, 255);
        }

        return grainy;
    }

    /// <summary>A grey picture handed over the way the decoder hands one over.</summary>
    private sealed class TruthFrames(byte[] grey, int width, int height) : IVideoFrameSource
    {
        public event EventHandler<VideoFrameEventArgs>? FrameRendered;

        public void Publish()
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var index = 0; index < width * height; index++)
            {
                var at = index * 4;
                pixels[at] = grey[index];
                pixels[at + 1] = grey[index];
                pixels[at + 2] = grey[index];
                pixels[at + 3] = 255;
            }

            FrameRendered?.Invoke(this, new VideoFrameEventArgs(pixels, width, height, stride));
        }
    }
}
