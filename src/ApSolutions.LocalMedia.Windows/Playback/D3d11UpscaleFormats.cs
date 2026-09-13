// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// What the probe draws before it asks a card to enlarge it.
/// </summary>
public enum UpscaleProbeContent
{
    /// <summary>Hard synthetic edges with flat colour: the original picture, kept as the control.</summary>
    Bands = 0,

    /// <summary>Gradients, fine texture and moving colour: the shape of a decoded frame.</summary>
    Detail = 1,
}

/// <summary>The card behind the video processor, which decides which super resolution to ask for.</summary>
public enum GpuVendor
{
    Unknown,
    Nvidia,
    Intel,
    Amd,
}

/// <summary>A picture format the video processor is asked about, by the name used in its report.</summary>
public sealed record VideoProcessorFormat(string Name, uint DxgiFormat);

/// <summary>
/// Everything PLY-016's scaler decides without touching a graphics card: which card it is talking
/// to, what the processor's answer actually said, which format this pipeline should hand over, and
/// the exact bytes each vendor's extension expects.
/// </summary>
/// <remarks>
/// <para>
/// Split from <see cref="WindowsVideoUpscaleProbe"/> on purpose. The COM half cannot run where there
/// is no video device — a hosted runner has none — and a file that mixes the two reads at whatever
/// coverage the runner's hardware allows. This half runs everywhere, so it is the half that is
/// asserted.
/// </para>
/// <para>
/// The vendor identifiers, the extension GUIDs and the payloads below are VLC's, from
/// <c>modules/video_output/win32/d3d11_scaler.cpp</c> (LGPL-2.1-or-later). The DXGI values were read
/// from <c>dxgiformat.h</c> of the 10.0.26100.0 Windows SDK on 2026-09-12.
/// </para>
/// </remarks>
public static class D3d11UpscaleFormats
{
    /// <summary>NVIDIA's video processor extension, which carries RTX Video Super Resolution.</summary>
    public static Guid NvidiaExtension { get; } = new("d43ce1b3-1f4b-48ac-baee-c3c25375e6f7");

    /// <summary>Intel's Video Processing Engine extension.</summary>
    public static Guid IntelExtension { get; } = new("edd1d4b9-8659-4cbc-a4d6-9831a2163ac3");

    /// <summary>
    /// Every format worth asking about, in the order the report lists them. Asking about a format
    /// this pipeline could never produce still earns its row: it is what tells a later reader that
    /// the question was asked and answered, rather than never asked.
    /// </summary>
    public static IReadOnlyList<VideoProcessorFormat> Battery { get; } =
    [
        new("NV12", 103u),
        new("P010", 104u),
        new("YUY2", 107u),
        new("AYUV", 100u),
        new("B8G8R8A8_UNORM", 87u),
        new("R8G8B8A8_UNORM", 28u),
        new("R10G10B10A2_UNORM", 24u),
        new("R16G16B16A16_FLOAT", 10u),
    ];

    /// <summary>
    /// The formats this pipeline can hand over, cheapest first. LibVLC already produces UYVY, and
    /// YUY2 is those same bytes with each pair swapped — no colour conversion at all. NV12 costs a
    /// planar conversion but is the processor's own currency. BGRA costs the conversion being paid
    /// today and twice the bytes across the bus, so it comes last.
    /// </summary>
    private static readonly string[] PreferenceOrder =
        ["YUY2", "NV12", "B8G8R8A8_UNORM", "R8G8B8A8_UNORM"];

    /// <summary>The card that reports <paramref name="pciVendorId"/> on the bus.</summary>
    public static GpuVendor VendorOf(uint pciVendorId) => pciVendorId switch
    {
        0x10DEu => GpuVendor.Nvidia,
        0x8086u => GpuVendor.Intel,
        0x1002u or 0x1022u => GpuVendor.Amd,
        _ => GpuVendor.Unknown,
    };

    /// <summary>
    /// Whether the processor takes this format in. Read from the flags and never from the result
    /// code: <c>CheckVideoProcessorFormat</c> answers <c>S_OK</c> for a format it refuses and says
    /// so in the flags, so a probe that trusts the return value reports everything as available.
    /// </summary>
    public static bool AcceptsAsInput(uint formatSupportFlags) => (formatSupportFlags & 0x1u) != 0;

    /// <summary>Whether the processor can write this format out.</summary>
    public static bool AcceptsAsOutput(uint formatSupportFlags) => (formatSupportFlags & 0x2u) != 0;

