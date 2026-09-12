// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.Versioning;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// PLY-016's measurement, run against whatever cards this machine has. It answers the question the
/// feature was blocked on: which picture format the video processor takes, and whether the vendor's
/// super resolution changes a single pixel when it is switched on.
/// </summary>
/// <remarks>
/// <b>What a green run here does not mean.</b> Everything below other than the pixel count is a card
/// answering a question about itself, and a card answers those the same way whether or not its super
/// resolution ever runs — NVIDIA's driver accepts the extension and ignores it until somebody
/// switches RTX Video Super Resolution on in the NVIDIA app. So a zero in the pixel count is
/// <b>inconclusive</b>, never «it does not work», and the report says so in its own words.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsVideoUpscaleProbeTests
{
    /// <summary>720p into a 4K screen, which is the case the owner widened the feature to cover.</summary>
    private const int SourceWidth = 1280;
    private const int SourceHeight = 720;

    /// <summary>
    /// Runs the probe once and writes what every adapter answered. The report is the deliverable:
    /// a row per adapter and format, so a later reader can tell a question that was asked and
    /// answered "no" from one that was never asked.
    /// </summary>
    [Fact]
    public void Every_adapter_answers_the_whole_battery_and_the_answers_are_written_down()
    {
        var probes = Probe();

        // Asserted, not skipped: every Windows machine reports at least one adapter, so an empty
        // answer is a probe that stopped working. It used to be a skip, and a `ProbeAll` that
        // returned nothing at all left this whole suite green — measured 2026-09-12.
        Assert.NotEmpty(probes);

        var battery = D3d11UpscaleFormats.Battery.Select(format => format.Name).ToArray();
        foreach (var probe in probes)
        {
            // An adapter that reached its video processor answered about EVERY format. «About none»
            // used to be accepted here too, and it is indistinguishable from a battery that never
            // ran: deleting the loop left this green while the report called an RTX 5070 a card
            // that «takes no format this pipeline can produce».
            Assert.True(
                probe.Note?.Contains("ID3D11Video", StringComparison.Ordinal) == true
                    || battery.All(probe.FormatSupport.ContainsKey),
                $"'{probe.Description}' answered about {probe.FormatSupport.Count} of {battery.Length} formats.");

            // An adapter that sent no picture has to say why. One that reports neither a comparison
            // nor a reason is an adapter whose answer went missing between here and the report, and
            // reads afterwards as a card that simply cannot do it.
            Assert.True(
                probe.Pixels is not null || probe.Note is not null,
                $"'{probe.Description}' reported neither a comparison nor a reason.");
        }
    }

    /// <summary>
    /// The only assertion that proves anything about the feature. A readback that disagrees with
    /// itself cannot say whether the extension did something, so the control is checked first and
    /// the measurement is only believed after it.
    /// </summary>
    [Fact]
    public void A_readback_that_disagrees_with_itself_is_caught_before_anything_is_concluded()
    {
        var measured = Probe().Where(probe => probe.Pixels is not null).ToArray();
        Assert.SkipWhen(
            measured.Length == 0,
            "No adapter on this machine ran a picture through its video processor.");

        foreach (var probe in measured)
        {
            // The negative control: two runs with the extension off have to agree, or the readback
            // is noise and says nothing about the run that mattered.
            Assert.Equal(0, probe.Pixels!.ControlDifferingBytes);

            // The positive control, and without it a zero above would be worthless: a blt that drew
            // nothing leaves three identical black pictures, which counts zero differences and zero
            // control differences exactly like a super resolution that ran and changed nothing.
            //
            // The WORST of the three draws, not the first: two failed «off» draws leave the output
            // untouched and read as «the super resolution did nothing».
            Assert.Equal(0, probe.Pixels.WorstBltResult);
            Assert.True(probe.Pixels.TotalBytes > 0, $"'{probe.Description}' read back an empty picture.");
            Assert.True(
                probe.Pixels.ReadbacksAgreeInLength,
                $"'{probe.Description}' read back three pictures of different sizes, so the headline count is "
                + "a length difference wearing the clothes of a pixel difference.");
            Assert.True(
                probe.Pixels.DistinctValues > 2,
                $"'{probe.Description}' drew a picture of {probe.Pixels.DistinctValues} distinct byte values, "
                + "so its processor did not draw the test pattern and no zero it reports means anything.");
        }
    }

    /// <summary>
    /// <b>That the vendor's switch was actually thrown, both ways.</b> Deleting either call left the
    /// suite green and turned Intel's measured 18 109 378 differing bytes into a silent zero —
    /// measured 2026-09-12. The headline number is the difference between two draws, so a test that
    /// never asks whether the second draw was configured is measuring one draw against itself.
    /// </summary>
    [Fact]
    public void The_vendor_switch_is_recorded_in_both_positions_or_the_headline_is_one_draw_twice()
    {
        var vendors = Probe()
            .Where(probe => probe.Vendor is GpuVendor.Nvidia or GpuVendor.Intel && probe.Pixels is not null)
            .ToArray();
        Assert.SkipWhen(vendors.Length == 0, "No NVIDIA or Intel adapter on this machine drew a picture.");

        foreach (var probe in vendors)
        {
            Assert.NotNull(probe.Extension);
            Assert.True(
                probe.Extension!.OffResult >= 0,
                $"'{probe.Description}' never got its extension switched back off (0x{probe.Extension.OffResult:X8}), "
                + "so the two draws being compared had the same configuration.");
        }
    }

    /// <summary>
    /// What every card offers with nobody switching anything on. This is the floor under PLY-016:
    /// these filters belong to Direct3D rather than to a graphics card company, so they need no
    /// vendor application, no user setting and no licence.
    /// </summary>
    [Fact]
    public void The_standard_filters_a_processor_offers_are_recorded_for_every_adapter_that_has_one()
    {
        var measured = Probe().Where(probe => probe.Filters is not null).ToArray();
        Assert.SkipWhen(measured.Length == 0, "No adapter on this machine has a video processor.");

        foreach (var probe in measured)
        {
            // The names have to come from the bitmask and agree with it, or the report is listing
            // filters nobody asked the card about.
            Assert.Equal(
                D3d11UpscaleFormats.OfferedFilters(probe.Filters!.FilterCaps),
                probe.Filters.Offered);

            // Offered and measured means the picture has to change; offered and unchanged would be
            // a filter that reports itself and does nothing, which is the defect this house is
            // named after.
            if (probe.Filters.Offered.Contains("EDGE_ENHANCEMENT"))
            {
                Assert.True(
                    probe.Filters.EdgeEnhancementDifferingBytes > 0,
                    $"'{probe.Description}' offers edge enhancement and changed not one byte with it at maximum.");
            }
        }
    }

    /// <summary>
    /// The target is the policy's, not a number typed here: the same arithmetic the player will run
    /// decides how large the picture is enlarged, so the measurement and the feature cannot drift
    /// apart.
    /// </summary>
    private static IReadOnlyList<AdapterUpscaleProbe> Probe()
    {
        var decision = UpscalePolicy.Decide(SourceWidth, SourceHeight, 3840, 2160);
        Assert.True(decision.ShouldUpscale, "720p on a 4K screen stopped being a case for enlargement.");

        var probes = WindowsVideoUpscaleProbe.ProbeAll(
            SourceWidth, SourceHeight, decision.TargetWidth, decision.TargetHeight);
        Write(probes, decision);
        return probes;
    }

    private static void Write(IReadOnlyList<AdapterUpscaleProbe> probes, UpscaleDecision decision)
    {
        var directory = RepositoryLayout.PathFromRoot("artifacts", "test-results", "PLY-016");
        Directory.CreateDirectory(directory);
        var rows = new List<string>
        {
            "adapter,vendor,vendor_id,format,as_input,as_output,flags,preferred_input,extension_on,extension_off,"
            + "differing_bytes,control_differing_bytes,total_bytes,distinct_values,worst_blt,"
            + "filter_caps,offered_filters,edge_enhancement_differing,note",
        };

        foreach (var probe in probes)
        {
            foreach (var format in D3d11UpscaleFormats.Battery)
            {
                var flags = probe.FormatSupport.TryGetValue(format.Name, out var value) ? value : 0u;
                rows.Add(string.Join(
                    ',',
                    Quote(probe.Description),
                    probe.Vendor,
                    FormattableString.Invariant($"0x{probe.VendorId:X4}"),
                    format.Name,
                    D3d11UpscaleFormats.AcceptsAsInput(flags),
                    D3d11UpscaleFormats.AcceptsAsOutput(flags),
                    flags.ToString(CultureInfo.InvariantCulture),
                    probe.PreferredInput ?? string.Empty,
                    // «not attempted» and «S_OK» are different answers, and writing 0x00000000 for
                    // both made an adapter with no video processor read exactly like one that
                    // accepted the extension.
                    Hresult(probe.Extension?.OnResult),
                    Hresult(probe.Extension?.OffResult),
                    (probe.Pixels?.DifferingBytes ?? -1).ToString(CultureInfo.InvariantCulture),
                    (probe.Pixels?.ControlDifferingBytes ?? -1).ToString(CultureInfo.InvariantCulture),
                    (probe.Pixels?.TotalBytes ?? -1).ToString(CultureInfo.InvariantCulture),
                    (probe.Pixels?.DistinctValues ?? -1).ToString(CultureInfo.InvariantCulture),
                    Hresult(probe.Pixels?.WorstBltResult),
                    probe.Filters is null ? "not run" : FormattableString.Invariant($"0x{probe.Filters.FilterCaps:X2}"),
                    Quote(probe.Filters is null ? string.Empty : string.Join(' ', probe.Filters.Offered)),
                    (probe.Filters?.EdgeEnhancementDifferingBytes ?? -1).ToString(CultureInfo.InvariantCulture),
                    Quote(probe.Note ?? string.Empty)));
            }
        }

        File.WriteAllLines(Path.Combine(directory, "d3d11-upscale-probe.csv"), rows);

        var summary = new List<string>
        {
            FormattableString.Invariant(
                $"{SourceWidth}x{SourceHeight} -> {decision.TargetWidth}x{decision.TargetHeight}, {probes.Count} adapter(s)."),
        };
        summary.AddRange(probes.Select(probe => FormattableString.Invariant(
            $"{probe.Description} [{probe.Vendor}] input={probe.PreferredInput ?? "none"}")
            + $" extension_on={Hresult(probe.Extension?.OnResult)} extension_off={Hresult(probe.Extension?.OffResult)}"
            + FormattableString.Invariant($" differing={Number(probe.Pixels?.DifferingBytes)}")
            + FormattableString.Invariant($" control={Number(probe.Pixels?.ControlDifferingBytes)}")
            + FormattableString.Invariant($" total={Number(probe.Pixels?.TotalBytes)}")
            + FormattableString.Invariant($" distinct={Number(probe.Pixels?.DistinctValues)}")
            + $" worst_blt={Hresult(probe.Pixels?.WorstBltResult)}"
            + $" filters=[{(probe.Filters is null ? "not run" : string.Join(' ', probe.Filters.Offered))}]"
            + FormattableString.Invariant($" edge={Number(probe.Filters?.EdgeEnhancementDifferingBytes)}")
            + $" note={probe.Note ?? "-"}"));
        File.WriteAllLines(Path.Combine(directory, "d3d11-upscale-probe.txt"), summary);
    }

    private static string Number(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "not run";

    /// <summary>A result code, or the words for never having asked. They are not the same answer.</summary>
    private static string Hresult(int? value) =>
        value is null ? "not attempted" : FormattableString.Invariant($"0x{value.Value:X8}");

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
