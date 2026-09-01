// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>What happened when the endpoint was asked to carry a different layout.</summary>
public enum AudioEndpointChange
{
    /// <summary>The endpoint now carries the layout asked for.</summary>
    Applied,

    /// <summary>The endpoint is already carrying it, and nothing was written.</summary>
    AlreadySet,

    /// <summary>The endpoint's driver will not take that layout.</summary>
    RefusedByDevice,

    /// <summary>This machine offers no way to write the layout at all.</summary>
    Unavailable,
}

/// <summary>
/// Writes the channel layout an output endpoint carries.
/// </summary>
/// <remarks>
/// <b>This changes a Windows setting, not an application one</b>, and that is the whole reason this
/// port exists rather than a player call. Measured on 2026-09-02 across every alternative: LibVLC's
/// only live channel API takes stereo modes alone — its enumeration is <c>Stereo, RStereo, Left,
/// Right, Dolbys</c> and nothing more — and on an eight-channel endpoint asking for stereo changed
/// not one decibel of the eight tones that came out. Nor does an instance option exist:
/// <c>--stereo-mode=1</c> changed nothing, <c>--audio-channels</c> is not an option at all and stops
/// the instance from starting, and the other output modules never reached the endpoint.
/// <para>
/// What decides the channel count is the endpoint's own format, which is why this writes that. The
/// consequence is that the person's choice reaches every application on the machine, so the
/// interface says so before it is made rather than after.
/// </para>
/// <para>
/// Writing it invalidates every audio client on that endpoint — <c>AUDCLNT_E_DEVICE_INVALIDATED</c>,
/// which is documented Windows behaviour — and LibVLC's own recovery from that
/// <b>discards the selected device and falls back to the default one</b>
/// (<c>DeviceSelect(aout, NULL)</c> in <c>mmdevice.c</c>). So whoever calls this has to put the
/// chosen device back afterwards, and that is not optional: skipping it moves the sound to a
/// different pair of speakers.
/// </para>
/// </remarks>
public interface IAudioEndpointConfigurator
{
    /// <summary>True where this machine can write an endpoint format at all.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// The layouts this endpoint's driver will accept, which is not the same as the one it carries.
    /// </summary>
    /// <remarks>
    /// The catalogue reads the layout an endpoint is <b>set to</b>, and that is the right answer for
    /// "what is playing". It is the wrong one for "what can be chosen": an endpoint set to stereo
    /// would offer stereo alone, so a person who reduced it once could never raise it again — the
    /// control would be a one-way door. The driver is asked instead, in exclusive mode, which is
    /// where it answers about itself rather than about the current shared mix.
    /// <para>
    /// Asked for one endpoint rather than for the whole catalogue on purpose: it activates an audio
    /// client per call, and a machine with a dozen endpoints would pay for eleven answers nobody
    /// reads.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<AudioChannelLayout>> GetSupportedLayoutsAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>Asks the endpoint to carry the layout, and says what actually happened.</summary>
    Task<AudioEndpointChange> SetLayoutAsync(
        string deviceId,
        AudioChannelLayout layout,
        CancellationToken cancellationToken = default);
}