    /// <summary>
    /// The format to hand the processor, given what it answered, or <see langword="null"/> when it
    /// takes nothing this pipeline can make. A format it accepts only as output is not an answer.
    /// </summary>
    public static string? PreferredInput(IReadOnlyDictionary<string, uint> formatSupport)
    {
        ArgumentNullException.ThrowIfNull(formatSupport);

        return PreferenceOrder.FirstOrDefault(
            name => formatSupport.TryGetValue(name, out var flags) && AcceptsAsInput(flags));
    }

    /// <summary>
    /// NVIDIA's stream extension payload: version 1, method 2 — RTX Video Super Resolution — and
    /// the switch. Three unsigned numbers, in that order.
    /// </summary>
    public static byte[] NvidiaStreamExtension(bool enable)
    {
        var payload = new byte[12];
        BitConverter.TryWriteBytes(payload.AsSpan(0), 1u);
        BitConverter.TryWriteBytes(payload.AsSpan(4), 2u);
        BitConverter.TryWriteBytes(payload.AsSpan(8), enable ? 1u : 0u);
        return payload;
    }

    /// <summary>
    /// Intel's, which is three calls rather than one payload: the interface version, the mode, and
    /// the scaling. Switching off is not the same call with a zero — the mode leaves pre-processing
    /// and the scaling goes back to its default — and the pair is what makes an off measurable.
    /// </summary>
    /// <remarks>
    /// <b><see cref="ExtensionCall.OnOutput"/> is the half that is easy to get wrong, and getting it wrong
    /// looks exactly like a card that cannot do this.</b> The first two go through
    /// <c>VideoProcessorSetOutputExtension</c> and only the third through
    /// <c>VideoProcessorSetStreamExtension</c> — Chromium's <c>ToggleIntelVpSuperResolution</c> in
    /// <c>ui/gl/swap_chain_presenter.cc</c>. Measured here on 2026-09-12: sending the version call
    /// down the stream entry point is refused with <c>E_FAIL</c> on hardware that supports the
    /// interface perfectly well.
    /// </remarks>
    public static IReadOnlyList<ExtensionCall> IntelSuperResolutionCalls(bool enable) =>
    [
        new(0x01u, 0x0003u, OnOutput: true),
        new(0x20u, enable ? 0x1u : 0x0u, OnOutput: true),
        new(0x37u, enable ? 0x2u : 0x0u, OnOutput: false),
    ];

    /// <summary>One call into a vendor extension: which function, what value, and down which door.</summary>
    public sealed record ExtensionCall(uint Function, uint Value, bool OnOutput);

    /// <summary>A filter every Direct3D 11 video processor may offer, by its own name and number.</summary>
    public sealed record VideoProcessorFilter(string Name, uint Index, uint CapBit);

    /// <summary>
    /// The two standard filters that do anything for a picture being enlarged, and the reason they
    /// matter more than any vendor's name: <b>they belong to Direct3D, not to a graphics card
    /// company, so nobody has to switch anything on for them to run.</b> Every other filter in the
    /// enumeration adjusts colour — brightness, contrast, hue, saturation — which is not this
    /// feature's business.
    /// </summary>
    public static IReadOnlyList<VideoProcessorFilter> StandardFilters { get; } =
    [
        new("NOISE_REDUCTION", 4u, 0x10u),
        new("EDGE_ENHANCEMENT", 5u, 0x20u),
    ];

    /// <summary>
    /// Which of <see cref="StandardFilters"/> a processor says it offers, read from the
    /// <c>FilterCaps</c> bitmask of its capabilities.
    /// </summary>
    public static IReadOnlyList<string> OfferedFilters(uint filterCaps) =>
        [.. StandardFilters.Where(filter => (filterCaps & filter.CapBit) != 0).Select(filter => filter.Name)];

    /// <summary>
    /// What a processor says it accepts for one filter, as <c>GetVideoProcessorFilterRange</c>
    /// reports it. <see cref="Multiplier"/> is not decoration: the documentation's formula is
    /// <i>actual value = set value × multiplier</i>, so the numbers here are steps and not degrees.
    /// </summary>
    public sealed record FilterRange(int Minimum, int Maximum, int Default, float Multiplier);

