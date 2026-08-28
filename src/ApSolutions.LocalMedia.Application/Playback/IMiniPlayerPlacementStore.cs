// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>Where the mini player was left, in the logical units the layout works in.</summary>
public sealed record MiniPlayerPlacement(double X, double Y, double Width, double Height);

/// <summary>
/// Remembers where the mini player was left between sessions.
/// </summary>
/// <remarks>
/// The coordinator already remembers a placement for as long as the process lives; this is the half
/// that survives closing the application. It is a port rather than a call into the settings store so
/// that the presentation layer never learns where the file is — the same reason every other
/// preference in this application arrives through one of these.
/// </remarks>
public interface IMiniPlayerPlacementStore
{
    /// <summary>The stored placement, or null when none has been written or the stored one is unusable.</summary>
    MiniPlayerPlacement? Read();

    /// <summary>Writes the placement the mini player was left in.</summary>
    void Save(MiniPlayerPlacement placement);
}
