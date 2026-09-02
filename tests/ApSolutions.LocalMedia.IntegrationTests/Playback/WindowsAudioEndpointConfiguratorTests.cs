// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.Versioning;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// The endpoint's channel layout is written through an interface Windows does not document.
/// </summary>
/// <remarks>
/// <b>This suite exists to fail loudly the day Windows changes it.</b> That is the price of using
/// <c>IPolicyConfig</c>, accepted deliberately: it is what the sound panel calls and there is no
/// documented equivalent, and every alternative was measured on 2026-09-02 and does nothing —
/// LibVLC's live channel API takes stereo modes alone, <c>--stereo-mode=1</c> changed not one
/// decibel of eight tones, and <c>--audio-channels</c> is not an option at all.
/// <para>
/// Without a test that says so, a Windows update would turn the control into one that quietly does
/// nothing, which is exactly the shape of defect this repository is named after.
/// </para>
/// <para>
/// <b>And here is what it does NOT catch, measured 2026-09-02 rather than assumed.</b> No test in
/// this file reaches a successful <c>SetDeviceFormat</c>, so replacing that call's body with
/// <c>=&gt; 0;</c> — S_OK reported, nothing written — leaves the suite green. Reaching it would mean
/// writing a format the endpoint does not already carry and then restoring it, and this suite
/// deliberately does not: a test run must not change what comes out of somebody's speakers, and a
/// process killed between the write and the restore would leave it changed. On a machine whose every
/// endpoint declares two channels there is no <c>Applied</c> path that is not such a write, and both
/// halves of that shape — the refusal and the layouts on offer — are measured deterministically
/// through the seam in <c>EndpointFormatArithmeticTests</c> instead.
/// </para>
/// <para>
/// So what this suite guards is narrower than "the control still works": that the interface still
/// answers on this Windows, that a refusal arrives as a refusal rather than as an exception from the
/// marshaller, and that an endpoint which is gone is told apart from a driver that says no. The day
/// the vtable moves, the first of those goes red. A silent no-op inside a call that still returns
/// S_OK would not, and that is written here so nobody reads a green run as more than it is.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsAudioEndpointConfiguratorTests
{
    [Fact]
    public void The_undocumented_interface_still_answers_on_this_Windows()
    {
        var configurator = new WindowsAudioEndpointConfigurator();

        Assert.True(
            configurator.IsAvailable,
            "IPolicyConfig no longer answers on this Windows. The channel-layout control cannot work "
            + "and must say so rather than appear to; see the class remarks for what was measured.");
    }

    [Fact]
    public async Task An_endpoint_reports_the_layouts_its_driver_takes_rather_than_the_one_it_carries()
    {
        var endpoints = WasapiLoopbackRecorder.ActiveRenderEndpoints();
        Assert.SkipWhen(endpoints.Count == 0, "this machine offers no active render endpoint.");

        var configurator = new WindowsAudioEndpointConfigurator();
        var endpoint = endpoints.MaxBy(candidate => candidate.Channels);
        var supported = await configurator.GetSupportedLayoutsAsync(
            endpoint.Id,
            TestContext.Current.CancellationToken);

        // Stereo always: an endpoint that refused two channels is one nothing could play to.
        Assert.Contains(AudioChannelLayout.Stereo, supported);

        // And what it is set to has to be among them, which is the half that catches a query that
        // answered about the wrong endpoint.
        //
        // Skipped rather than asserted below two channels, and that is the point of the skip: on a
        // stereo-only endpoint `carried` IS Stereo, so this second assertion becomes a copy of the
        // first and the whole test asks one question twice. It read that way until 2026-09-02, on
        // every machine it has ever run on — this one, where every endpoint declares two channels,
        // and a hosted runner, which has none at all.
        Assert.SkipWhen(
            endpoint.Channels < 6,
            $"the widest endpoint here carries {endpoint.Channels} channels, so what it is set to is "
            + "Stereo and this half would repeat the assertion above.");

        var carried = endpoint.Channels >= 8
            ? AudioChannelLayout.Surround71
            : AudioChannelLayout.Surround51;
        Assert.Contains(carried, supported);
    }

    /// <summary>
    /// Writing a layout the driver does not take is refused rather than attempted.
    /// </summary>
    /// <remarks>
    /// Asserted on an endpoint the machine actually has, so this is not a test of a made-up
    /// identifier: what is checked is that a refusal comes back as a refusal a sentence can be
    /// written from, not as an exception from inside the marshaller.
    /// </remarks>
    [Fact]
    public async Task A_layout_the_driver_will_not_take_is_refused()
    {
        var stereoOnly = WasapiLoopbackRecorder.ActiveRenderEndpoints()
            .Where(endpoint => endpoint.Channels == 2)
            .Select(endpoint => endpoint.Id)
            .FirstOrDefault();
        Assert.SkipWhen(stereoOnly is null, "this machine has no stereo-only endpoint to refuse with.");

        var configurator = new WindowsAudioEndpointConfigurator();
        var supported = await configurator.GetSupportedLayoutsAsync(
            stereoOnly!,
            TestContext.Current.CancellationToken);
        Assert.SkipWhen(
            supported.Contains(AudioChannelLayout.Surround71),
            "that endpoint's driver does take 7.1, so refusing it would be the wrong answer.");

        var change = await configurator.SetLayoutAsync(
            stereoOnly!,
            AudioChannelLayout.Surround71,
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.RefusedByDevice, change);
    }

    /// <summary>
    /// An endpoint that is not there answers, rather than throwing from inside the marshaller.
    /// </summary>
    /// <remarks>
    /// <c>Unavailable</c> and not <c>RefusedByDevice</c>, and the difference is the sentence the
    /// interface gets to say: a driver that will not take 7.1 is worth telling somebody about, while
    /// an endpoint that has gone is a different problem with a different fix.
    /// </remarks>
    [Fact]
    public async Task An_endpoint_that_is_not_there_is_unavailable()
    {
        var configurator = new WindowsAudioEndpointConfigurator();

        var change = await configurator.SetLayoutAsync(
            "{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}",
            AudioChannelLayout.Stereo,
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.Unavailable, change);
    }
}
