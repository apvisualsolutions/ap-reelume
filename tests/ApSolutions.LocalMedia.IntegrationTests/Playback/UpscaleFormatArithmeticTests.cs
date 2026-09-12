// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// The half of PLY-016's scaler that decides, split from the half that talks to a graphics card so
/// it can be measured on any machine — the same seam <c>WindowsAudioEndpointConfigurator</c> was cut
/// along on 2026-09-02.
/// </summary>
public sealed class UpscaleFormatArithmeticTests
{
    /// <summary>
    /// Read off this machine's PCI bus on 2026-09-12: the RTX 5070 answers <c>VEN_10DE</c> and the
    /// UHD 770 <c>VEN_8086</c>. AMD's belongs to a card nobody here has, so it is named as the PCI
    /// registry and VLC's own source name it and not as something measured.
    /// </summary>
    [Theory]
    [InlineData(0x10DEu, GpuVendor.Nvidia)]
    [InlineData(0x8086u, GpuVendor.Intel)]
    [InlineData(0x1002u, GpuVendor.Amd)]
    // AMD answers under two identifiers, and the second one belongs to the integrated graphics of
    // its own processors — exactly the machine this feature exists for.
    [InlineData(0x1022u, GpuVendor.Amd)]
    // Measured on this machine: the Microsoft Basic Render Driver, which has no video processor.
    [InlineData(0x1414u, GpuVendor.Unknown)]
    public void A_card_is_named_by_the_identifier_its_bus_reports(uint pciVendorId, GpuVendor expected) =>
        Assert.Equal(expected, D3d11UpscaleFormats.VendorOf(pciVendorId));

    /// <summary>
    /// The flags, not the <c>HRESULT</c>. <c>CheckVideoProcessorFormat</c> returns <c>S_OK</c> for a
    /// format it does not support and says so in the flags it writes out instead, so a probe that
    /// reads the return value reports every format as available.
    /// </summary>
    [Theory]
    [InlineData(0u, false, false)]
    [InlineData(1u, true, false)]
    [InlineData(2u, false, true)]
    [InlineData(3u, true, true)]
    public void Support_is_read_from_the_flags_and_never_from_the_result_code(
        uint flags,
        bool asInput,
        bool asOutput)
    {
        Assert.Equal(asInput, D3d11UpscaleFormats.AcceptsAsInput(flags));
        Assert.Equal(asOutput, D3d11UpscaleFormats.AcceptsAsOutput(flags));
    }

    /// <summary>The battery is the question being asked; a missing row is a question never asked.</summary>
    [Fact]
    public void The_battery_asks_about_every_format_this_pipeline_could_ever_hand_over()
    {
        var names = D3d11UpscaleFormats.Battery.Select(format => format.Name).ToArray();

        Assert.Equal(
            ["NV12", "P010", "YUY2", "AYUV", "B8G8R8A8_UNORM", "R8G8B8A8_UNORM", "R10G10B10A2_UNORM", "R16G16B16A16_FLOAT"],
            names);
        // Read from dxgiformat.h of the 10.0.26100.0 Windows SDK on 2026-09-12.
        Assert.Equal([103u, 104u, 107u, 100u, 87u, 28u, 24u, 10u], D3d11UpscaleFormats.Battery.Select(f => f.DxgiFormat));
    }

    /// <summary>
    /// Which format the picture should be handed over as, given what the processor answered. The
    /// order is this pipeline's cost and nothing else: LibVLC already hands out UYVY, and YUY2 is
    /// the same bytes with each pair swapped, so it costs no colour conversion at all. NV12 costs a
    /// planar conversion; BGRA costs the one being paid today and twice the bytes on the bus.
    /// </summary>
    [Theory]
    [InlineData("NV12,YUY2,B8G8R8A8_UNORM", "YUY2")]
    [InlineData("NV12,B8G8R8A8_UNORM", "NV12")]
    [InlineData("B8G8R8A8_UNORM", "B8G8R8A8_UNORM")]
    [InlineData("R8G8B8A8_UNORM", "R8G8B8A8_UNORM")]
    public void The_cheapest_format_this_pipeline_can_produce_is_the_one_chosen(
        string accepted,
        string expected)
    {
        var support = Accepting(accepted.Split(','));

        Assert.Equal(expected, D3d11UpscaleFormats.PreferredInput(support));
    }