    /// <summary>
    /// The edge-enhancement level to ask this processor for, or <see langword="null"/> when there is
    /// nothing worth asking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This decision used to live inside <see cref="WindowsVideoUpscaleProbe"/></b>, which is
    /// excluded from coverage whole — so the one thing rule 10 says must never be excluded was. It
    /// moved out on 2026-09-12 without changing what it answers for the cards measured that day.
    /// </para>
    /// <para>
    /// <b>The maximum, and the reason is that it is the only level with a measurement behind it</b>:
    /// 24.4 % of the picture moved on both of this machine's cards at that level. Any other number
    /// would be a guess wearing a policy's clothes. Whether the maximum is also the level a person
    /// wants to watch is a different question — edge enhancement buys sharpness with haloes — and
    /// that one is signed off by eye, on real material, by the owner.
    /// </para>
    /// <para>
    /// Three answers of <see langword="null"/>, and they are not the same «no»: a processor that
    /// never offered the filter, a range with nothing above the driver's own default — where
    /// switching it on cannot move a pixel, so the call would only cost one — and a range that
    /// contradicts itself, which is not obeyed at all. A driver reporting a maximum under its
    /// minimum is reporting a bug, and following it would push an arbitrary number into a filter.
    /// </para>
    /// </remarks>
    public static int? EdgeEnhancementLevel(uint filterCaps, FilterRange range)
    {
        ArgumentNullException.ThrowIfNull(range);

        if (!OfferedFilters(filterCaps).Contains("EDGE_ENHANCEMENT"))
        {
            return null;
        }

        // A default below the floor the driver just declared is a driver contradicting itself, and
        // it is not obeyed: following it would push a number the same driver called out of bounds
        // into a filter.
        //
        // TWO other contradictions were written here and BOTH were taken out for the same measured
        // reason — nothing can tell them apart from their own absence:
        //
        //  · «Default above Maximum»: whenever it holds, the comparison below already answers null.
        //  · «Maximum at or below Minimum»: to reach a non-null answer a range needs Default at or
        //    above Minimum (this check) and Maximum above Default (the comparison), and those two
        //    together already say Maximum is above Minimum. Enumerated over the whole domain on
        //    2026-09-12: zero inputs separate the two versions.
        //
        // Both were found the same way and not by reading: they survived being deleted with every
        // row green. A guard that cannot fail is not protection, it is a sentence that reads like
        // protection — so it says so here instead of standing there.
        if (range.Default < range.Minimum)
        {
            return null;
        }

        return range.Maximum > range.Default ? range.Maximum : null;
    }

    /// <summary>
    /// What a filter level means in the filter's own units: <i>actual value = set value ×
    /// multiplier</i>, straight out of the documentation of
    /// <c>D3D11_VIDEO_PROCESSOR_FILTER_RANGE</c>, whose own example reads «a filter value of 2 would
    /// be interpreted by the device as 0.50».
    /// </summary>
    /// <remarks>
    /// One multiplication, and it is here rather than beside the call for two reasons. It is
    /// arithmetic, so rule 10 keeps it out of the excluded file; and without it the multiplier was a
    /// field nobody read — measured by deleting it from the record and watching everything compile
    /// and stay green, which is this repository's own characteristic defect in a new file.
    /// <b>Two cards that both answered «100» were not asked the same thing unless their multipliers
    /// agree</b>, so this is what makes the two measurements comparable at all.
    /// </remarks>
    public static float FilterStrength(int level, float multiplier) => level * multiplier;

    /// <summary>
    /// The picture the probe sends through the scaler, laid out as <paramref name="format"/> wants
    /// it and identical every run.
    /// </summary>
    /// <remarks>
    /// Hard edges at a small scale, because that is what a super resolution has anything to do with:
    /// an even field of one colour comes out of a scaler that did nothing looking exactly like it
    /// came out of one that did everything. Colour is left flat for the same reason — what these
    /// models rebuild is detail, and detail lives in the luma.
    /// </remarks>
    public static byte[] TestPattern(string format, int width, int height, out int pitch) =>
        TestPattern(format, width, height, UpscaleProbeContent.Bands, out pitch);

