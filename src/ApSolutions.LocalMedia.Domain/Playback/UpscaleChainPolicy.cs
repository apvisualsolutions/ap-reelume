// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// How an enlarged picture is drawn, worst first, so a larger number is a better picture.
/// </summary>
/// <remarks>
/// <para>
/// The order is the declaration order on purpose: the chain is a fall from the best link the machine
/// offers down to the one that always works, and a reader comparing two of these gets the answer the
/// name promises.
/// </para>
/// <para>
/// <b>There is no member meaning «nothing».</b> <see cref="CompositionBilinear"/> is what the
/// application already does, so «switched off» is a real drawing path rather than an absence — which
/// is what keeps «off» distinguishable from «measured nothing». A fifth state for off would be a
/// value no pixel could ever tell apart from this one.
/// </para>
/// </remarks>
public enum UpscaleLink
{
    /// <summary>The renderer's own filter, which is what every frame gets today.</summary>
    CompositionBilinear = 0,

    /// <summary>Cubic resampling on the drawing canvas, with no shader involved.</summary>
    BicubicResample = 1,

    /// <summary>Our own edge-aware pass, which runs on any card at all.</summary>
    PortableUpscaler = 2,

    /// <summary>The graphics driver's standard edge enhancement, which both cards here declare.</summary>
    VideoProcessorEnhancement = 3,

    /// <summary>The card maker's own super resolution, where it is switched on and works.</summary>
    VendorSuperResolution = 4,
}

/// <summary>
/// Which links this machine can actually draw with. Each flag means <b>available and affordable</b>,
/// so whoever sets it has already asked the card and the clock.
/// </summary>
/// <remarks>
/// There is no flag for <see cref="UpscaleLink.CompositionBilinear"/>: a frame is always drawn, so a
/// field that can never be false would only be a place for a bug to hide.
/// </remarks>
/// <param name="VendorSuperResolution">NVIDIA's, Intel's or AMD's own, switched on and moving pixels.</param>
/// <param name="VideoProcessorEnhancement">The driver's standard edge enhancement.</param>
/// <param name="RuntimeShader">A runtime shader compiles and draws on this renderer.</param>
/// <param name="CubicResampler">The drawing canvas offers cubic resampling.</param>
public readonly record struct UpscaleCapabilities(
    bool VendorSuperResolution,
    bool VideoProcessorEnhancement,
    bool RuntimeShader,
    bool CubicResampler);

/// <summary>
/// PLY-016's chain of descent: which link draws this frame, decided without touching a graphics
/// card.
/// </summary>
/// <remarks>
/// <para>
/// The half that talks to the machine fills in <see cref="UpscaleCapabilities"/>; the order they
/// fall in is arithmetic and lives here, which is the same seam <see cref="UpscalePolicy"/> and
/// <see cref="UpscaleCostPolicy"/> already sit on.
/// </para>
/// <para>
/// <b>The two gates in front of the fall are not tidiness.</b> «Switched off, nothing changes» is
/// half of what the owner asked for, and a picture already as large as the box it is drawn in gains
/// nothing from a card — enlarging it anyway would spend a graphics card to throw the result away
/// and light the indicator over every file in the library.
/// </para>
/// </remarks>
public static class UpscaleChainPolicy
{
    /// <summary>
    /// The link that draws a picture whose <paramref name="decision"/> came from
    /// <see cref="UpscalePolicy.Decide"/>, on a machine offering <paramref name="capabilities"/>.
    /// </summary>
    public static UpscaleLink Choose(
        bool enabled,
        UpscaleDecision decision,
        UpscaleCapabilities capabilities)
    {
        if (!enabled || !decision.ShouldUpscale)
        {
            return UpscaleLink.CompositionBilinear;
        }

        if (capabilities.VendorSuperResolution)
        {
            return UpscaleLink.VendorSuperResolution;
        }

        if (capabilities.VideoProcessorEnhancement)
        {
            return UpscaleLink.VideoProcessorEnhancement;
        }

        if (capabilities.RuntimeShader)
        {
            return UpscaleLink.PortableUpscaler;
        }

        return capabilities.CubicResampler
            ? UpscaleLink.BicubicResample
            : UpscaleLink.CompositionBilinear;
    }

    // WHAT IS DELIBERATELY NOT HERE, and it was here for an hour on 2026-09-13: a method that took a
    // measured UpscaleCost and switched the link it belonged to off the table. It was written, tested
    // with six cases, and called from nowhere but its own test file — which is this repository's
    // defining defect, and the gate auditor named it. Removing it is better than keeping a sixth
    // instance of «registered and never fed».
    //
    // Wiring it for real needs a clock in the drawing path, and that belongs with the frame-cost
    // measurement rather than here: a feedback loop that switches the feature off from a wall-clock
    // reading would make the headless suite flaky, and «did it fall back?» would be a behaviour
    // nothing asserted. UpscaleCostPolicy keeps the arithmetic and the trap it names — an unmeasured
    // cost reports that it does not fit, so asking «does it fit?» of one is how every link gets
    // switched off on a machine nobody has timed.
}
