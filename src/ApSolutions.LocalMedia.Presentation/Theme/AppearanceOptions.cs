// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Appearance;

namespace ApSolutions.LocalMedia.Presentation.Theme;

/// <summary>How much room the library gives a row and a cover.</summary>
public enum InterfaceDensity
{
    Compact,
    Comfortable,
    Roomy,
}

/// <summary>The three corner scales the prototype offers, as the radius each one draws.</summary>
public enum CornerRounding
{
    Sharp,
    Soft,
    VeryRound,
}

/// <summary>
/// Everything the Appearance page decides that is not the theme or the language.
/// </summary>
/// <remarks>
/// <para>
/// One record rather than nine settings, because they are applied together: writing them one at a
/// time means nine passes over the resource dictionary and nine chances for the interface to be
/// half-way between two states. The page hands over the whole of what it wants and the service
/// makes the screen match.
/// </para>
/// <para>
/// The defaults are what the application already looked like before any of this could be chosen, so
/// a profile with nothing stored renders exactly as it did: the prototype's first accent, Mica on,
/// the tint at full, comfortable rows, the 148 px cover this tree has always drawn, soft corners,
/// titles under the covers, and motion left to Windows.
/// </para>
/// </remarks>
public sealed record AppearanceOptions
{
    /// <summary>The cover width the library grid has drawn since the redesign.</summary>
    public const int DefaultCoverWidth = 148;

    /// <summary>The narrowest and widest the prototype's slider goes.</summary>
    public const int MinimumCoverWidth = 110;

    public const int MaximumCoverWidth = 220;

    public string Accent { get; init; } = AccentPalette.Presets[0];

    /// <summary>
    /// Whether the light/dark choice follows Windows. Off, the manual pill takes over.
    /// </summary>
    /// <remarks>
    /// It is the same fact <see cref="ThemePreference.System"/> already carries, said as the toggle
    /// the prototype draws: this is what the page writes, and <c>IThemeService</c> is what it writes
    /// to. Two properties would be two answers to one question.
    /// </remarks>
    public bool FollowsWindowsTheme { get; init; } = true;

    public bool Mica { get; init; } = true;

    /// <summary>The strength of the coloured glow at the top of the content, 0 to 100.</summary>
    public int TintPercent { get; init; } = 100;

    public InterfaceDensity Density { get; init; } = InterfaceDensity.Comfortable;

    public int CoverWidth { get; init; } = DefaultCoverWidth;

    public CornerRounding Rounding { get; init; } = CornerRounding.Soft;

    public bool CoverTitles { get; init; } = true;

    /// <summary>
    /// Whether the interface animates. Turning it off is what Windows' reduced motion asks for, so
    /// the two are one setting: Windows can force it on, and this can force it on as well.
    /// </summary>
    public bool Animations { get; init; } = true;
}