    /// <summary>
    /// The same picture in <paramref name="content"/>'s shape. <see cref="UpscaleProbeContent.Bands"/>
    /// is the original: hard synthetic edges, flat colour. <see cref="UpscaleProbeContent.Detail"/>
    /// is what a decoded frame looks like instead — gradients, fine texture, and colour that moves.
    /// </summary>
    public static byte[] TestPattern(
        string format,
        int width,
        int height,
        UpscaleProbeContent content,
        out int pitch)
    {
        if (content is not (UpscaleProbeContent.Bands or UpscaleProbeContent.Detail))
        {
            throw new ArgumentOutOfRangeException(
                nameof(content),
                content,
                "There is no such picture, and drawing another one would answer a question nobody asked.");
        }

        var detail = content == UpscaleProbeContent.Detail;
        switch (format)
        {
            case "YUY2":
                pitch = width * 2;
                var packed = new byte[pitch * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var at = (y * pitch) + (x * 2);
                        packed[at] = detail ? DetailLuma(x, y) : Luma(x, y);

                        // U and V alternate along the row in a packed 4:2:2 picture.
                        packed[at + 1] = detail ? DetailChroma(x / 2, y, (x & 1) == 0) : (byte)128;
                    }
                }

                return packed;

            case "NV12":
                pitch = width;
                var planar = new byte[width * height * 3 / 2];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        planar[(y * width) + x] = detail ? DetailLuma(x, y) : Luma(x, y);
                    }
                }

                if (detail)
                {
                    for (var y = 0; y < height / 2; y++)
                    {
                        for (var x = 0; x < width / 2; x++)
                        {
                            var at = (width * height) + (y * width) + (x * 2);
                            planar[at] = DetailChroma(x, y * 2, true);
                            planar[at + 1] = DetailChroma(x, y * 2, false);
                        }
                    }
                }
                else
                {
                    Array.Fill(planar, (byte)128, width * height, planar.Length - (width * height));
                }

                return planar;

            case "B8G8R8A8_UNORM":
            case "R8G8B8A8_UNORM":
                pitch = width * 4;
                var rgba = new byte[pitch * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var at = (y * pitch) + (x * 4);
                        var value = detail ? DetailLuma(x, y) : Luma(x, y);
                        rgba[at] = detail ? DetailChroma(x / 2, y, true) : value;
                        rgba[at + 1] = value;
                        rgba[at + 2] = detail ? DetailChroma(x / 2, y, false) : value;
                        rgba[at + 3] = 255;
                    }
                }

                return rgba;

            default:
                throw new NotSupportedException(
                    $"'{format}' is not a format this pipeline sends, so no picture is drawn for it.");
        }
    }

    /// <summary>
    /// How many bytes of two readbacks differ. Two of different lengths are as different as two
    /// pictures get: answering zero there would read as «nothing changed», which is the one wrong
    /// answer this measurement can give.
    /// </summary>
    public static long CountDifferences(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.Length != second.Length)
        {
            return Math.Max(first.Length, second.Length);
        }

        var differing = 0L;
        for (var at = 0; at < first.Length; at++)
        {
            if (first[at] != second[at])
            {
                differing++;
            }
        }

        return differing;
    }

    /// <summary>Broadcast black and white in bands three and five pixels wide, which never line up.</summary>
    private static byte Luma(int x, int y) => ((x / 3) + (y / 5)) % 2 == 0 ? (byte)235 : (byte)16;

    /// <summary>
    /// Luma for the detail picture: a slow diagonal gradient with fine texture over it and a small
    /// deterministic dither on top, which is the shape of a decoded frame rather than of a test card.
    /// Kept inside limited range, because that is what a decoder publishes.
    /// </summary>
    private static byte DetailLuma(int x, int y)
    {
        var gradient = 40 + (((x * 3) + (y * 5)) % 150);
        var texture = ((x / 2) + (y / 2)) % 2 == 0 ? 18 : -18;
        var dither = ((x * 7) + (y * 13) + (x * y / 3)) % 11 - 5;
        return (byte)Math.Clamp(gradient + texture + dither, 16, 235);
    }

    /// <summary>
    /// Chroma for the detail picture, which the band picture leaves flat at 128. Real frames carry
    /// colour that moves, and whether that matters to a vendor's model is exactly what is unmeasured.
    /// </summary>
    private static byte DetailChroma(int x, int y, bool isU)
    {
        var wave = isU ? (x * 5) + (y * 2) : (x * 2) - (y * 5);
        return (byte)Math.Clamp(128 + (wave % 47) - 23, 16, 240);
    }

    /// <summary>
    /// What a card's answer means, decided here rather than in a person's head.
    /// </summary>
    /// <remarks>
    /// It was prose in an evidence document until 2026-09-12, which is how «inconclusive» becomes
    /// «it does not work» a month later. The distinction that matters is the last one: a card that
    /// accepted the request and moved nothing has not refused anything — NVIDIA's driver accepts and
    /// ignores while the feature is off in its own application — so it is never reported as a
    /// failure.
    /// </remarks>
    public static UpscaleVerdict Verdict(UpscalePixelComparison? pixels, VendorExtensionOutcome? extension)
    {
        if (pixels is null)
        {
            return UpscaleVerdict.NotRun;
        }

        // The controls first, and the order is load-bearing: asking about the extension before them
        // reports a card whose readback is broken as «nothing to ask» instead of as untrustworthy.
        //
        // «Two distinct byte values» is the black frame a blt that drew nothing leaves behind — the
        // output is BGRA with opaque alpha, so it comes back as 0 and 255 and nothing else.
        if (pixels.WorstBltResult < 0
            || !pixels.ReadbacksAgreeInLength
            || pixels.ControlDifferingBytes != 0
            || pixels.DistinctValues <= 2)
        {
            return UpscaleVerdict.Untrustworthy;
        }

        // A comparison of one draw against itself is not a measurement whatever its number says.
        if (extension is not null && !extension.ComparesTwoConfigurations)
        {
            return UpscaleVerdict.Untrustworthy;
        }

        if (extension is null)
        {
            return UpscaleVerdict.NotAsked;
        }

        if (extension.OnResult < 0)
        {
            return UpscaleVerdict.Refused;
        }

        return pixels.DifferingBytes > 0 ? UpscaleVerdict.Works : UpscaleVerdict.Inconclusive;
    }
}

