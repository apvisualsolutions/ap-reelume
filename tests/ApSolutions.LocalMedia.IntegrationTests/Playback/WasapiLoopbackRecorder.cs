// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// What the audio engine actually received, one array per channel. The channel count is the
/// endpoint's rather than the source's, which is the point: it says what left the application, not
/// what the application claimed.
/// </summary>
internal sealed record LoopbackCapture(int ChannelCount, int SampleRate, IReadOnlyList<float[]> Channels)
{
    public double DurationSeconds => Channels.Count == 0 ? 0 : (double)Channels[0].Length / SampleRate;
}

/// <summary>
/// Records the mix Windows sends to one render endpoint, through the WASAPI loopback path.
///
/// This exists because listing an endpoint is not verifying it. The registry and the device catalog
/// both report the layout an endpoint <i>declares</i>, and that label is written by Windows: it says
/// nothing about whether eight channels of audio ever arrived. Loopback captures the engine's mix in
/// the endpoint's own format, so the count and the content are measured instead of read.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WasapiLoopbackRecorder
{
    private const int ShareModeShared = 0;
    private const int StreamFlagsLoopback = 0x00020000;
    private const int ClsCtxAll = 23;
    private const int RenderFlow = 0;
    private const int MultimediaRole = 1;
    private const int DeviceStateActive = 1;
    private const int BufferSilentFlag = 0x2;

    private const ushort WaveFormatPcm = 1;
    private const ushort WaveFormatIeeeFloat = 3;
    private const ushort WaveFormatExtensible = 0xFFFE;

    // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT, the subformat a shared-mode mix almost always carries.
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");
    private static readonly Guid EnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    private static Guid _audioClientId = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static Guid _captureClientId = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

    /// <summary>
    /// Captures the endpoint's mix while <paramref name="whilePlaying"/> runs. The capture is started
    /// before the callback, never after: starting it afterwards loses the opening of the media, and a
    /// channel that only sounds early would read as silent.
    /// </summary>
    public static async Task<LoopbackCapture> RecordAsync(
        string? endpointId,
        TimeSpan duration,
        Func<Task> whilePlaying,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(whilePlaying);
        var enumerator = CreateEnumerator();
        try
        {
            var device = OpenDevice(enumerator, endpointId);
            try
            {
                Marshal.ThrowExceptionForHR(
                    device.Activate(ref _audioClientId, ClsCtxAll, IntPtr.Zero, out var clientPointer));
                var client = (IAudioClient)Marshal.GetObjectForIUnknown(clientPointer);
                _ = Marshal.Release(clientPointer);
                try
                {
                    return await RecordThroughAsync(client, duration, whilePlaying, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _ = Marshal.ReleaseComObject(client);
                }
            }
            finally
            {
                _ = Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            _ = Marshal.ReleaseComObject(enumerator);
        }
    }

    /// <summary>
    /// The active render endpoints as WASAPI itself reports them, with the channel count of the mix
    /// format each one is currently running. Read independently of the registry, so the two can be
    /// compared rather than assumed to agree.
    /// </summary>
    public static IReadOnlyList<(string Id, int Channels, int SampleRate)> ActiveRenderEndpoints()
    {
        var enumerator = CreateEnumerator();
        try
        {
            var results = new List<(string, int, int)>();
            foreach (var device in EnumerateActive(enumerator))
            {
                try
                {
                    Marshal.ThrowExceptionForHR(device.GetId(out var id));
                    Marshal.ThrowExceptionForHR(
                        device.Activate(ref _audioClientId, ClsCtxAll, IntPtr.Zero, out var clientPointer));
                    var client = (IAudioClient)Marshal.GetObjectForIUnknown(clientPointer);
                    _ = Marshal.Release(clientPointer);
                    try
                    {
                        Marshal.ThrowExceptionForHR(client.GetMixFormat(out var format));
                        try
                        {
                            var header = Marshal.PtrToStructure<WaveFormatEx>(format);
                            results.Add((id, header.Channels, header.SamplesPerSecond));
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(format);
                        }
                    }
                    finally
                    {
                        _ = Marshal.ReleaseComObject(client);
                    }
                }
                finally
                {
                    _ = Marshal.ReleaseComObject(device);
                }
            }

            return results;
        }
        finally
        {
            _ = Marshal.ReleaseComObject(enumerator);
        }
    }

    private static async Task<LoopbackCapture> RecordThroughAsync(
        IAudioClient client,
        TimeSpan duration,
        Func<Task> whilePlaying,
        CancellationToken cancellationToken)
    {
        Marshal.ThrowExceptionForHR(client.GetMixFormat(out var formatPointer));
        WaveFormatEx format;
        bool isFloat;
        try
        {
            format = Marshal.PtrToStructure<WaveFormatEx>(formatPointer);
            isFloat = IsFloat(format, formatPointer);

            // A one-second buffer in 100-nanosecond units, so a slow test thread cannot overrun it.
            Marshal.ThrowExceptionForHR(client.Initialize(
                ShareModeShared, StreamFlagsLoopback, 10_000_000, 0, formatPointer, IntPtr.Zero));
        }
        finally
        {
            Marshal.FreeCoTaskMem(formatPointer);
        }

        var channels = format.Channels;
        var bytesPerSample = format.BitsPerSample / 8;
        var accumulated = new List<float>[channels];
        for (var channel = 0; channel < channels; channel++)
        {
            accumulated[channel] = new List<float>(format.SamplesPerSecond * 4);
        }

        Marshal.ThrowExceptionForHR(client.GetService(ref _captureClientId, out var servicePointer));
        var capture = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(servicePointer);
        _ = Marshal.Release(servicePointer);
        try
        {
            Marshal.ThrowExceptionForHR(client.Start());
            var playback = whilePlaying();
            try
            {
                var deadline = DateTime.UtcNow + duration;
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Drain(capture, accumulated, channels, format.BlockAlign, bytesPerSample, isFloat);
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }

                Drain(capture, accumulated, channels, format.BlockAlign, bytesPerSample, isFloat);
            }
            finally
            {
                _ = client.Stop();
                await playback.ConfigureAwait(false);
            }
        }
        finally
        {
            _ = Marshal.ReleaseComObject(capture);
        }

        var buffers = new float[channels][];
        for (var channel = 0; channel < channels; channel++)
        {
            buffers[channel] = [.. accumulated[channel]];
        }

        return new LoopbackCapture(channels, format.SamplesPerSecond, buffers);
    }

    /// <summary>
    /// Empties every packet WASAPI is currently holding. A silent packet is flagged rather than
    /// zero-filled, so its zeroes are written explicitly: dropping them would shorten the recording
    /// and slide every later sample forward in time.
    /// </summary>
    private static void Drain(
        IAudioCaptureClient capture,
        List<float>[] accumulated,
        int channels,
        int blockAlign,
        int bytesPerSample,
        bool isFloat)
    {
        while (true)
        {
            Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out var packetFrames));
            if (packetFrames == 0)
            {
                return;
            }

            Marshal.ThrowExceptionForHR(
                capture.GetBuffer(out var data, out var frames, out var flags, out _, out _));
            try
            {
                if ((flags & BufferSilentFlag) != 0)
                {
                    for (var frame = 0; frame < frames; frame++)
                    {
                        for (var channel = 0; channel < channels; channel++)
                        {
                            accumulated[channel].Add(0f);
                        }
                    }

                    continue;
                }

                var bytes = new byte[frames * blockAlign];
                Marshal.Copy(data, bytes, 0, bytes.Length);
                for (var frame = 0; frame < frames; frame++)
                {
                    for (var channel = 0; channel < channels; channel++)
                    {
                        var offset = (frame * blockAlign) + (channel * bytesPerSample);
                        accumulated[channel].Add(ReadSample(bytes, offset, isFloat, bytesPerSample));
                    }
                }
            }
            finally
            {
                Marshal.ThrowExceptionForHR(capture.ReleaseBuffer(frames));
            }
        }
    }

    private static float ReadSample(byte[] bytes, int offset, bool isFloat, int bytesPerSample)
    {
        if (isFloat)
        {
            return BitConverter.ToSingle(bytes, offset);
        }

        return bytesPerSample switch
        {
            2 => BitConverter.ToInt16(bytes, offset) / 32768f,
            4 => BitConverter.ToInt32(bytes, offset) / 2147483648f,
            _ => throw new NotSupportedException($"The endpoint mixes {bytesPerSample}-byte integer samples."),
        };
    }

    /// <summary>
    /// Whether the mix carries floating-point samples. For an extensible format the subformat GUID is
    /// read from the tail of the structure rather than inferred from the bit depth, because a 32-bit
    /// integer mix and a 32-bit float mix are the same width and would decode to noise if confused.
    /// </summary>
    private static bool IsFloat(WaveFormatEx format, IntPtr formatPointer)
    {
        if (format.FormatTag == WaveFormatIeeeFloat)
        {
            return true;
        }

        if (format.FormatTag == WaveFormatPcm)
        {
            return false;
        }

        if (format.FormatTag != WaveFormatExtensible || format.ExtraSize < 22)
        {
            throw new NotSupportedException($"The endpoint mixes an unrecognised format tag {format.FormatTag}.");
        }

        // WAVEFORMATEXTENSIBLE: an 18-byte WAVEFORMATEX, then a 2-byte sample union, a 4-byte channel
        // mask, and the 16-byte subformat GUID.
        var subFormat = new byte[16];
        Marshal.Copy(formatPointer + 18 + 2 + 4, subFormat, 0, subFormat.Length);
        return new Guid(subFormat) == IeeeFloatSubFormat;
    }

    private static IMMDevice OpenDevice(IMMDeviceEnumerator enumerator, string? endpointId)
    {
        if (endpointId is null)
        {
            Marshal.ThrowExceptionForHR(
                enumerator.GetDefaultAudioEndpoint(RenderFlow, MultimediaRole, out var fallback));
            return fallback ?? throw new InvalidOperationException("Windows reported no default render endpoint.");
        }

        // The catalog stores the bare endpoint GUID the registry keys use, while WASAPI names the same
        // endpoint "{0.0.0.00000000}.{guid}", so a suffix match is the join between the two.
        foreach (var candidate in EnumerateActive(enumerator))
        {
            Marshal.ThrowExceptionForHR(candidate.GetId(out var id));
            if (id.EndsWith(endpointId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            _ = Marshal.ReleaseComObject(candidate);
        }

        throw new InvalidOperationException($"No active render endpoint ends with '{endpointId}'.");
    }

    private static IEnumerable<IMMDevice> EnumerateActive(IMMDeviceEnumerator enumerator)
    {
        Marshal.ThrowExceptionForHR(
            enumerator.EnumAudioEndpoints(RenderFlow, DeviceStateActive, out var collection));
        var devices = (IMMDeviceCollection)Marshal.GetObjectForIUnknown(collection);
        _ = Marshal.Release(collection);
        try
        {
            Marshal.ThrowExceptionForHR(devices.GetCount(out var count));
            for (var index = 0; index < count; index++)
            {
                Marshal.ThrowExceptionForHR(devices.Item(index, out var device));
                yield return device;
            }
        }
        finally
        {
            _ = Marshal.ReleaseComObject(devices);
        }
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        var type = Type.GetTypeFromCLSID(EnumeratorClassId)
            ?? throw new InvalidOperationException("The audio endpoint enumerator is not registered.");
        return Activator.CreateInstance(type) as IMMDeviceEnumerator
            ?? throw new InvalidOperationException("The audio endpoint enumerator refused to activate.");
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WaveFormatEx
    {
        public ushort FormatTag;
        public ushort Channels;
        public int SamplesPerSecond;
        public int AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? device);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        int GetCount(out int count);

        int Item(int index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid interfaceId, int classContext, IntPtr activationParams, out IntPtr instance);

        int OpenPropertyStore(int access, out IntPtr properties);

        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        int GetState(out int state);
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        int Initialize(
            int shareMode,
            int streamFlags,
            long bufferDuration,
            long periodicity,
            IntPtr format,
            IntPtr sessionId);

        int GetBufferSize(out int frames);

        int GetStreamLatency(out long latency);

        int GetCurrentPadding(out int frames);

        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);

        int GetMixFormat(out IntPtr format);

        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

        int Start();

        int Stop();

        int Reset();

        int SetEventHandle(IntPtr handle);

        int GetService(ref Guid interfaceId, out IntPtr instance);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        int GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePosition, out long counterPosition);

        int ReleaseBuffer(int frames);

        int GetNextPacketSize(out int frames);
    }
}
