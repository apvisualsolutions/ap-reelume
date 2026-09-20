// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Application.Discovery;

/// <summary>
/// Whether local folders are followed live, as the one person who can answer that decided.
/// </summary>
/// <remarks>
/// The setting existed on screen before it existed here: «Vigilar cambios en raíces locales» was
/// painted, toggled and reset, and read by nobody — a field with no store and no consumer, which is
/// ENG-010. What it was missing is this port and the decision behind it
/// (<c>ScanWatchPolicy.ShouldWatchLive</c>).
/// <para>
/// Unlike the connection settings, the absence of a stored value means <b>yes</b> here: watching a
/// folder on this machine sends nothing anywhere, the scope record promises it, and an installation
/// that has never been asked should still see a new film appear.
/// </para>
/// </remarks>
public interface IScanWatchSettings
{
    bool WatchLocalRoots { get; }

    /// <summary>
    /// How long the recovery sweep waits between passes. Always within
    /// <c>ScanWatchPolicy.MinimumSweepInterval</c> and <c>ScanWatchPolicy.MaximumSweepInterval</c>:
    /// whoever implements this brings a stored value back into range, so the scheduler reading it
    /// can believe it without checking.
    /// </summary>
    TimeSpan SweepInterval { get; }

    /// <summary>
    /// Raised after either value changes, so the watching can be rebuilt then and there.
    /// </summary>
    /// <remarks>
    /// Without this the screen would be back where ENG-044 found it: a control that governs
    /// something, but not until the next launch. The whole point of that task was that a switch
    /// nobody's code reads and a switch nobody's code reads <i>yet</i> look identical from the
    /// outside.
    /// </remarks>
    event EventHandler? Changed;

    void SetWatchLocalRoots(bool enabled);

    void SetSweepInterval(TimeSpan interval);
}
