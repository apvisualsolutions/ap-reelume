// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// Which link of PLY-016's chain draws a frame, from the vendor's own super resolution down to the
/// composition doing what it does today.
/// </summary>
/// <remarks>
/// <para>
/// The chain is a strict fall, and its bottom rung is <b>what the application already does</b>
/// rather than a fifth state meaning «nothing». That is deliberate: a link meaning «off» that is
/// not also a real drawing path would make «switched off» and «measured nothing» read the same, and
/// this repository has removed two guards for exactly that reason.
/// </para>
/// <para>
/// Every row of the capability table is written out by hand. Computing the expected link from the
/// same rule the implementation uses would make a wrong rule agree with itself, which is how a
/// sixteen-case test certifies a bug.
/// </para>
/// <para>
/// <b>Six tests used to live here and were deleted with the method they exercised.</b> It took a
/// measured cost and switched the link it belonged to off the table, and nothing outside this file
/// ever called it — which is the defect this repository is named for. The note in
/// <c>UpscaleChainPolicy</c> says where that decision goes when there is a clock to feed it.
/// </para>
/// </remarks>
public sealed class UpscaleChainPolicyTests
{
    /// <summary>A 720p picture drawn into a 4K box, which is the case the feature exists for.</summary>
    private static readonly UpscaleDecision Enlarging = UpscalePolicy.Decide(1280, 720, 3840, 2160);

    /// <summary>
    /// The whole capability table, sixteen rows, each naming the link that has to win.
    /// </summary>
    [Theory]
    // The vendor's own super resolution wins wherever it is available, whatever else is.
    [InlineData(true, true, true, true, UpscaleLink.VendorSuperResolution)]
    [InlineData(true, true, true, false, UpscaleLink.VendorSuperResolution)]
    [InlineData(true, true, false, true, UpscaleLink.VendorSuperResolution)]
    [InlineData(true, true, false, false, UpscaleLink.VendorSuperResolution)]
    [InlineData(true, false, true, true, UpscaleLink.VendorSuperResolution)]
    [InlineData(true, false, true, false, UpscaleLink.VendorSuperResolution)]
    [InlineData(true, false, false, true, UpscaleLink.VendorSuperResolution)]
    [InlineData(true, false, false, false, UpscaleLink.VendorSuperResolution)]
    // Without it, the video processor's standard enhancement, which both cards here declare.
    [InlineData(false, true, true, true, UpscaleLink.VideoProcessorEnhancement)]
    [InlineData(false, true, true, false, UpscaleLink.VideoProcessorEnhancement)]
    [InlineData(false, true, false, true, UpscaleLink.VideoProcessorEnhancement)]
    [InlineData(false, true, false, false, UpscaleLink.VideoProcessorEnhancement)]
    // Without either, our own shader, which is the one that runs on every card.
    [InlineData(false, false, true, true, UpscaleLink.PortableUpscaler)]
    [InlineData(false, false, true, false, UpscaleLink.PortableUpscaler)]
    // Without a shader, cubic resampling, which is still better than what the composition gives.
    [InlineData(false, false, false, true, UpscaleLink.BicubicResample)]
    // And with nothing at all, exactly today's drawing.
    [InlineData(false, false, false, false, UpscaleLink.CompositionBilinear)]
    public void The_chain_falls_from_the_vendor_to_the_composition_and_never_skips_a_link(
        bool vendor,
        bool videoProcessor,
        bool shader,
        bool cubic,
        UpscaleLink expected)
    {
        var capabilities = new UpscaleCapabilities(vendor, videoProcessor, shader, cubic);

        Assert.Equal(expected, UpscaleChainPolicy.Choose(true, Enlarging, capabilities));
    }

    /// <summary>
    /// «Switched off, nothing changes», which is the half of the acceptance criterion that a person
    /// notices immediately when it is wrong.
    /// </summary>
    [Fact]
    public void Turned_off_lands_on_the_composition_even_where_every_link_is_available()
    {
        var everything = new UpscaleCapabilities(true, true, true, true);

        Assert.Equal(
            UpscaleLink.CompositionBilinear,
            UpscaleChainPolicy.Choose(false, Enlarging, everything));
    }

    /// <summary>
    /// A picture that is not being enlarged spends no card, whatever the card offers.
    /// </summary>
    /// <remarks>
    /// The product's own negative control rather than the harness's: a 4K file on a 4K screen, and a
    /// 720p file in a 1280-wide window, both have nothing to gain. A chain that enlarged them anyway
    /// would light the indicator over every file in the library.
    /// </remarks>
    [Theory]
    [InlineData(3840, 2160, 3840, 2160)]
    [InlineData(1280, 720, 1280, 720)]
    [InlineData(3840, 2160, 1920, 1080)]
    public void A_picture_that_is_not_being_enlarged_lands_on_the_composition(
        int sourceWidth,
        int sourceHeight,
        int surfaceWidth,
        int surfaceHeight)
    {
        var decision = UpscalePolicy.Decide(sourceWidth, sourceHeight, surfaceWidth, surfaceHeight);
        var everything = new UpscaleCapabilities(true, true, true, true);

        Assert.False(decision.ShouldUpscale, "the case being asserted is not the case set up.");
        Assert.Equal(
            UpscaleLink.CompositionBilinear,
            UpscaleChainPolicy.Choose(true, decision, everything));
    }
}
