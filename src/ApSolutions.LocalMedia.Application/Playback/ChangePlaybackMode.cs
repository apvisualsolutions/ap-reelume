// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>Where the picture is being shown. Exactly one is in force at a time.</summary>
public enum PlaybackMode
{
    Embedded,
    Fullscreen,
    Mini,
}

/// <summary>The transition that happened, with the position it preserved.</summary>
public sealed record PlaybackModeChange(PlaybackMode From, PlaybackMode To, TimeSpan Position);

/// <summary>
/// Moves the active session between window modes without stopping it. The engine is never reopened,
/// so the position, the tracks, and the volume survive every transition, and there is never more
/// than one mode in force.
/// </summary>
public sealed class ChangePlaybackMode : IDisposable
{
    private readonly IMediaPlayerEngine _engine;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ChangePlaybackMode(IMediaPlayerEngine engine) =>
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    public void Dispose() => _gate.Dispose();

    /// <summary>The mode currently in force.</summary>
    public PlaybackMode Current { get; private set; } = PlaybackMode.Embedded;

    /// <summary>How many times the mode has changed, so a test can prove the session survived.</summary>
    public int TransitionCount { get; private set; }

    public async Task<PlaybackModeChange> ExecuteAsync(
        PlaybackMode target,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await _engine.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var from = Current;
            if (from == target)
            {
                return new PlaybackModeChange(from, target, snapshot.Position);
            }

            Current = target;
            TransitionCount++;
            return new PlaybackModeChange(from, target, snapshot.Position);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Toggles between a mode and embedded, which is what a shortcut does.</summary>
    public Task<PlaybackModeChange> ToggleAsync(PlaybackMode mode, CancellationToken cancellationToken = default) =>
        ExecuteAsync(Current == mode ? PlaybackMode.Embedded : mode, cancellationToken);
}
