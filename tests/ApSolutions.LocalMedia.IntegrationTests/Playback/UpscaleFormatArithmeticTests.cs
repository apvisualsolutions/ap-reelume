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

    /// <summary>
    /// What a card's answer means. This lived as prose in an evidence document, which is how
    /// «inconclusive» quietly becomes «it does not work» a month later.
    /// </summary>
    [Theory]
    // Accepted and the picture changed: the only case that is a success.
    [InlineData(18109378L, 0L, 44, 0, true, 0, UpscaleVerdict.Works)]
    // Accepted and nothing moved. NVIDIA's driver does exactly this while the feature is off in its
    // own application, so it is never reported as a failure.
    [InlineData(0L, 0L, 6, 0, true, 0, UpscaleVerdict.Inconclusive)]
    // Refused outright, which IS an answer about the card.
    [InlineData(0L, 0L, 44, 0, true, -2147467259, UpscaleVerdict.Refused)]
    // The negative control failed: the readback disagrees with itself.
    [InlineData(18109378L, 5L, 44, 0, true, 0, UpscaleVerdict.Untrustworthy)]
    // The positive control failed: the processor drew nothing, so any zero is meaningless.
    [InlineData(0L, 0L, 1, 0, true, 0, UpscaleVerdict.Untrustworthy)]
    // The exact boundary, and it is the failure that actually happens: the output is BGRA with
    // opaque alpha, so a blt that drew nothing comes back as two values — 0 and 255 — and nothing
    // else. With no case here, loosening the guard to «< 2» left fifty tests green.
    [InlineData(0L, 0L, 2, 0, true, 0, UpscaleVerdict.Untrustworthy)]
    // And the one either side of it, so the boundary is pinned from both directions.
    [InlineData(18109378L, 0L, 3, 0, true, 0, UpscaleVerdict.Works)]
    // A draw that failed, and three readbacks of different lengths.
    [InlineData(0L, 0L, 44, -1, true, 0, UpscaleVerdict.Untrustworthy)]
    [InlineData(33177600L, 0L, 44, 0, false, 0, UpscaleVerdict.Untrustworthy)]
    public void What_a_card_answered_is_read_the_same_way_every_time(
        long differing,
        long control,
        int distinct,
        int worstBlt,
        bool agree,
        int onResult,
        UpscaleVerdict expected)
    {
        var pixels = new UpscalePixelComparison(differing, control, 33177600L, distinct, worstBlt, agree);

        Assert.Equal(
            expected,
            D3d11UpscaleFormats.Verdict(pixels, new VendorExtensionOutcome(onResult, 0, 0, true, false)));
    }

    /// <summary>
    /// A headline computed from two draws that were configured the same way is not a measurement,
    /// whatever number it carries. Three separate mutations produced exactly that and every test
    /// stayed green while Intel's 18 109 378 fell to zero.
    /// </summary>
    [Theory]
    // Both «on», both «off», and the shape a deleted call leaves behind.
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void A_headline_from_two_identical_configurations_is_never_believed(bool on, bool off)
    {
        var pixels = new UpscalePixelComparison(18109378L, 0L, 33177600L, 44, 0, true);

        Assert.Equal(
            UpscaleVerdict.Untrustworthy,
            D3d11UpscaleFormats.Verdict(pixels, new VendorExtensionOutcome(0, 0, 0, on, off)));
    }

    /// <summary>
    /// The controls are asked before the extension is, and the order is what tells a card with a
    /// broken readback apart from one that simply has nothing to ask.
    /// </summary>
    [Fact]
    public void A_broken_readback_is_untrustworthy_even_when_there_is_no_extension_to_ask_about()
    {
        var broken = new UpscalePixelComparison(0L, 7L, 33177600L, 44, 0, true);

        Assert.Equal(UpscaleVerdict.Untrustworthy, D3d11UpscaleFormats.Verdict(broken, null));
    }

    /// <summary>The two cases with nothing to read, told apart from each other and from a failure.</summary>
    [Fact]
    public void A_card_that_sent_no_picture_and_one_with_no_extension_are_different_answers()
    {
        var sound = new UpscalePixelComparison(0L, 0L, 33177600L, 44, 0, true);

        Assert.Equal(UpscaleVerdict.NotRun, D3d11UpscaleFormats.Verdict(null, null));
        Assert.Equal(UpscaleVerdict.NotAsked, D3d11UpscaleFormats.Verdict(sound, null));
    }

    /// <summary>
    /// The line a person reads a month later. It carries the <b>verdict</b>, which is the whole
    /// reason the classification exists: the report used to carry nineteen columns of numbers and
    /// not one of them said «inconclusive», so the RTX 5070's line read `differing=0` and nothing
    /// else.
    /// </summary>
    [Fact]
    public void The_line_a_person_reads_says_what_happened_before_it_says_a_number()
    {
        var nvidia = UpscaleProbeReport.Describe(Probe(
            new VendorExtensionOutcome(0, 0, 0, true, false),
            new UpscalePixelComparison(0L, 0L, 33177600L, 6, 0, true)));
        var intel = UpscaleProbeReport.Describe(Probe(
            new VendorExtensionOutcome(0, 0, 0, true, false),
            new UpscalePixelComparison(18109378L, 0L, 33177600L, 44, 0, true)));

        Assert.Contains("Inconclusive", nvidia, StringComparison.Ordinal);
        Assert.Contains("differing=0", nvidia, StringComparison.Ordinal);
        Assert.Contains("Works", intel, StringComparison.Ordinal);
        Assert.Contains("differing=18109378", intel, StringComparison.Ordinal);
        // The filters are the floor under the feature, so they are in the line whatever the vendor
        // extension did.
        Assert.Contains("EDGE_ENHANCEMENT", nvidia, StringComparison.Ordinal);
        Assert.Contains("edge=8084664", nvidia, StringComparison.Ordinal);
    }

    /// <summary>An adapter that never got that far says so, rather than reading as a refusal.</summary>
    [Fact]
    public void An_adapter_with_no_video_processor_says_so_in_its_own_line()
    {
        var line = UpscaleProbeReport.Describe(new AdapterUpscaleProbe(
            "Microsoft Basic Render Driver",
            0x1414u,
            GpuVendor.Unknown,
            new Dictionary<string, uint>(StringComparer.Ordinal),
            null,
            null,
            null,
            null,
            "this driver exposes no ID3D11VideoDevice."));

        Assert.Contains("NotRun", line, StringComparison.Ordinal);
        Assert.Contains("no ID3D11VideoDevice", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Refused", line, StringComparison.Ordinal);
    }

    private static AdapterUpscaleProbe Probe(
        VendorExtensionOutcome extension,
        UpscalePixelComparison pixels) =>
        new(
            "NVIDIA GeForce RTX 5070",
            0x10DEu,
            GpuVendor.Nvidia,
            new Dictionary<string, uint>(StringComparer.Ordinal) { ["YUY2"] = 3u },
            "YUY2",
            extension,
            new StandardFilterOutcome(0x30u, ["NOISE_REDUCTION", "EDGE_ENHANCEMENT"], 8084664L),
            pixels,
            null);

    /// <summary>
    /// The second picture exists because the first one assumes something nobody measured: that a
    /// vendor's super resolution has anything to do with hard synthetic bands. These are neural
    /// models trained on compressed video, and the probe's own numbers hint at it — the same bands
    /// come back off an RTX 5070 with six distinct byte values and off an Intel UHD with forty-four.
    /// So the detail picture carries what a frame actually carries: gradients, fine texture, and
    /// colour that is not flat.
    /// </summary>
    [Theory]
    [InlineData("YUY2")]
    [InlineData("NV12")]
    [InlineData("B8G8R8A8_UNORM")]
    public void The_detail_picture_carries_colour_where_the_band_picture_carries_none(string format)
    {
        var bands = D3d11UpscaleFormats.TestPattern(
            format, 64, 32, UpscaleProbeContent.Bands, out _);
        var detail = D3d11UpscaleFormats.TestPattern(
            format, 64, 32, UpscaleProbeContent.Detail, out _);

        Assert.Equal(bands.Length, detail.Length);
        Assert.True(
            detail.Distinct().Count() > bands.Distinct().Count(),
            $"{format}: detail has {detail.Distinct().Count()} values, bands {bands.Distinct().Count()}");
    }

    [Fact]
    public void The_detail_picture_paints_the_NV12_colour_plane_instead_of_leaving_it_grey()
    {
        var detail = D3d11UpscaleFormats.TestPattern(
            "NV12", 64, 32, UpscaleProbeContent.Detail, out _);

        // The band picture fills this plane with 128 and says so; this one must not.
        var chroma = detail[(64 * 32)..];
        Assert.True(chroma.Distinct().Count() > 1, "the colour plane came back flat");
    }

    [Theory]
    [InlineData("YUY2")]
    [InlineData("NV12")]
    public void Both_pictures_are_the_same_every_run(string format)
    {
        foreach (var content in new[] { UpscaleProbeContent.Bands, UpscaleProbeContent.Detail })
        {
            var first = D3d11UpscaleFormats.TestPattern(format, 64, 32, content, out var pitch);
            var second = D3d11UpscaleFormats.TestPattern(format, 64, 32, content, out var again);

            Assert.Equal(pitch, again);
            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void A_content_kind_that_does_not_exist_is_refused_rather_than_drawn_as_something_else()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => D3d11UpscaleFormats.TestPattern("NV12", 64, 32, (UpscaleProbeContent)9, out _));
    }

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
