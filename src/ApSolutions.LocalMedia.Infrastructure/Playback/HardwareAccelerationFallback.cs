// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Tracks the one-time step from hardware decoding to software. Once a media has fallen back it is
/// not retried in hardware, so a failing decoder cannot make the session flap between the two.
/// </summary>
public sealed class HardwareAccelerationFallback
{
    private readonly Lock _sync = new();
    private bool _hardwareDisabled;

    /// <summary>True once the fallback has happened for this engine.</summary>
    public bool HasFallenBack
    {
        get
        {
            lock (_sync)
            {
                return _hardwareDisabled;
            }
        }
    }

    /// <summary>Whether the next open should ask for hardware decoding.</summary>
    public bool ShouldUseHardware(bool requested)
    {
        lock (_sync)
        {
            return requested && !_hardwareDisabled;
        }
    }

    /// <summary>
    /// Records that hardware decoding did not work. Returns true the first time only, so the caller
    /// retries once in software and never again.
    /// </summary>
    public bool TryFallBack()
    {
        lock (_sync)
        {
            if (_hardwareDisabled)
            {
                return false;
            }

            _hardwareDisabled = true;
            return true;
        }
    }

    /// <summary>Builds the decision to report for this attempt.</summary>
    public VideoOutputDecision Decide(
        VideoSourceCapabilities source,
        DisplayCapabilities display,
        bool hardwareRequested) =>
        VideoOutputPolicy.Decide(source, display, hardwareRequested, ShouldUseHardware(hardwareRequested));

    /// <summary>Forgets the fallback, which only happens when a new engine is created.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            _hardwareDisabled = false;
        }
    }
}
