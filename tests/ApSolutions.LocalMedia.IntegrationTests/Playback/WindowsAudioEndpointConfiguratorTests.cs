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
        // answered about the wrong endpoint — every count below has an entry in the enumeration.
        var carried = endpoint.Channels switch
        {
            >= 8 => AudioChannelLayout.Surround71,
            >= 6 => AudioChannelLayout.Surround51,
            _ => AudioChannelLayout.Stereo,
        };
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
