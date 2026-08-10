// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// Output selection uses the stable endpoint identifier, falls back to the default when the stored
/// device is gone, and reduces a layout the endpoint cannot take instead of claiming it.
/// </summary>
public sealed class AudioOutputPolicyTests
{
    private static readonly AudioOutputDevice Speakers = new(
        "endpoint-speakers",
        "Altavoces",
        [AudioChannelLayout.Stereo, AudioChannelLayout.Surround51],
        IsDefault: true,
        IsAvailable: true);

    private static readonly AudioOutputDevice Headset = new(
        "endpoint-headset",
        "Auriculares",
        [AudioChannelLayout.Stereo],
        IsDefault: false,
        IsAvailable: true);

    private static readonly AudioOutputDevice Receiver = new(
        "endpoint-receiver",
        "Receptor HDMI",
        [AudioChannelLayout.Stereo, AudioChannelLayout.Surround51, AudioChannelLayout.Surround71],
        IsDefault: false,
        IsAvailable: true);

    [Fact]
    public void The_stored_identifier_wins_while_its_device_is_present()
    {
        var selection = AudioOutputPolicy.Resolve(
            [Speakers, Headset, Receiver],
            "endpoint-headset",
            AudioChannelLayout.Stereo);

        Assert.NotNull(selection);
        Assert.Equal(Headset, selection!.Device);
        Assert.False(selection.FellBackToDefaultDevice);
        Assert.Equal(AudioChannelLayout.Stereo, selection.Layout);
        Assert.False(selection.LayoutWasDegraded);
    }

    [Fact]
    public void A_disconnected_device_falls_back_to_the_default_and_says_so()
    {
        var selection = AudioOutputPolicy.Resolve(
            [Speakers, Headset with { IsAvailable = false }],
            "endpoint-headset",
            AudioChannelLayout.Stereo);

        Assert.Equal(Speakers, selection!.Device);
        Assert.True(selection.FellBackToDefaultDevice);
    }

    [Fact]
    public void Without_a_stored_device_the_default_answers_and_then_the_first_available()
    {
        var withDefault = AudioOutputPolicy.Resolve([Headset, Speakers], null, AudioChannelLayout.Stereo);
        Assert.Equal(Speakers, withDefault!.Device);
        Assert.False(withDefault.FellBackToDefaultDevice);

        var withoutDefault = AudioOutputPolicy.Resolve(
            [Headset, Receiver],
            null,
            AudioChannelLayout.Stereo);
        Assert.Equal(Headset, withoutDefault!.Device);
    }

    [Fact]
    public void With_no_available_output_the_policy_returns_nothing_rather_than_guessing()
    {
        Assert.Null(AudioOutputPolicy.Resolve([], null, AudioChannelLayout.Stereo));
        Assert.Null(AudioOutputPolicy.Resolve(
            [Speakers with { IsAvailable = false }],
            "endpoint-speakers",
            AudioChannelLayout.Stereo));
        Assert.Throws<ArgumentNullException>(() =>
            AudioOutputPolicy.Resolve(null!, null, AudioChannelLayout.Stereo));
    }

    [Theory]
    [InlineData(AudioChannelLayout.Stereo, AudioChannelLayout.Stereo)]
    [InlineData(AudioChannelLayout.Surround51, AudioChannelLayout.Surround51)]
    [InlineData(AudioChannelLayout.Surround71, AudioChannelLayout.Surround51)]
    public void A_layout_the_endpoint_cannot_take_is_reduced_to_the_largest_it_can(
        AudioChannelLayout desired,
        AudioChannelLayout expected) =>
        Assert.Equal(expected, AudioOutputPolicy.ResolveLayout(Speakers, desired));

    [Fact]
    public void A_reduced_layout_is_reported_with_what_was_asked_for()
    {
        var selection = AudioOutputPolicy.Resolve(
            [Headset with { IsDefault = true }],
            "endpoint-headset",
            AudioChannelLayout.Surround71);

        Assert.Equal(AudioChannelLayout.Stereo, selection!.Layout);
        Assert.True(selection.LayoutWasDegraded);
        Assert.Equal(AudioChannelLayout.Surround71, selection.DegradedFrom);
    }

    [Fact]
    public void An_endpoint_that_lists_nothing_still_gets_stereo()
    {
        var bare = Speakers with { SupportedLayouts = [] };

        Assert.Equal(AudioChannelLayout.Stereo, AudioOutputPolicy.ResolveLayout(bare, AudioChannelLayout.Surround71));
        Assert.Throws<ArgumentNullException>(() =>
            AudioOutputPolicy.ResolveLayout(null!, AudioChannelLayout.Stereo));
    }

    [Fact]
    public void Passthrough_is_never_offered_and_the_layouts_map_to_their_channel_counts()
    {
        Assert.False(AudioOutputPolicy.SupportsBitstreamPassthrough);
        Assert.Equal(
            [AudioChannelLayout.Surround71, AudioChannelLayout.Surround51, AudioChannelLayout.Stereo],
            AudioOutputPolicy.SelectableLayouts);
        Assert.Equal(2, (int)AudioChannelLayout.Stereo);
        Assert.Equal(6, (int)AudioChannelLayout.Surround51);
        Assert.Equal(8, (int)AudioChannelLayout.Surround71);
        Assert.DoesNotContain(
            Enum.GetNames<AudioChannelLayout>(),
            name => name.Contains("Dolby", StringComparison.OrdinalIgnoreCase)
                || name.Contains("DTS", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Bitstream", StringComparison.OrdinalIgnoreCase));
    }
}
