// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Tracks the one-time step from hardware decoding to software. Once a media has fallen back it is
/// not retried in hardware, so a failing decoder cannot make the session flap between the two.
/// </summary>
/// <remarks>
/// <para>
/// It used to build the reported decision as well, and that method went with the engine's decision to
/// stop asking for a graphics-card surface at all: nothing called it any more, and a method with no
/// caller outside its own tests is this repository's house defect wearing a small hat.
/// </para>
/// <para>
/// A <c>Reset</c> went the same way on 2026-08-25, and it is the same story again: it said it was
/// for "when a new engine is created", and a new engine builds a new one of these, so nothing in
/// <c>src/</c> or in any test had ever called it. The coverage gate is what noticed — one line of a
/// small file is a whole percentage point.
/// </para>
/// </remarks>
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
}
