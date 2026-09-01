// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// The part of a session an output switch touches. Kept narrow on purpose: switching a device must
/// pause and resume, never stop and reopen, so the position and the tracks are preserved.
/// </summary>
public interface IAudioOutputTarget
{
    bool IsPlaying { get; }

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task SetOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The live engine as an output switch sees it (AUD-A01). Pause and resume go through the same
/// engine calls the transport uses, and the device routing reaches the session that is actually
/// playing — which is the half the audit found missing.
/// </summary>
public sealed class EngineAudioOutputTarget : IAudioOutputTarget
{
    private readonly IMediaPlayerEngine _engine;

    public EngineAudioOutputTarget(IMediaPlayerEngine engine) =>
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    public bool IsPlaying => _engine.State == PlaybackState.Playing;

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        _engine.PauseAsync(cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        _engine.PlayAsync(cancellationToken);

    public Task SetOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
        _engine.SetAudioOutputDeviceAsync(deviceId, cancellationToken);
}

/// <summary>What the person asked for and where the choice should be remembered.</summary>
public sealed record AudioOutputRequest(
    string? DeviceId,
    AudioChannelLayout Layout,
    PreferenceScope Scope,
    string ScopeKey);

/// <summary>
/// Applies an output choice to the active session and remembers it by stable endpoint identifier.
/// A device that has gone away falls back to the default without rewriting the stored preference, so
/// plugging it back in restores it.
/// </summary>
public sealed class LibVlcAudioOutputAdapter : IDisposable
{
    private readonly IAudioDeviceCatalog _catalog;
    private readonly IPlaybackPreferenceRepository _preferences;
    private readonly IAudioEndpointConfigurator? _endpoints;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The configurator is optional, and a session built without one simply routes without changing
    /// any layout — which is what every context that is not Windows wants, and what the tests that
    /// only care about routing want too.
    /// </summary>
    public LibVlcAudioOutputAdapter(
        IAudioDeviceCatalog catalog,
        IPlaybackPreferenceRepository preferences,
        IAudioEndpointConfigurator? endpoints = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _endpoints = endpoints;
    }

    /// <summary>What the last layout change actually did, for the surface to describe.</summary>
    public AudioEndpointChange LastLayoutChange { get; private set; } = AudioEndpointChange.AlreadySet;

    public void Dispose() => _gate.Dispose();

    /// <summary>Resolves the stored choice against what the machine offers right now.</summary>
    public async Task<AudioOutputSelection?> ResolveStoredAsync(
        PreferenceScope scope,
        string scopeKey,
        AudioChannelLayout desiredLayout,
        CancellationToken cancellationToken = default)
    {
        var stored = await _preferences.GetAsync(scope, scopeKey, cancellationToken).ConfigureAwait(false);
        var devices = await _catalog.GetOutputsAsync(cancellationToken).ConfigureAwait(false);
        return AudioOutputPolicy.Resolve(devices, stored?.AudioOutputDeviceId, desiredLayout);
    }

    /// <summary>Chooses an output and stores it, without touching an active session.</summary>
    public Task<AudioOutputSelection?> SelectAsync(
        AudioOutputRequest request,
        CancellationToken cancellationToken = default) =>
        SelectAsync(request, target: null, cancellationToken);

    /// <summary>
    /// Chooses an output, applies it to the session, and stores it. The session is paused before the
    /// switch and resumed after it, so a hot change never restarts the media.
    /// </summary>
    public async Task<AudioOutputSelection?> SelectAsync(
        AudioOutputRequest request,
        IAudioOutputTarget? target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var devices = await _catalog.GetOutputsAsync(cancellationToken).ConfigureAwait(false);
            var selection = AudioOutputPolicy.Resolve(devices, request.DeviceId, request.Layout);
            if (selection is null)
            {
                return null;
            }

            // The layout is written on the endpoint before the device is routed, and the order is
            // the whole of why this works. Writing it invalidates every audio client on that
            // endpoint, and LibVLC's recovery from that discards the chosen device and falls back to
            // the default one — so the routing below is what puts the choice back. Doing it the
            // other way round moves the sound to a different pair of speakers and leaves the
            // interface claiming otherwise.
            if (_endpoints is { IsAvailable: true })
            {
                LastLayoutChange = await _endpoints
                    .SetLayoutAsync(selection.Device.Id, request.Layout, cancellationToken)
                    .ConfigureAwait(false);

                if (LastLayoutChange is AudioEndpointChange.Applied)
                {
                    // What the endpoint carries has changed, so what the session will actually play
                    // is read again rather than assumed from the request.
                    devices = await _catalog.GetOutputsAsync(cancellationToken).ConfigureAwait(false);
                    selection = AudioOutputPolicy.Resolve(devices, selection.Device.Id, request.Layout)
                        ?? selection;
                }
            }
            else
            {
                LastLayoutChange = AudioEndpointChange.Unavailable;
            }

            if (target is { IsPlaying: true })
            {
                await target.PauseAsync(cancellationToken).ConfigureAwait(false);
                await target.SetOutputDeviceAsync(selection.Device.Id, cancellationToken).ConfigureAwait(false);
                await target.ResumeAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (target is not null)
            {
                await target.SetOutputDeviceAsync(selection.Device.Id, cancellationToken).ConfigureAwait(false);
            }

            // Only an explicit choice is stored. A fallback caused by an absent device must not
            // overwrite the preference, or unplugging a headset once would forget it forever.
            if (request.DeviceId is not null && !selection.FellBackToDefaultDevice)
            {
                var stored = await _preferences
                    .GetAsync(request.Scope, request.ScopeKey, cancellationToken)
                    .ConfigureAwait(false)
                    ?? new PlaybackPreference { Scope = request.Scope, ScopeKey = request.ScopeKey };
                await _preferences
                    .SaveAsync(stored with { AudioOutputDeviceId = selection.Device.Id }, cancellationToken)
                    .ConfigureAwait(false);
            }

            return selection;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
