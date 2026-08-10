// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>
/// Hands one existing file to whatever the operating system associates with it. This is the escape
/// hatch offered when the embedded engine cannot present a file; the application makes no promise
/// about the external player's position and never imports, copies, or modifies the file.
/// </summary>
public interface IExternalPlaybackLauncher
{
    /// <summary>Opens the file externally and reports whether the shell accepted the request.</summary>
    Task<bool> TryLaunchAsync(string path, CancellationToken cancellationToken = default);
}
