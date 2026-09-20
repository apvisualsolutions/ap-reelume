// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Presentation.Shell;

/// <summary>
/// The sections of the Settings page, one on screen at a time, in the order the prototype's index
/// lists them. Each is a place the side index can point at — not a route: leaving Settings and
/// coming back finds the same section standing.
/// </summary>
public enum SettingsSection
{
    Appearance,

    /// <summary>
    /// Which language the interface speaks: «Idioma». A destination of its own since 2026-09-13
    /// (UX-010), where it used to be a card inside Appearance. It is one choice that governs every
    /// screen rather than part of dressing the library — and as a card it would have put a second
    /// «Restaurar valores por defecto» on the same screen, which the walk refuses to click.
    /// </summary>
    Language,

    /// <summary>The library's folders and the scanning that watches them: «Biblioteca y escaneo».</summary>
    Library,

    /// <summary>
    /// Where a cover comes from and in which order: «Orden de las portadas» (LIB-021, ADR-0009). A
    /// destination of its own rather than a card inside <see cref="Library"/>, which already hosts
    /// the scanning group: the gate refuses two option groups in one place, and a second «Restaurar
    /// valores por defecto» on one screen is a button the walk cannot resolve.
    /// </summary>
    Covers,

    // «Reproducción» and «Detección de segmentos» were here until 2026-09-13. They went down to the
    // player's gear (ADR-0012) and are reached through PlayerSettingsGroup now: their value is
    // decided while watching something, which is not a question a settings page can be asked.
    Recommendations,
    Shortcuts,
    Lifecycle,
    Privacy,

    /// <summary>«Copias y restauración»: the rail's old destination, now where decisions live.</summary>
    Backups,
    Updates,
    Credits,
}
