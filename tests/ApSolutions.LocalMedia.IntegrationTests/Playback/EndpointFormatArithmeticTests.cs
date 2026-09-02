// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// The bytes the configurator assembles, over a store and a probe that are not a sound card.
/// </summary>
/// <remarks>
/// <b>None of this could be run without one until 2026-09-02.</b> The class was COM all the way
/// down, so on a hosted runner it measured 23 % of its lines and the new-file gate refused it —
/// rightly, because the arithmetic here decides how many channels come out of the speakers and
/// nobody had ever executed it anywhere but on a developer's machine.
/// <para>
/// What the seam does not cover is stated rather than implied: that Windows accepts these bytes is
/// the business of <see cref="WindowsAudioEndpointConfiguratorTests"/>, which needs the hardware and
/// skips without it. This file checks that the right bytes are built and the right answers given.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class EndpointFormatArithmeticTests
{
    private const string Endpoint = "{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}";

    [Theory]
    [InlineData(AudioChannelLayout.Stereo, 2, 0x3u)]
    [InlineData(AudioChannelLayout.Surround51, 6, 0x3Fu)]
    [InlineData(AudioChannelLayout.Surround71, 8, 0x63Fu)]
    public async Task The_written_format_carries_the_channel_count_and_the_speaker_mask(
        AudioChannelLayout layout,
        short channels,
        uint mask)
    {
        // The endpoint starts on a layout that is not the one asked for, in every case: starting on
        // the same one answers AlreadySet and writes nothing, which is correct and would leave this
        // checking an empty buffer.
        var store = new RecordingStore(Format(channels == 2 ? (short)8 : (short)2, 48000, 24));
        var configurator = new WindowsAudioEndpointConfigurator(() => store, _ => new AcceptingProbe(48000));

        var change = await configurator.SetLayoutAsync(Endpoint, layout, TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.Applied, change);
        Assert.NotNull(store.Written);
        Assert.Equal(channels, BitConverter.ToInt16(store.Written!, 2));
        Assert.Equal(mask, BitConverter.ToUInt32(store.Written!, 20));

        // The depth and the rate are the endpoint's own, untouched: they are what its driver already
        // accepts, and guessing them was measured refusing with AUDCLNT_E_UNSUPPORTED_FORMAT.
        Assert.Equal(24, BitConverter.ToInt16(store.Written!, 14));
        Assert.Equal(48000, BitConverter.ToInt32(store.Written!, 4));

        // Block align and bytes per second follow from the two, and a reader that got them wrong
        // would produce a format the driver takes and plays at the wrong speed.
        var blockAlign = (short)(channels * 24 / 8);
        Assert.Equal(blockAlign, BitConverter.ToInt16(store.Written!, 12));
        Assert.Equal(48000 * blockAlign, BitConverter.ToInt32(store.Written!, 8));
    }

    [Fact]
    public async Task An_endpoint_already_carrying_the_layout_is_not_written_again()
    {
        var store = new RecordingStore(Format(8, 48000, 24));
        var configurator = new WindowsAudioEndpointConfigurator(() => store, _ => new AcceptingProbe(48000));

        var change = await configurator.SetLayoutAsync(
            Endpoint,
            AudioChannelLayout.Surround71,
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.AlreadySet, change);
        Assert.Null(store.Written);
    }

    [Fact]
    public async Task A_driver_that_refuses_the_layout_is_never_written_to()
    {
        var store = new RecordingStore(Format(2, 48000, 24));
        var configurator = new WindowsAudioEndpointConfigurator(() => store, _ => new StereoOnlyProbe(48000));

        var change = await configurator.SetLayoutAsync(
            Endpoint,
            AudioChannelLayout.Surround71,
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.RefusedByDevice, change);
        Assert.Null(store.Written);
    }

    [Fact]
    public async Task A_driver_that_takes_the_layout_and_a_write_it_refuses_are_told_apart()
    {
        var store = new RecordingStore(Format(2, 48000, 24)) { WriteResult = unchecked((int)0x88890008) };
        var configurator = new WindowsAudioEndpointConfigurator(() => store, _ => new AcceptingProbe(48000));

        var change = await configurator.SetLayoutAsync(
            Endpoint,
            AudioChannelLayout.Surround51,
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.RefusedByDevice, change);
        Assert.NotNull(store.Written);
    }

    [Fact]
    public async Task A_store_that_cannot_read_the_format_is_unavailable_rather_than_a_guess()
    {
        var store = new RecordingStore(null);
        var configurator = new WindowsAudioEndpointConfigurator(() => store, _ => new AcceptingProbe(48000));

        var change = await configurator.SetLayoutAsync(
            Endpoint,
            AudioChannelLayout.Stereo,
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.Unavailable, change);
    }

    [Fact]
    public async Task With_no_store_at_all_nothing_is_claimed()
    {
        var configurator = new WindowsAudioEndpointConfigurator(() => null, _ => new AcceptingProbe(48000));

        Assert.False(configurator.IsAvailable);
        Assert.Equal(
            AudioEndpointChange.Unavailable,
            await configurator.SetLayoutAsync(Endpoint, AudioChannelLayout.Stereo, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// What is on offer is what the driver takes, at either depth it is asked about.
    /// </summary>
    /// <remarks>
    /// 24 bits first and 16 second, because those are the two the endpoint measured on 2026-09-02
    /// accepted. A driver that takes only the second still offers the layout, which is the half a
    /// single-depth question would answer wrongly.
    /// </remarks>
    [Fact]
    public async Task The_layouts_on_offer_come_from_the_driver_at_either_depth()
    {
        var configurator = new WindowsAudioEndpointConfigurator(
            () => new RecordingStore(Format(2, 44100, 16)),
            _ => new SixteenBitOnlyProbe(44100));

        var supported = await configurator.GetSupportedLayoutsAsync(Endpoint, TestContext.Current.CancellationToken);

        Assert.Contains(AudioChannelLayout.Stereo, supported);
        Assert.Contains(AudioChannelLayout.Surround71, supported);
    }

    [Fact]
    public async Task A_probe_that_answers_nothing_leaves_stereo_alone_on_offer()
    {
        var configurator = new WindowsAudioEndpointConfigurator(
            () => new RecordingStore(Format(2, 48000, 24)),
            _ => null);

        var supported = await configurator.GetSupportedLayoutsAsync(Endpoint, TestContext.Current.CancellationToken);

        Assert.Equal([AudioChannelLayout.Stereo], supported);
    }

    /// <summary>
    /// A probe whose mix format cannot be read still offers stereo, and offers nothing else.
    /// </summary>
    [Fact]
    public async Task A_probe_with_no_mix_format_offers_stereo_and_nothing_more()
    {
        var configurator = new WindowsAudioEndpointConfigurator(
            () => new RecordingStore(Format(2, 48000, 24)),
            _ => new BrokenProbe());

        var supported = await configurator.GetSupportedLayoutsAsync(Endpoint, TestContext.Current.CancellationToken);

        Assert.Equal([AudioChannelLayout.Stereo], supported);
    }

    [Fact]
    public async Task A_driver_that_refuses_even_stereo_is_still_offered_it()
    {
        var configurator = new WindowsAudioEndpointConfigurator(
            () => new RecordingStore(Format(2, 48000, 24)),
            _ => new RefusingProbe(48000));

        var supported = await configurator.GetSupportedLayoutsAsync(Endpoint, TestContext.Current.CancellationToken);

        // Nothing could play to an endpoint that takes no stereo, and an interface with no choice at
        // all to show is worse than one showing the only one there ever is.
        Assert.Equal([AudioChannelLayout.Stereo], supported);
    }

    [Fact]
    public async Task An_empty_endpoint_identifier_is_refused_before_anything_is_opened()
    {
        var configurator = new WindowsAudioEndpointConfigurator(
            () => throw new InvalidOperationException("the store was opened for an empty id"),
            _ => throw new InvalidOperationException("the probe was opened for an empty id"));

        // The factories throw rather than answer, so a guard that ran late would surface as that
        // exception instead of the argument one — which is the difference between checking the guard
        // and checking that something threw.
        await Assert.ThrowsAsync<ArgumentException>(
            () => configurator.SetLayoutAsync(" ", AudioChannelLayout.Stereo, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(
            () => configurator.GetSupportedLayoutsAsync(" ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_configurator_refuses_to_exist_without_its_two_halves()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WindowsAudioEndpointConfigurator(null!, _ => null));
        Assert.Throws<ArgumentNullException>(
            () => new WindowsAudioEndpointConfigurator(() => null, null!));
    }

    /// <summary>A WAVEFORMATEXTENSIBLE the way a driver hands one over.</summary>
    private static byte[] Format(short channels, int sampleRate, short bits)
    {
        var format = new byte[40];
        var blockAlign = (short)(channels * bits / 8);
        BitConverter.GetBytes(unchecked((short)0xFFFE)).CopyTo(format, 0);
        BitConverter.GetBytes(channels).CopyTo(format, 2);
        BitConverter.GetBytes(sampleRate).CopyTo(format, 4);
        BitConverter.GetBytes(sampleRate * blockAlign).CopyTo(format, 8);
        BitConverter.GetBytes(blockAlign).CopyTo(format, 12);
        BitConverter.GetBytes(bits).CopyTo(format, 14);
        BitConverter.GetBytes((short)22).CopyTo(format, 16);
        BitConverter.GetBytes(bits).CopyTo(format, 18);
        BitConverter.GetBytes(channels == 8 ? 0x63Fu : channels == 6 ? 0x3Fu : 0x3u).CopyTo(format, 20);
        return format;
    }

    private static nint Allocate(byte[] format)
    {
        var buffer = Marshal.AllocCoTaskMem(format.Length);
        Marshal.Copy(format, 0, buffer, format.Length);
        return buffer;
    }

    private sealed class RecordingStore(byte[]? current) : IEndpointFormatStore
    {
        public byte[]? Written { get; private set; }

        public int WriteResult { get; init; }

        public int GetDeviceFormat(string deviceId, out nint format)
        {
            if (current is null)
            {
                format = nint.Zero;
                return unchecked((int)0x80004005);
            }

            format = Allocate(current);
            return 0;
        }

        public int SetDeviceFormat(string deviceId, nint endpointFormat, nint mixFormat)
        {
            var written = new byte[40];
            Marshal.Copy(endpointFormat, written, 0, written.Length);
            Written = written;
            return WriteResult;
        }
    }

    private sealed class AcceptingProbe(int sampleRate) : IEndpointFormatProbe
    {
        public int GetMixFormat(out nint format)
        {
            format = Allocate(Format(2, sampleRate, 24));
            return 0;
        }

        public int IsFormatSupported(int shareMode, nint format, out nint closestMatch)
        {
            closestMatch = nint.Zero;
            return 0;
        }
    }

    private sealed class StereoOnlyProbe(int sampleRate) : IEndpointFormatProbe
    {
        public int GetMixFormat(out nint format)
        {
            format = Allocate(Format(2, sampleRate, 24));
            return 0;
        }

        public int IsFormatSupported(int shareMode, nint format, out nint closestMatch)
        {
            closestMatch = nint.Zero;
            var channels = Marshal.ReadInt16(format, 2);
            return channels == 2 ? 0 : unchecked((int)0x88890008);
        }
    }

    /// <summary>Takes 16 bits and nothing else, and hands back a suggestion with every refusal.</summary>
    private sealed class SixteenBitOnlyProbe(int sampleRate) : IEndpointFormatProbe
    {
        public int GetMixFormat(out nint format)
        {
            format = Allocate(Format(2, sampleRate, 16));
            return 0;
        }

        public int IsFormatSupported(int shareMode, nint format, out nint closestMatch)
        {
            if (Marshal.ReadInt16(format, 14) == 16)
            {
                closestMatch = nint.Zero;
                return 0;
            }

            // The refusal that carries a closest match, which the caller has to free — the branch a
            // probe that always answered null would leave unrun.
            closestMatch = Allocate(Format(2, sampleRate, 16));
            return unchecked((int)0x88890008);
        }
    }

    private sealed class RefusingProbe(int sampleRate) : IEndpointFormatProbe
    {
        public int GetMixFormat(out nint format)
        {
            format = Allocate(Format(2, sampleRate, 24));
            return 0;
        }

        public int IsFormatSupported(int shareMode, nint format, out nint closestMatch)
        {
            closestMatch = nint.Zero;
            return unchecked((int)0x88890008);
        }
    }

    private sealed class BrokenProbe : IEndpointFormatProbe
    {
        public int GetMixFormat(out nint format)
        {
            format = nint.Zero;
            return unchecked((int)0x80004005);
        }

        public int IsFormatSupported(int shareMode, nint format, out nint closestMatch)
        {
            closestMatch = nint.Zero;
            return unchecked((int)0x88890008);
        }
    }
}
