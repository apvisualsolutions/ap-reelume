// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>Speaker layouts the application can send. Bitstream passthrough is not one of them.</summary>
public enum AudioChannelLayout
{
    Stereo = 2,
    Surround51 = 6,
    Surround71 = 8,
}

/// <summary>
/// One output endpoint. The identifier is the stable one the operating system assigns, so a stored
/// preference survives a restart and a reconnection.
/// </summary>
public sealed record AudioOutputDevice(
    string Id,
    string Name,
    IReadOnlyList<AudioChannelLayout> SupportedLayouts,
    bool IsDefault,
    bool IsAvailable);

/// <summary>What the session will use, and whether the layout had to be reduced to get there.</summary>
public sealed record AudioOutputSelection(
    AudioOutputDevice Device,
    AudioChannelLayout Layout,
    bool FellBackToDefaultDevice,
    AudioChannelLayout? DegradedFrom)
{
    public bool LayoutWasDegraded => DegradedFrom is not null;
}

/// <summary>
/// Resolves the output from the stored preference and what the machine actually offers. A missing
/// device falls back to the default rather than failing, and a layout the endpoint cannot take is
/// reduced and reported instead of being claimed.
/// </summary>
public static class AudioOutputPolicy
{
    /// <summary>Dolby and DTS bitstream passthrough is out of scope for this release.</summary>
    public static bool SupportsBitstreamPassthrough => false;

    /// <summary>Layouts the interface may offer, largest first for degradation.</summary>
    public static IReadOnlyList<AudioChannelLayout> SelectableLayouts { get; } =
        [AudioChannelLayout.Surround71, AudioChannelLayout.Surround51, AudioChannelLayout.Stereo];

    public static AudioOutputSelection? Resolve(
        IReadOnlyList<AudioOutputDevice> devices,
        string? preferredDeviceId,
        AudioChannelLayout desiredLayout)
    {
        ArgumentNullException.ThrowIfNull(devices);
        var available = devices.Where(device => device.IsAvailable).ToArray();
        if (available.Length == 0)
        {
            return null;
        }

        var preferred = preferredDeviceId is null
            ? null
            : available.FirstOrDefault(device => device.Id.Equals(preferredDeviceId, StringComparison.Ordinal));
        var device = preferred
            ?? available.FirstOrDefault(candidate => candidate.IsDefault)
            ?? available[0];

        var layout = ResolveLayout(device, desiredLayout);
        return new AudioOutputSelection(
            device,
            layout,
            FellBackToDefaultDevice: preferredDeviceId is not null && preferred is null,
            DegradedFrom: layout == desiredLayout ? null : desiredLayout);
    }

    /// <summary>The largest layout the endpoint accepts that does not exceed what was asked for.</summary>
    public static AudioChannelLayout ResolveLayout(AudioOutputDevice device, AudioChannelLayout desired)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device.SupportedLayouts.Contains(desired))
        {
            return desired;
        }

        var smaller = device.SupportedLayouts
            .Where(layout => (int)layout < (int)desired)
            .OrderByDescending(layout => (int)layout)
            .ToArray();
        return smaller.Length > 0 ? smaller[0] : AudioChannelLayout.Stereo;
    }
}