/// <summary>
/// One adapter's answer, written the way a person reads it a month later.
/// </summary>
/// <remarks>
/// This lives in production and not in the test that writes the file, and that is the point: the
/// report carried nineteen columns of numbers and <b>not one of them was the verdict</b>, so the
/// classification only ever ran inside the test that checked the classification. Registered and
/// never fed, which is the defect this repository is named after.
/// </remarks>
public static class UpscaleProbeReport
{
    /// <summary>The one line per adapter that says what happened, verdict first.</summary>
    public static string Describe(AdapterUpscaleProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var verdict = D3d11UpscaleFormats.Verdict(probe.Pixels, probe.Extension);
        var filters = probe.Filters is null ? "not run" : string.Join(' ', probe.Filters.Offered);
        var differing = probe.Pixels is null
            ? "not run"
            : probe.Pixels.DifferingBytes.ToString(CultureInfo.InvariantCulture);
        var edge = probe.Filters is null
            ? "not run"
            : probe.Filters.EdgeEnhancementDifferingBytes.ToString(CultureInfo.InvariantCulture);

        // The level and what it means, because «edge=0» reads identically whether the filter was
        // asked for and did nothing or was never asked at all — and those are opposite findings.
        var level = probe.Filters?.EdgeEnhancementLevel is { } asked
            ? FormattableString.Invariant($"{asked}@{probe.Filters.EdgeEnhancementStrength}")
            : "not asked";

        return FormattableString.Invariant(
            $"{probe.Description} [{probe.Vendor}] {verdict} input={probe.PreferredInput ?? "none"} ")
            + FormattableString.Invariant($"differing={differing} filters=[{filters}] edge={edge}")
            + FormattableString.Invariant($" edge_level={level}")
            + (probe.Note is null ? string.Empty : $" note={probe.Note}");
    }
}

/// <summary>What the measurement concluded about one card's vendor super resolution.</summary>
public enum UpscaleVerdict
{
    /// <summary>No picture was sent through this card's processor.</summary>
    NotRun,

    /// <summary>The measurement failed one of its own controls, so it says nothing either way.</summary>
    Untrustworthy,

    /// <summary>This card has no vendor extension to ask, so none was asked.</summary>
    NotAsked,

    /// <summary>The driver refused the request outright.</summary>
    Refused,

    /// <summary>Accepted and changed nothing, which is what an unswitched feature looks like.</summary>
    Inconclusive,

    /// <summary>Accepted and the picture changed.</summary>
    Works,
}