    /// <summary>
    /// The control that has to stay silent. A processor that accepts only formats this pipeline
    /// cannot produce leaves nothing to choose, and answering anyway would send a picture in a
    /// format the scaler rejects at the last moment.
    /// </summary>
    [Theory]
    [InlineData("P010,AYUV,R16G16B16A16_FLOAT")]
    [InlineData("")]
    public void A_processor_that_accepts_nothing_this_pipeline_makes_is_answered_with_nothing(string accepted)
    {
        var support = Accepting(accepted.Split(',', StringSplitOptions.RemoveEmptyEntries));

        Assert.Null(D3d11UpscaleFormats.PreferredInput(support));
    }

    /// <summary>
    /// A format accepted only as output is not an input, and the pair is the point: the flags carry
    /// both, and a chooser that ignores the difference picks a format the processor will refuse.
    /// </summary>
    [Fact]
    public void A_format_accepted_only_as_output_is_never_chosen_as_the_input()
    {
        var support = new Dictionary<string, uint>(StringComparer.Ordinal)
        {
            ["YUY2"] = 2u,
            ["NV12"] = 1u,
        };

        Assert.Equal("NV12", D3d11UpscaleFormats.PreferredInput(support));
    }

    /// <summary>
    /// NVIDIA's payload is three unsigned numbers, and what they mean is the whole of the feature:
    /// version 1, method 2 — RTX VSR — and the switch. Taken from VLC's
    /// <c>modules/video_output/win32/d3d11_scaler.cpp</c>, LGPL-2.1-or-later.
    /// </summary>
    [Theory]
    [InlineData(true, 1u)]
    [InlineData(false, 0u)]
    public void The_NVIDIA_payload_carries_its_version_its_method_and_the_switch(bool enable, uint expected)
    {
        var payload = D3d11UpscaleFormats.NvidiaStreamExtension(enable);

        Assert.Equal(12, payload.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(payload, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(payload, 4));
        Assert.Equal(expected, BitConverter.ToUInt32(payload, 8));
    }

    /// <summary>
    /// Intel's is three separate calls rather than one payload, and switching it off is not the
    /// same call with a zero: the scaling mode goes back to its default and the mode leaves
    /// pre-processing. A probe that only ever sent the «on» calls could never measure the «off».
    /// </summary>
    [Fact]
    public void The_Intel_extensions_turn_off_by_going_back_and_not_by_sending_a_zero()
    {
        var on = D3d11UpscaleFormats.IntelSuperResolutionCalls(enable: true);
        var off = D3d11UpscaleFormats.IntelSuperResolutionCalls(enable: false);

        Assert.Equal([0x0003u, 0x1u, 0x2u], on.Select(call => call.Value));
        Assert.Equal([0x0003u, 0x0u, 0x0u], off.Select(call => call.Value));
        Assert.Equal([0x01u, 0x20u, 0x37u], on.Select(call => call.Function));
    }

    /// <summary>
    /// The half that costs a wrong verdict rather than an error. The version and the mode are
    /// <b>output</b> extensions and only the scaling is a stream one — Chromium's
    /// <c>ToggleIntelVpSuperResolution</c>. Sending the first down the stream door was refused with
    /// <c>E_FAIL</c> on this machine on 2026-09-12, which reads exactly like a card that has no such
    /// interface, and was written up as one until the documentation said otherwise.
    /// </summary>
    [Fact]
    public void Only_the_Intel_scaling_call_is_a_stream_extension()
    {
        var calls = D3d11UpscaleFormats.IntelSuperResolutionCalls(enable: true);

        Assert.Equal([true, true, false], calls.Select(call => call.OnOutput));
    }

    /// <summary>
    /// A processor that never mentioned a format at all is not the same as one that mentioned it and
    /// said no, and the chooser has to walk past both. A card whose answer arrived half-written
    /// produces exactly this, and reading a missing key as «accepted» would send it a picture it
    /// refuses at the last moment.
    /// </summary>
    [Fact]
    public void A_format_the_processor_never_mentioned_is_walked_past_like_one_it_refused()
    {
        var support = new Dictionary<string, uint>(StringComparer.Ordinal) { ["B8G8R8A8_UNORM"] = 3u };

        Assert.Equal("B8G8R8A8_UNORM", D3d11UpscaleFormats.PreferredInput(support));
    }

    /// <summary>
    /// The picture sent through the scaler has to carry detail, or nothing a super resolution does
    /// would show. Its size is the format's, and it is the same every run: a measurement that used
    /// random content could not be compared against the run before it.
    /// </summary>
    [Theory]
    [InlineData("YUY2", 2)]
    [InlineData("B8G8R8A8_UNORM", 4)]
    [InlineData("R8G8B8A8_UNORM", 4)]
    public void The_test_picture_is_the_size_its_format_says_and_never_changes(string format, int bytesPerPixel)
    {
        var first = D3d11UpscaleFormats.TestPattern(format, 64, 32, out var pitch);
        var second = D3d11UpscaleFormats.TestPattern(format, 64, 32, out _);

        Assert.Equal(64 * bytesPerPixel, pitch);
        Assert.Equal(64 * 32 * bytesPerPixel, first.Length);
        Assert.Equal(first, second);
    }

    /// <summary>NV12 is the odd one: a full luma plane and a half-height colour plane behind it.</summary>
    [Fact]
    public void The_NV12_picture_carries_a_half_height_colour_plane_behind_its_luma()
    {
        var pattern = D3d11UpscaleFormats.TestPattern("NV12", 64, 32, out var pitch);

        Assert.Equal(64, pitch);
        Assert.Equal(64 * 32 * 3 / 2, pattern.Length);
        // Colourless on purpose: what a super resolution rebuilds is detail, and detail is luma.
        Assert.All(pattern[(64 * 32)..], byte_ => Assert.Equal(128, byte_));
    }

    /// <summary>
    /// A flat picture would let a scaler that did nothing look identical to one that did everything.
    /// </summary>
    [Theory]
    [InlineData("YUY2")]
    [InlineData("NV12")]
    [InlineData("B8G8R8A8_UNORM")]
    public void The_test_picture_carries_hard_edges_rather_than_one_flat_colour(string format)
    {
        var pattern = D3d11UpscaleFormats.TestPattern(format, 64, 32, out _);

        Assert.True(pattern.Distinct().Count() > 2, $"{format} produced {pattern.Distinct().Count()} distinct values.");
    }

    /// <summary>
    /// <b>The edges have to be close together, and counting distinct values does not check that.</b>
    /// A super resolution rebuilds fine detail; widening the bands from three pixels to thirty-two
    /// halved Intel's measured difference and left every test green — 2026-09-12. So the run length
    /// itself is asserted: no straight line of the luma plane may hold more than three equal
    /// neighbours in a row.
    /// </summary>
    [Fact]
    public void The_edges_of_the_test_picture_are_never_more_than_three_pixels_apart()
    {
        var pattern = D3d11UpscaleFormats.TestPattern("NV12", 64, 32, out var pitch);

        var longest = 0;
        for (var y = 0; y < 32; y++)
        {
            var run = 1;
            for (var x = 1; x < 64; x++)
            {
                run = pattern[(y * pitch) + x] == pattern[(y * pitch) + x - 1] ? run + 1 : 1;
                longest = Math.Max(longest, run);
            }
        }

        Assert.True(longest <= 3, $"The luma plane holds a run of {longest} equal pixels.");
    }

    /// <summary>Which standard filters a bitmask offers, which is what the report lists by name.</summary>
    [Theory]
    [InlineData(0x00u, "")]
    [InlineData(0x10u, "NOISE_REDUCTION")]
    [InlineData(0x20u, "EDGE_ENHANCEMENT")]
    [InlineData(0x30u, "NOISE_REDUCTION,EDGE_ENHANCEMENT")]
    // Every colour filter set and neither of the two that matter: the mask is read bit by bit and
    // not as «this card has filters».
    [InlineData(0xCFu, "")]
    public void The_filters_a_processor_offers_are_read_bit_by_bit(uint filterCaps, string expected) =>
        Assert.Equal(
            expected.Split(',', StringSplitOptions.RemoveEmptyEntries),
            D3d11UpscaleFormats.OfferedFilters(filterCaps));

    /// <summary>A format this pipeline never sends is refused rather than silently drawn as noise.</summary>
    [Fact]
    public void A_format_the_pipeline_never_sends_is_refused() =>
        Assert.Throws<NotSupportedException>(() => D3d11UpscaleFormats.TestPattern("P010", 64, 32, out _));

    /// <summary>
    /// The comparison behind the whole measurement. Two readbacks of different lengths are as
    /// different as two pictures can be, and answering zero there would read as «nothing changed».
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, 0)]
    [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 1, 9, 3 }, 1)]
    [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 9, 9, 9 }, 3)]
    [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }, 3)]
    [InlineData(new byte[0], new byte[0], 0)]
    public void Differences_are_counted_byte_by_byte_and_a_short_readback_is_all_difference(
        byte[] first,
        byte[] second,
        long expected) =>
        Assert.Equal(expected, D3d11UpscaleFormats.CountDifferences(first, second));

    private static Dictionary<string, uint> Accepting(IEnumerable<string> names)
    {
        var support = D3d11UpscaleFormats.Battery.ToDictionary(
            format => format.Name,
            _ => 0u,
            StringComparer.Ordinal);
        foreach (var name in names)
        {
            support[name] = 3u;
        }

        return support;
    }
}
