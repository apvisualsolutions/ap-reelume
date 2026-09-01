// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// Writes the channel layout an endpoint carries, through the same setting the sound panel writes.
/// </summary>
/// <remarks>
/// <b>The interface this uses is not documented by Microsoft</b>, and that is stated here rather than
/// discovered later. <c>IPolicyConfig</c> is what the sound panel itself calls; every open-source
/// implementation of "change the default format" uses it, because there is no documented equivalent.
/// The consequence accepted with it: a Windows update could change its shape, and this would stop
/// working. <c>WindowsAudioEndpointConfiguratorTests</c> exists to say so out loud on the day it
/// happens, rather than leaving a control that silently does nothing.
/// <para>
/// <b>The format is read before it is written.</b> The endpoint's own format carries a bit depth and
/// a sample rate its driver already accepts, so only the channel count and the speaker mask move.
/// Building one from scratch was measured on 2026-09-02 and answered
/// <c>AUDCLNT_E_UNSUPPORTED_FORMAT</c> — the driver took 24 bits and the guess offered 32.
/// </para>
/// <para>
/// It needs no elevation. Measured on the same day from an ordinary process:
/// <c>administrator=False</c>, and the write returned <c>S_OK</c>.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsAudioEndpointConfigurator : IAudioEndpointConfigurator
{
    private static readonly Guid PolicyConfigClass = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");

    /// <summary>True when the undocumented interface can be created on this machine.</summary>
    public bool IsAvailable => TryCreate() is not null;

    /// <inheritdoc />
    public Task<IReadOnlyList<AudioChannelLayout>> GetSupportedLayoutsAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        var client = TryActivateClient(deviceId);
        if (client is null)
        {
            // Nothing was asked, so nothing is claimed: stereo is what every endpoint carries and
            // the only layout that can be offered without an answer.
            return Task.FromResult<IReadOnlyList<AudioChannelLayout>>([AudioChannelLayout.Stereo]);
        }

        var accepted = new List<AudioChannelLayout>();
        foreach (var layout in (AudioChannelLayout[])[
            AudioChannelLayout.Stereo,
            AudioChannelLayout.Surround51,
            AudioChannelLayout.Surround71])
        {
            if (Accepts(client, layout))
            {
                accepted.Add(layout);
            }
        }

        // Stereo always, because an endpoint that refuses two channels is one nothing could play to
        // and the interface would be left with no choice at all to show.
        if (accepted.Count == 0)
        {
            accepted.Add(AudioChannelLayout.Stereo);
        }

        return Task.FromResult<IReadOnlyList<AudioChannelLayout>>(accepted);
    }

    /// <summary>Whether the driver takes this layout at any depth it is likely to accept.</summary>
    /// <remarks>
    /// <b>Exclusive mode</b>, which is the whole point of asking. In shared mode WASAPI answers about
    /// the mixer, and the mixer takes exactly the format the endpoint is set to — so every endpoint
    /// would report the one layout it already has, and the question would answer itself.
    /// <para>
    /// <b>PCM, and not the mix format's own subtype.</b> Measured on 2026-09-02: copying the mix
    /// format and changing only its channel count made an eight-channel endpoint report stereo alone,
    /// because a shared mixer runs in IEEE float and drivers in exclusive mode want integer PCM. The
    /// depths tried are the two that endpoint accepted when it was asked directly — 24 bits and 16 —
    /// and the sample rate is left at whatever it is already running.
    /// </para>
    /// </remarks>
    private static bool Accepts(IAudioClient client, AudioChannelLayout layout)
    {
        if (client.GetMixFormat(out var mix) != 0 || mix == nint.Zero)
        {
            return layout == AudioChannelLayout.Stereo;
        }

        int sampleRate;
        try
        {
            sampleRate = Marshal.ReadInt32(mix, SampleRateOffset);
        }
        finally
        {
            Marshal.FreeCoTaskMem(mix);
        }

        return AcceptsAt(client, layout, sampleRate, 24) || AcceptsAt(client, layout, sampleRate, 16);
    }

    private static bool AcceptsAt(IAudioClient client, AudioChannelLayout layout, int sampleRate, short bits)
    {
        var candidate = Marshal.AllocHGlobal(ExtensibleSize);
        try
        {
            Marshal.Copy(BuildPcm(layout, sampleRate, bits), 0, candidate, ExtensibleSize);
            return client.IsFormatSupported(ExclusiveMode, candidate, out var closest) switch
            {
                0 => true,
                _ => Release(closest),
            };
        }
        finally
        {
            Marshal.FreeHGlobal(candidate);
        }
    }

    /// <summary>A refusal may still hand over a suggestion, and it belongs to the caller to free.</summary>
    private static bool Release(nint closest)
    {
        if (closest != nint.Zero)
        {
            Marshal.FreeCoTaskMem(closest);
        }

        return false;
    }

    /// <summary>An integer-PCM WAVEFORMATEXTENSIBLE, built rather than copied.</summary>
    private static byte[] BuildPcm(AudioChannelLayout layout, int sampleRate, short bits)
    {
        var format = new byte[ExtensibleSize];
        var channels = (short)layout;
        var blockAlign = (short)(channels * bits / 8);

        BitConverter.GetBytes(unchecked((short)0xFFFE)).CopyTo(format, 0);
        BitConverter.GetBytes(channels).CopyTo(format, ChannelsOffset);
        BitConverter.GetBytes(sampleRate).CopyTo(format, SampleRateOffset);
        BitConverter.GetBytes(sampleRate * blockAlign).CopyTo(format, BytesPerSecondOffset);
        BitConverter.GetBytes(blockAlign).CopyTo(format, BlockAlignOffset);
        BitConverter.GetBytes(bits).CopyTo(format, BitsOffset);
        BitConverter.GetBytes((short)ExtensibleExtraBytes).CopyTo(format, CbSizeOffset);
        BitConverter.GetBytes(bits).CopyTo(format, ValidBitsOffset);
        BitConverter.GetBytes(MaskFor(layout)).CopyTo(format, ChannelMaskOffset);
        PcmSubFormat.ToByteArray().CopyTo(format, SubFormatOffset);
        return format;
    }

    private static IAudioClient? TryActivateClient(string deviceId)
    {
        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(DeviceEnumeratorClass);
            if (enumeratorType is null
                || Activator.CreateInstance(enumeratorType) is not IMMDeviceEnumerator enumerator)
            {
                return null;
            }

            if (enumerator.GetDevice(deviceId, out var device) != 0 || device is null)
            {
                return null;
            }

            var clientId = AudioClientInterface;
            return device.Activate(ref clientId, LocalServerContext, nint.Zero, out var instance) == 0
                && instance != nint.Zero
                    ? Marshal.GetObjectForIUnknown(instance) as IAudioClient
                    : null;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    public async Task<AudioEndpointChange> SetLayoutAsync(
        string deviceId,
        AudioChannelLayout layout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        // The driver decides, not the catalogue. The catalogue reads the layout the endpoint is set
        // to, so asking it here would refuse every raise: an endpoint reduced to stereo once would
        // report stereo alone and never be allowed back up. That is the one-way door this control
        // must not be.
        var supported = await GetSupportedLayoutsAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (!supported.Contains(layout))
        {
            return AudioEndpointChange.RefusedByDevice;
        }

        var configurator = TryCreate();
        if (configurator is null)
        {
            return AudioEndpointChange.Unavailable;
        }

        return Write(configurator, deviceId, layout);
    }

    private static AudioEndpointChange Write(IPolicyConfig policy, string deviceId, AudioChannelLayout layout)
    {
        if (policy.GetDeviceFormat(deviceId, false, out var current) != 0 || current == nint.Zero)
        {
            return AudioEndpointChange.Unavailable;
        }

        try
        {
            var extraBytes = Marshal.ReadInt16(current, ChannelMaskOffset - 4);
            var length = WaveFormatExSize + extraBytes;
            var format = new byte[Math.Max(length, ExtensibleSize)];
            Marshal.Copy(current, format, 0, length);

            var channels = (short)layout;
            if (BitConverter.ToInt16(format, ChannelsOffset) == channels)
            {
                return AudioEndpointChange.AlreadySet;
            }

            var bits = BitConverter.ToInt16(format, BitsOffset);
            var blockAlign = (short)(channels * bits / 8);
            var sampleRate = BitConverter.ToInt32(format, SampleRateOffset);

            BitConverter.GetBytes(channels).CopyTo(format, ChannelsOffset);
            BitConverter.GetBytes(sampleRate * blockAlign).CopyTo(format, BytesPerSecondOffset);
            BitConverter.GetBytes(blockAlign).CopyTo(format, BlockAlignOffset);

            // The mask only exists on the extensible form, and writing it into a plain WAVEFORMATEX
            // would be writing past the structure the driver handed over.
            if (extraBytes >= ExtensibleExtraBytes)
            {
                BitConverter.GetBytes(MaskFor(layout)).CopyTo(format, ChannelMaskOffset);
            }

            var buffer = Marshal.AllocHGlobal(format.Length);
            try
            {
                Marshal.Copy(format, 0, buffer, format.Length);

                // Both arguments, because the panel writes both: the endpoint format is what the
                // driver is opened with and the mix format is what the shared-mode mixer produces.
                return policy.SetDeviceFormat(deviceId, buffer, buffer) == 0
                    ? AudioEndpointChange.Applied
                    : AudioEndpointChange.RefusedByDevice;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(current);
        }
    }

    /// <summary>The speaker mask Windows writes for each layout, which is what the panel writes.</summary>
    private static uint MaskFor(AudioChannelLayout layout) => layout switch
    {
        AudioChannelLayout.Surround71 => 0x63F,
        AudioChannelLayout.Surround51 => 0x3F,
        _ => 0x3,
    };

    private static IPolicyConfig? TryCreate()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(PolicyConfigClass);
            return type is null ? null : Activator.CreateInstance(type) as IPolicyConfig;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            // The class exists and does not answer to this interface, which is what a Windows that
            // changed its shape looks like from here.
            return null;
        }
    }

    // WAVEFORMATEX, field by field, because the structure is read from the driver rather than
    // declared: a struct with the wrong packing would silently misread a working format.
    private const int ChannelsOffset = 2;
    private const int SampleRateOffset = 4;
    private const int BytesPerSecondOffset = 8;
    private const int BlockAlignOffset = 12;
    private const int BitsOffset = 14;
    private const int ChannelMaskOffset = 20;
    private const int WaveFormatExSize = 18;
    private const int ExtensibleExtraBytes = 22;
    private const int CbSizeOffset = 16;
    private const int ValidBitsOffset = 18;
    private const int SubFormatOffset = 24;
    private const int ExtensibleSize = 40;
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    private const int ExclusiveMode = 1;
    private const int LocalServerContext = 0x17;
    private static readonly Guid DeviceEnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid AudioClientInterface = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

    /// <summary>
    /// The four methods of the undocumented interface that this needs, in vtable order.
    /// </summary>
    /// <remarks>
    /// The order is the whole contract: COM finds a method by its slot, so a method declared out of
    /// place calls a different one. These four are the first four, and the ones after them are left
    /// out on purpose — declaring a method this does not call is a slot that can be wrong without
    /// anything noticing.
    /// <para>
    /// <c>PreserveSig</c> on all of them, so a refusal arrives as an HRESULT to read rather than as
    /// an exception to catch. Measured: without it, a format the driver would not take arrived as a
    /// <c>COMException 0x88890008</c> from inside the marshaller.
    /// </para>
    /// </remarks>
    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig]
        int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out nint format);

        [PreserveSig]
        int GetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [MarshalAs(UnmanagedType.Bool)] bool isDefault,
            out nint format);

        [PreserveSig]
        int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        [PreserveSig]
        int SetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            nint endpointFormat,
            nint mixFormat);
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice? device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, int classContext, nint activationParams, out nint instance);
    }

    /// <summary>
    /// Two of the audio client's methods, and no more: every method declared is a vtable slot that
    /// has to be right, and the ones after these are never called from here.
    /// </summary>
    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, nint format, nint sessionId);

        [PreserveSig]
        int GetBufferSize(out uint frames);

        [PreserveSig]
        int GetStreamLatency(out long latency);

        [PreserveSig]
        int GetCurrentPadding(out uint frames);

        [PreserveSig]
        int IsFormatSupported(int shareMode, nint format, out nint closestMatch);

        [PreserveSig]
        int GetMixFormat(out nint format);
    }
}
