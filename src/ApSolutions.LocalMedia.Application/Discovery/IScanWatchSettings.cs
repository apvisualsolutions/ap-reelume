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

    void SetWatchLocalRoots(bool enabled);
}
