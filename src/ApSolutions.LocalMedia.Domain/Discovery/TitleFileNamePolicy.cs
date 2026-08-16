// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// What is known about one entry, as far as naming its file goes (LIB-012). Everything is optional
/// except the extension, because a catalogue that has not been identified knows almost nothing.
/// </summary>
public sealed record TitleNaming(
    string? Title,
    int? Year = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null,
    string? EpisodeTitle = null,
    string Extension = "");

/// <summary>
/// The file name an entry deserves (LIB-012).
/// </summary>
/// <remarks>
/// The convention is the one Plex, Jellyfin and Kodi all read, the same one this project already
/// follows in <see cref="TrailerDiscoveryPolicy"/>: <c>Title (Year).ext</c> for a film and
/// <c>Series (Year) - SxxEyy - Title.ext</c> for an episode. Nothing here is invented.
/// <para>
/// This decides a name and nothing else. It does not sanitize and it does not resolve collisions:
/// <see cref="RenamePolicy"/> owns both, and duplicating either would mean two places deciding what
/// is safe to write. What this owns is the shape, and its answer is allowed to be
/// <see langword="null"/> — an entry nobody has identified, whose title is the file name itself,
/// has no better name to propose, and proposing one anyway is how a rename turns into a guess.
/// </para>
/// </remarks>
public static class TitleFileNamePolicy
{
    /// <summary>A year is written with four digits or it is not written at all.</summary>
    private const int EarliestYear = 1000;

    private const int LatestYear = 9999;

    /// <summary>
    /// The name <paramref name="naming"/> deserves, or <see langword="null"/> when there is nothing
    /// better to propose than what the file is already called.
    /// </summary>
    public static string? Compose(TitleNaming naming)
    {
        ArgumentNullException.ThrowIfNull(naming);

        var title = Collapse(naming.Title);
        if (title is null)
        {
            return null;
        }

        var extension = NormalizeExtension(naming.Extension);
        var year = FormatYear(naming.Year);
        var episode = FormatEpisodeNumber(naming.SeasonNumber, naming.EpisodeNumber);
        if (episode is null)
        {
            // Season without episode, or a number no season or episode can hold, describes an entry
            // that is not placed in its series. A film's shape would be a lie about it.
            return naming.SeasonNumber is null && naming.EpisodeNumber is null
                ? $"{title}{year}{extension}"
                : null;
        }

        var episodeTitle = Collapse(naming.EpisodeTitle);
        return episodeTitle is null
            ? $"{title}{year} - {episode}{extension}"
            : $"{title}{year} - {episode} - {episodeTitle}{extension}";
    }

    /// <summary>
    /// One run of whitespace becomes one space, and a value that holds nothing else becomes
    /// <see langword="null"/>: a title of three spaces is an absent title, not a name.
    /// </summary>
    private static string? Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words);
    }

    /// <summary>
    /// The extension as written, with the separator it needs. An entry with no extension keeps none:
    /// the approved-extension list is what decides whether a file can be opened at all, and this is
    /// not the place to hand one out.
    /// </summary>
    private static string NormalizeExtension(string extension)
    {
        var trimmed = extension.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    private static string FormatYear(int? year) =>
        year is >= EarliestYear and <= LatestYear
            ? $" ({year.Value.ToString(CultureInfo.InvariantCulture)})"
            : string.Empty;

    /// <summary>
    /// <c>SxxEyy</c>, with both halves padded to two digits and neither truncated: a series with a
    /// hundred episodes in a season writes three digits rather than losing one. Season zero is the
    /// specials season and is a season like any other here.
    /// </summary>
    private static string? FormatEpisodeNumber(int? season, int? episode)
    {
        if (season is not { } seasonNumber || episode is not { } episodeNumber)
        {
            return null;
        }

        if (seasonNumber < 0 || episodeNumber < 0)
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"S{seasonNumber:D2}E{episodeNumber:D2}");
    }
}
