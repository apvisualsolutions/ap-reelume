using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Playback;
using Microsoft.Win32;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// Lists the render endpoints Windows currently has active, with the channel layout each one's mix
/// format actually declares. Nothing is assumed: an endpoint that mixes in stereo is reported as
/// stereo even if its hardware could do more, because that is what playback would get today.
/// </summary>
public sealed class WindowsAudioDeviceCatalog : IAudioDeviceCatalog
{
    private const string RenderRoot =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";

    private const string FriendlyNameProperty = "{a45c254e-df1c-4efd-8020-67d146a850e0},2";
    private const string DeviceFormatProperty = "{f19f064d-082c-4e27-bc73-6882a1bb8e4c},0";
    private const int ActiveState = 1;

    // The registry blob is an eight-byte property header followed by a WAVEFORMATEXTENSIBLE, whose
    // channel count is the second 16-bit field.
    private const int FormatHeaderLength = 8;
    private const int ChannelCountOffset = FormatHeaderLength + 2;

    public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var defaultId = TryGetDefaultEndpointId();
        var devices = new List<AudioOutputDevice>();

        using var root = Registry.LocalMachine.OpenSubKey(RenderRoot);
        if (root is null)
        {
            return Task.FromResult<IReadOnlyList<AudioOutputDevice>>(devices);
        }

        foreach (var endpointId in root.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var endpoint = root.OpenSubKey(endpointId);
            if (endpoint is null || endpoint.GetValue("DeviceState") is not int state || state != ActiveState)
            {
                continue;
            }

            using var properties = endpoint.OpenSubKey("Properties");
            var name = properties?.GetValue(FriendlyNameProperty) as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var channels = ReadChannelCount(properties?.GetValue(DeviceFormatProperty) as byte[]);
            devices.Add(new AudioOutputDevice(
                endpointId,
                name,
                LayoutsFor(channels),
                IsDefault: defaultId is not null
                    && endpointId.EndsWith(defaultId, StringComparison.OrdinalIgnoreCase),
                IsAvailable: true));
        }

        return Task.FromResult<IReadOnlyList<AudioOutputDevice>>(devices);
    }

    /// <summary>Every layout the endpoint's current mix format can carry, largest included.</summary>
    private static IReadOnlyList<AudioChannelLayout> LayoutsFor(int channels) => channels switch
    {
        >= 8 => [AudioChannelLayout.Stereo, AudioChannelLayout.Surround51, AudioChannelLayout.Surround71],
        >= 6 => [AudioChannelLayout.Stereo, AudioChannelLayout.Surround51],
        _ => [AudioChannelLayout.Stereo],
    };

    private static int ReadChannelCount(byte[]? deviceFormat) =>
        deviceFormat is { Length: >= ChannelCountOffset + 2 }
            ? BitConverter.ToUInt16(deviceFormat, ChannelCountOffset)
            : 2;

    /// <summary>The identifier of the endpoint Windows plays through by default, when it can be read.</summary>
    private static string? TryGetDefaultEndpointId()
    {
        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            if (enumeratorType is null || Activator.CreateInstance(enumeratorType) is not IMMDeviceEnumerator enumerator)
            {
                return null;
            }

            try
            {
                // dataFlow 0 = render, role 1 = multimedia.
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out var device) != 0 || device is null)
                {
                    return null;
                }

                try
                {
                    return device.GetId(out var id) == 0 ? id : null;
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
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid interfaceId, int classContext, nint activationParams, out nint instance);

        int OpenPropertyStore(int access, out nint properties);

        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        int GetState(out int state);
    }
}