/// <summary>
/// What a card answered when it was asked to enlarge a picture, and whether a pixel moved.
/// </summary>
/// <param name="DifferingBytes">Bytes that differ between the extension switched on and switched off.</param>
/// <param name="ControlDifferingBytes">
/// Bytes that differ between two runs with the extension switched off. It has to be zero: a readback
/// that disagrees with itself cannot say anything about the run that mattered.
/// </param>
/// <param name="DistinctValues">
/// How many different byte values the picture that came out carries. The positive control, and the
/// probe is worthless without it: a <c>VideoProcessorBlt</c> that drew nothing at all leaves three
/// identical black readbacks, which counts zero differences and zero control differences exactly
/// like a super resolution that ran and changed nothing.
/// </param>
/// <param name="WorstBltResult">
/// The worst of the three draws, not the first. Two of them used to be thrown away, and a pair of
/// failed «off» draws leaves the output texture untouched: zero differences, zero control, and a
/// report that reads «the super resolution did nothing».
/// </param>
/// <param name="ReadbacksAgreeInLength">
/// Whether the three readbacks came back the same size. A failed <c>Map</c> returns nothing, and the
/// difference count then answers the longer length — which in the control reads correctly as «these
/// disagree» and in the headline reads as «the whole picture changed».
/// </param>
public sealed record UpscalePixelComparison(
    long DifferingBytes,
    long ControlDifferingBytes,
    long TotalBytes,
    int DistinctValues,
    int WorstBltResult,
    bool ReadbacksAgreeInLength);

/// <summary>
/// Both halves of the vendor's switch. The «off» call is recorded because the measurement is the
/// difference between the two: without it the headline number silently becomes zero.
/// </summary>
/// <param name="OnRequested">
/// What the «on» call actually asked for, and <paramref name="OffRequested"/> what the «off» one
/// did. <b>The two flags are the measurement, not the two result codes.</b> Recording only the
/// codes left three separate mutations green — switching the second call to «on», deleting it, and
/// deleting the first — while Intel's headline fell from 18 109 378 differing bytes to a silent
/// zero: every one of them makes the probe compare a draw against itself, and every one of them
/// still returns <c>S_OK</c>.
/// </param>
public sealed record VendorExtensionOutcome(
    int OnResult,
    int OffResult,
    uint RefusedFunction,
    bool OnRequested,
    bool OffRequested)
{
    /// <summary>
    /// Whether the two draws being compared were configured differently at all. False means the
    /// headline number is one draw measured against itself, whatever it says.
    /// </summary>
    public bool ComparesTwoConfigurations => OnRequested != OffRequested;
}

/// <summary>
/// What a processor offers without anybody switching anything on: the standard Direct3D filters it
/// declares, and whether asking for edge enhancement changes the picture.
/// </summary>
/// <param name="EdgeEnhancementLevel">
/// The level that was asked for, or <see langword="null"/> when nothing was. <b>It is here because
/// its absence made a test lie</b>: while only the differing-bytes count travelled, a processor that
/// offers the filter but declares a range with no room read as «offers edge enhancement and changed
/// not one byte with it at maximum» — a sentence about a call that never happened.
/// </param>
/// <param name="EdgeEnhancementStrength">
/// That level in the filter's own units, which is what makes two cards comparable: the same number
/// under different multipliers is a different request.
/// </param>
public sealed record StandardFilterOutcome(
    uint FilterCaps,
    IReadOnlyList<string> Offered,
    long EdgeEnhancementDifferingBytes,
    int? EdgeEnhancementLevel,
    float EdgeEnhancementStrength);

/// <summary>Everything one adapter answered about enlarging pictures.</summary>
/// <summary>
/// Two timings of the same enlargement that differ by <paramref name="ExtraPasses"/> passes, so the
/// fixed cost around the work can be subtracted from it rather than charged to it.
/// </summary>
/// <param name="One">One pass, plus the read-back and the flush around it.</param>
/// <param name="Many">One pass plus <paramref name="ExtraPasses"/> more, plus the same fixed cost.</param>
/// <param name="ExtraPasses">How many passes separate the two timings.</param>
/// <remarks>
/// The raw timings travel rather than a verdict, and that is deliberate: what a cost means depends on
/// the cadence of the film being played, which this probe does not know. <c>UpscaleCostPolicy</c> is
/// what turns these into an answer, and it lives in the domain where it can be tested — measuring
/// needs a graphics card, dividing does not.
/// </remarks>
public sealed record UpscaleTiming(TimeSpan One, TimeSpan Many, int ExtraPasses);

public sealed record AdapterUpscaleProbe(
    string Description,
    uint VendorId,
    GpuVendor Vendor,
    IReadOnlyDictionary<string, uint> FormatSupport,
    string? PreferredInput,
    VendorExtensionOutcome? Extension,
    StandardFilterOutcome? Filters,
    UpscalePixelComparison? Pixels,
    string? Note)
{
    /// <summary>What one enlargement costs on this adapter with the vendor extension off.</summary>
    public UpscaleTiming? Plain { get; init; }

    /// <summary>The same, with the vendor extension on, so the price of turning it on can be read.</summary>
    public UpscaleTiming? Enhanced { get; init; }
}

