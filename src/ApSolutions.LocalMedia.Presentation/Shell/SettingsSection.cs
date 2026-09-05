// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Shell;

/// <summary>
/// The sections of the Settings page, one on screen at a time, in the order the prototype's index
/// lists them. Each is a place the side index can point at — not a route: leaving Settings and
/// coming back finds the same section standing.
/// </summary>
public enum SettingsSection
{
    Appearance,

    /// <summary>The library's folders and the scanning that watches them: «Biblioteca y escaneo».</summary>
    Library,

    /// <summary>What happens when an episode or a lesson ends: «Reproducción».</summary>
    Playback,

    Recommendations,
    Subtitles,
    SegmentDetection,
    Shortcuts,
    Lifecycle,
    Privacy,

    /// <summary>«Copias y restauración»: the rail's old destination, now where decisions live.</summary>
    Backups,
    Updates,
    Credits,
}
