// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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

            // WHAT WAS SENT, not what came back. Asserting the result codes was the previous
            // version of this test and it did not bite: switching the second call to «on»,
            // deleting it, and deleting the first all return S_OK and all left the suite green
            // while Intel's headline fell from 18 109 378 to zero — measured 2026-09-12.
            Assert.True(
                probe.Extension!.ComparesTwoConfigurations,
                $"'{probe.Description}' drew both pictures with the switch in the same position "
                + $"(on={probe.Extension.OnRequested}, off={probe.Extension.OffRequested}), so its "
                + "headline number is one draw measured against itself.");
            Assert.True(probe.Extension.OffResult >= 0, $"0x{probe.Extension.OffResult:X8} switching it back off.");
        }
    }

    /// <summary>
    /// A card that measured its own picture is never reported as a refusal, and one that could not
    /// is never reported as a success. The verdict is production's and not this test's.
    /// </summary>
    [Fact]
    public void Every_adapter_is_given_the_verdict_its_own_numbers_earn()
    {
        foreach (var probe in Probe())
        {
            var verdict = D3d11UpscaleFormats.Verdict(probe.Pixels, probe.Extension);
            var line = UpscaleProbeReport.Describe(probe);

            Assert.Contains(verdict.ToString(), line, StringComparison.Ordinal);
            if (probe.Pixels is null)
            {
                Assert.Equal(UpscaleVerdict.NotRun, verdict);
            }
            else
            {
                Assert.NotEqual(UpscaleVerdict.NotRun, verdict);
            }
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
            // The capabilities have to have been READ. Comparing the names against
            // OfferedFilters(FilterCaps) was the previous version of this and it compared the
            // function with itself: throwing away GetVideoProcessorCaps' answer left the whole
            // floor — «filters=[] edge=-1» on both cards — with 105 tests green.
            Assert.True(
                probe.Filters!.FilterCaps != 0,
                $"'{probe.Description}' has a video processor that declares no filter at all, which is "
                + "what an unread capabilities struct looks like.");

            // Offered and measured means the picture has to change; offered and unchanged would be
            // a filter that reports itself and does nothing, which is the defect this house is
            // named after.
            Assert.Contains("EDGE_ENHANCEMENT", probe.Filters.Offered);

            // Declared and asked for stopped being the same thing when the level became a policy: a
            // processor that offers the filter and then declares a range with no room above its own
            // default is never asked. Saying that one «changed not one byte at maximum» would be a
            // sentence about a call that never happened, so the two are separated before the count
            // is read at all.
            Assert.True(
                probe.Filters.EdgeEnhancementLevel is not null,
                $"'{probe.Description}' declares edge enhancement and the range it declared was "
                + "refused, so nothing was asked for and the count beside it measures no call.");

            Assert.True(
                probe.Filters.EdgeEnhancementDifferingBytes > 0,
                FormattableString.Invariant(
                    $"'{probe.Description}' was asked for edge enhancement at level ")
                + FormattableString.Invariant(
                    $"{probe.Filters.EdgeEnhancementLevel} ({probe.Filters.EdgeEnhancementStrength} ")
                + "in the filter's own units) and changed not one byte.");
        }
    }

    /// <summary>
    /// The target is the policy's, not a number typed here: the same arithmetic the player will run
    /// decides how large the picture is enlarged, so the measurement and the feature cannot drift
    /// apart.
    /// </summary>
    /// <summary>
    /// <b>A zero measured with one picture and one format is not «this card does nothing».</b>
    /// NVIDIA answered <c>S_OK</c> and moved no byte with hard synthetic bands in YUY2, and the two
    /// things that measurement never varied are the two a vendor's model would care about: the
    /// content, because these are networks trained on compressed video and bands with flat colour
    /// give them nothing to rebuild; and the format, because both VLC and Chromium hand the
    /// processor NV12 off a hardware decoder rather than the packed format this pipeline prefers.
    /// </summary>
    /// <remarks>
    /// Chromium checks neither — measured in its source on 2026-09-12, <c>ToggleNvidiaVpSuperResolution</c>
    /// gates on the vendor and on battery power and nothing else, and its input may be NV12, YUY2 or
    /// P010 — so the format is the weaker of the two. It is measured anyway, because «not with any of
    /// these» is a different sentence from «not with the one we tried», and the whole point of the
    /// probe is to be able to say the first one.
    /// </remarks>
    [Fact]
    public void The_zero_is_measured_against_every_picture_and_format_the_card_takes()
    {
        var decision = UpscalePolicy.Decide(SourceWidth, SourceHeight, 3840, 2160);
        var baseline = WindowsVideoUpscaleProbe.ProbeAll(
            SourceWidth, SourceHeight, decision.TargetWidth, decision.TargetHeight);
        var withProcessor = baseline.Where(probe => probe.Pixels is not null).ToList();
        Assert.SkipWhen(withProcessor.Count == 0, "no adapter here exposes a video processor.");

        var rows = new List<string>();
        foreach (var probe in withProcessor)
        {
            foreach (var format in new[] { "YUY2", "NV12" })
            {
                if (!probe.FormatSupport.TryGetValue(format, out var flags)
                    || !D3d11UpscaleFormats.AcceptsAsInput(flags))
                {
                    continue;
                }

                foreach (var content in new[] { UpscaleProbeContent.Bands, UpscaleProbeContent.Detail })
                {
                    var measured = WindowsVideoUpscaleProbe.ProbeAll(
                        SourceWidth,
                        SourceHeight,
                        decision.TargetWidth,
                        decision.TargetHeight,
                        content,
                        format);
                    var match = measured.Single(other => other.Description == probe.Description);

                    // That the format asked for is the format sent. Without this the loop can run
                    // four times over the same preferred input and report a matrix that varied
                    // nothing — which reads exactly like a matrix that varied everything.
                    Assert.Equal(format, match.PreferredInput);

                    // Every combination has to have actually drawn something, or a zero below is a
                    // blt that never ran rather than a super resolution that did nothing.
                    Assert.NotNull(match.Pixels);
                    Assert.Equal(0, match.Pixels!.WorstBltResult);
                    Assert.True(
                        match.Pixels.DistinctValues > 2,
                        $"{probe.Description} {format} {content}: the readback carries "
                        + $"{match.Pixels.DistinctValues} distinct values, so nothing was drawn.");
                    Assert.Equal(0, match.Pixels.ControlDifferingBytes);

                    rows.Add(string.Join(
                        ',',
                        probe.Description.Replace(",", " ", StringComparison.Ordinal),
                        format,
                        content.ToString(),
                        match.Pixels.DifferingBytes.ToString(CultureInfo.InvariantCulture),
                        match.Pixels.TotalBytes.ToString(CultureInfo.InvariantCulture),
                        match.Pixels.DistinctValues.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        Assert.NotEmpty(rows);
        var directory = RepositoryLayout.PathFromRoot("artifacts", "test-results", "PLY-016");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(
            Path.Combine(directory, "d3d11-upscale-matrix.csv"),
            rows.Prepend("adapter,format,content,differing_bytes,total_bytes,distinct_values"));
    }

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
        // The verdict first, from production. The columns after it are the numbers it was read from.
        summary.AddRange(probes.Select(probe => UpscaleProbeReport.Describe(probe)
            + $" | extension_on={Hresult(probe.Extension?.OnResult)} extension_off={Hresult(probe.Extension?.OffResult)}"
            + FormattableString.Invariant($" requested_on={probe.Extension?.OnRequested} off={probe.Extension?.OffRequested}")
            + FormattableString.Invariant($" control={Number(probe.Pixels?.ControlDifferingBytes)}")
            + FormattableString.Invariant($" total={Number(probe.Pixels?.TotalBytes)}")
            + FormattableString.Invariant($" distinct={Number(probe.Pixels?.DistinctValues)}")
            + $" worst_blt={Hresult(probe.Pixels?.WorstBltResult)}"
            + Timing(probe)));
        File.WriteAllLines(Path.Combine(directory, "d3d11-upscale-probe.txt"), summary);
    }

    /// <summary>
    /// What one enlarged frame costs on each adapter, and whether it leaves room for the rest of the
    /// frame (PLY-016, and the figure PLY-018's criterion owes).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two timings and a subtraction, which is what separates the enlargement from the 33 MB
    /// read-back around it — the arithmetic lives in <c>UpscaleCostPolicy</c>, where it is tested on
    /// any machine, and only the clock is here.
    /// </para>
    /// <para>
    /// <b>What this asserts is that a measurement happened at all</b>, not a ceiling: a cost is a
    /// property of the card in front of it, and failing a machine for being slow would fail the
    /// machines this feature is for. The ceiling is reported so a person can read it.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_cost_of_one_enlarged_frame_is_measured_on_every_adapter()
    {
        var decision = UpscalePolicy.Decide(SourceWidth, SourceHeight, 3840, 2160);
        var probes = WindowsVideoUpscaleProbe.ProbeAll(
            SourceWidth, SourceHeight, decision.TargetWidth, decision.TargetHeight);

        // An adapter that never reached a video processor cannot be timed, and a machine made only of
        // those is a real machine rather than a failed measurement: every hosted runner is one, which
        // is how this assertion was found — it demanded a timing from a machine that has no card to
        // give it, and turned a green suite red on CI while passing on any desk (2026-09-13).
        var capable = probes
            .Where(probe => probe.Note?.Contains("ID3D11Video", StringComparison.Ordinal) != true)
            .ToArray();
        var timed = probes.Where(probe => probe.Plain is not null).ToArray();

        // What IS a failed measurement: an adapter that reached its video processor and came back
        // untimed anyway. That is the distinction the old message claimed to draw and did not.
        Assert.All(
            capable,
            probe => Assert.True(
                probe.Plain is not null,
                $"{probe.Description} reached its video processor and was not timed, so this measured "
                    + "nothing rather than finding a machine without one."));

        // And the floor against going blind, because «nothing to measure» is the shape of a probe
        // that stopped working: a machine that times nothing has to say, adapter by adapter, that it
        // has no video processor. Passing in silence would look identical on a broken probe.
        if (capable.Length == 0)
        {
            Assert.All(
                probes,
                probe => Assert.True(
                    probe.Note?.Contains("ID3D11Video", StringComparison.Ordinal) == true,
                    $"{probe.Description} was not timed and does not say it lacks a video processor, "
                        + "so the probe answered nothing instead of answering «no card»."));
        }

        foreach (var probe in timed)
        {
            var plain = probe.Plain!;

            // The clock ran: a zero here is a Stopwatch that never started, which reads exactly like
            // an enlargement that costs nothing and is a different answer entirely.
            Assert.True(
                plain.One > TimeSpan.Zero,
                $"{probe.Description} reported a single pass taking no time at all, so the clock is "
                    + "not being read rather than the card being instant.");

            // And the extra passes cost something: many passes measuring the same as one is the
            // shape of a loop that was optimised away or a blt that never reached the card.
            Assert.True(
                plain.Many > plain.One,
                $"{probe.Description} took {plain.Many.TotalMilliseconds:0.###} ms for "
                    + $"{1 + plain.ExtraPasses} passes and {plain.One.TotalMilliseconds:0.###} ms for one, "
                    + "so the extra passes did nothing measurable.");

            var cost = UpscaleCostPolicy.MeasureDifference(
                plain.Many, plain.One, plain.ExtraPasses, CinemaFramesPerSecond);
            Assert.True(
                cost.PerFrame > TimeSpan.Zero,
                $"{probe.Description} produced two timings that subtract to nothing.");
        }
    }

    /// <summary>
    /// The cadence this cost is judged against: 24, which is what a film carries and the tightest of
    /// the cadences a library of series and films actually holds.
    /// </summary>
    private const double CinemaFramesPerSecond = 24;

    private static string Timing(AdapterUpscaleProbe probe)
    {
        if (probe.Plain is not { } plain)
        {
            return " cost=not timed";
        }

        var plainCost = UpscaleCostPolicy.MeasureDifference(
            plain.Many, plain.One, plain.ExtraPasses, CinemaFramesPerSecond);
        var enhanced = probe.Enhanced is { } on
            ? UpscaleCostPolicy.MeasureDifference(on.Many, on.One, on.ExtraPasses, CinemaFramesPerSecond)
            : default;

        return FormattableString.Invariant(
            $" cost_plain_ms={plainCost.PerFrame.TotalMilliseconds:0.###}")
            + FormattableString.Invariant($" share_plain={plainCost.FrameBudgetShare:0.###}")
            + FormattableString.Invariant($" fits_plain={plainCost.FitsBudget}")
            + FormattableString.Invariant($" cost_enhanced_ms={enhanced.PerFrame.TotalMilliseconds:0.###}")
            + FormattableString.Invariant($" share_enhanced={enhanced.FrameBudgetShare:0.###}")
            + FormattableString.Invariant($" fits_enhanced={enhanced.FitsBudget}");
    }

    private static string Number(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "not run";

    /// <summary>A result code, or the words for never having asked. They are not the same answer.</summary>
    private static string Hresult(int? value) =>
        value is null ? "not attempted" : FormattableString.Invariant($"0x{value.Value:X8}");

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
