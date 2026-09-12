// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;

using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// Which matrix decodes a picture, and why the height decides it when nobody declared a space.
/// </summary>
/// <remarks>
/// The negative case carries as much weight as the positive one here. Decoding standard definition
/// with the high definition matrix is the same defect as the one being fixed, pointing the other
/// way, and a suite that only checked high definition would sign it off.
/// </remarks>
public sealed class YuvMatrixPolicyTests
{
    [Theory]
    [InlineData(2160)]
    [InlineData(1080)]
    [InlineData(720)]
    public void A_picture_of_high_definition_height_is_decoded_with_the_high_definition_matrix(int frameHeight)
    {
        Assert.Equal(
            YuvColourSpace.Bt709,
            YuvMatrixPolicy.Choose(frameHeight));
    }

    [Theory]
    [InlineData(719)]
    [InlineData(576)]
    [InlineData(480)]
    [InlineData(0)]
    [InlineData(-1080)]
    public void A_picture_below_that_height_keeps_the_standard_definition_matrix(int frameHeight)
    {
        Assert.Equal(
            YuvColourSpace.Bt601,
            YuvMatrixPolicy.Choose(frameHeight));
    }

    [Fact]
    public void The_two_matrices_differ_in_chroma_and_agree_on_luma()
    {
        var sd = YuvMatrixPolicy.MatrixFor(YuvColourSpace.Bt601);
        var hd = YuvMatrixPolicy.MatrixFor(YuvColourSpace.Bt709);

        // Limited range is the same gain either way, so the luma coefficient is shared. If these
        // two ever came back equal the whole fix would be a no-op that still read as done.
        Assert.Equal(sd.Luma, hd.Luma);
        Assert.NotEqual(sd.RedFromV, hd.RedFromV);
        Assert.NotEqual(sd.GreenFromU, hd.GreenFromU);
        Assert.NotEqual(sd.GreenFromV, hd.GreenFromV);
        Assert.NotEqual(sd.BlueFromU, hd.BlueFromU);
    }

    [Theory]
    [InlineData(YuvColourSpace.Bt601, 298, 409, 100, 208, 516)]
    [InlineData(YuvColourSpace.Bt709, 298, 459, 55, 136, 541)]
    public void Each_matrix_carries_the_coefficients_its_recommendation_defines(
        YuvColourSpace space,
        int luma,
        int redFromV,
        int greenFromU,
        int greenFromV,
        int blueFromU)
    {
        // Written down rather than derived, because a test that recomputes the coefficients the same
        // way the code does agrees with any mistake the code makes. These five numbers were measured
        // on 2026-09-12 against ffmpeg decoding the same samples, and each is the nearest integer of
        // its exact value scaled by 256, never more than one level out over every valid sample.
        Assert.Equal(
            new YuvColourMatrix(luma, redFromV, greenFromU, greenFromV, blueFromU),
            YuvMatrixPolicy.MatrixFor(space));
    }

    [Theory]
    [InlineData(1080, YuvColourSpace.Bt709)]
    [InlineData(480, YuvColourSpace.Bt601)]
    public void The_one_call_a_decoder_makes_is_the_choice_and_the_matrix_together(
        int frameHeight,
        YuvColourSpace expected) =>
        Assert.Equal(YuvMatrixPolicy.MatrixFor(expected), YuvMatrixPolicy.For(frameHeight));

    [Fact]
    public void A_space_outside_the_two_that_exist_is_refused_rather_than_given_a_default()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => YuvMatrixPolicy.MatrixFor((YuvColourSpace)99));

        Assert.Equal("space", refused.ParamName);
    }
}
