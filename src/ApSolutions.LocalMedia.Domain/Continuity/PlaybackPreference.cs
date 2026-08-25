// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>Where a stored preference applies, from the most specific scope to the least.</summary>
public enum PreferenceScope
{
    Global,
    Series,
    File,
}

/// <summary>
/// How a track is chosen. Attributes are stored instead of an index because two episodes of the same
/// show often announce their tracks in a different order.
/// </summary>
public sealed record TrackSelection(string? Language, int? Channels, string? Codec, bool PreferExternal);

/// <summary>
/// Subtitle presentation the person controls. Values are clamped into an accessible range so a stored
/// preference can never make text unreadable or invisible.
/// </summary>
public sealed record SubtitleStyle
{
    public const double MinimumFontSizePercent = 50;
    public const double MaximumFontSizePercent = 300;
    public const double MaximumOutlineThickness = 4;

    private SubtitleStyle(
        double fontSizePercent,
        string fontFamily,
        string foregroundHex,
        string backgroundHex,
        double backgroundOpacity,
        double outlineThickness)
    {
        FontSizePercent = fontSizePercent;
        FontFamily = fontFamily;
        ForegroundHex = foregroundHex;
        BackgroundHex = backgroundHex;
        BackgroundOpacity = backgroundOpacity;
        OutlineThickness = outlineThickness;
    }

    /// <summary>What the engine shows when the person has expressed no preference.</summary>
    public static SubtitleStyle EngineDefault { get; } =
        new(100, "Segoe UI", "#FFFFFFFF", "#CC000000", 0.8, 1.5);

    public double FontSizePercent { get; }

    public string FontFamily { get; }

    public string ForegroundHex { get; }

    public string BackgroundHex { get; }

    public double BackgroundOpacity { get; }

    public double OutlineThickness { get; }

    /// <summary>Builds a style whose every value stays inside the approved accessible range.</summary>
    public static SubtitleStyle Create(
        double fontSizePercent,
        string fontFamily,
        string foregroundHex,
        string backgroundHex,
        double backgroundOpacity,
        double outlineThickness) =>
        new(
            Math.Clamp(fontSizePercent, MinimumFontSizePercent, MaximumFontSizePercent),
            string.IsNullOrWhiteSpace(fontFamily) ? EngineDefault.FontFamily : fontFamily.Trim(),
            RequireColour(foregroundHex, nameof(foregroundHex)),
            RequireColour(backgroundHex, nameof(backgroundHex)),
            Math.Clamp(backgroundOpacity, 0, 1),
            Math.Clamp(outlineThickness, 0, MaximumOutlineThickness));

    /// <summary>
    /// Whether a string is a colour this accepts: <c>#RRGGBB</c> or <c>#AARRGGBB</c>.
    /// </summary>
    /// <remarks>
    /// Public because the swatches ask before they offer: a control that hands over a value this
    /// would throw on is a control that crashes the page it is on, and the check that decides it has
    /// to be the same one — a second copy is what disagrees the first time either moves.
    /// </remarks>
    public static bool IsColour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith('#')
            && (trimmed.Length is 7 or 9)
            && trimmed[1..].All(character => Uri.IsHexDigit(character));
    }

    private static string RequireColour(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return IsColour(value)
            ? value.Trim().ToUpperInvariant()
            : throw new ArgumentException(
                $"'{value}' is not a #RRGGBB or #AARRGGBB colour.",
                parameterName);
    }
}

/// <summary>
/// One stored preference for a scope. Every field is optional: an unset field falls through to the
/// next scope, and finally to the engine default.
/// </summary>
public sealed record PlaybackPreference
{
    public required PreferenceScope Scope { get; init; }

    /// <summary>Identifies the series or file this preference belongs to; empty for the global scope.</summary>
    public required string ScopeKey { get; init; }

    public TrackSelection? Audio { get; init; }

    public TrackSelection? Subtitle { get; init; }

    public bool? SubtitlesEnabled { get; init; }

    public double? SpeedMultiplier { get; init; }

    public int? VolumePercent { get; init; }

    /// <summary>Stable identifier of the chosen output device; resolved again on every session.</summary>
    public string? AudioOutputDeviceId { get; init; }

    public SubtitleStyle? SubtitleStyle { get; init; }

    /// <summary>The global scope always uses the same key so it can be stored in one row.</summary>
    public static string GlobalKey => "global";

    public static string SeriesKey(Guid seriesId) =>
        "series:" + seriesId.ToString("D", CultureInfo.InvariantCulture);

    public static string FileKey(Guid mediaFileId) =>
        "file:" + mediaFileId.ToString("D", CultureInfo.InvariantCulture);
}
