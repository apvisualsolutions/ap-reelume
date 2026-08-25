// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.Domain.Identification;

/// <summary>Where one episode file belongs: which series, which season, which number.</summary>
public sealed record LocalSeriesPlacement(
    string SeriesKey,
    string SeriesTitle,
    int SeasonNumber,
    int EpisodeNumber,
    string EpisodeTitle);

/// <summary>
/// Reads a folder of episodes as a series, with nobody's help and no provider.
/// </summary>
/// <remarks>
/// <para>
/// This is the rule the catalogue was missing entirely. <c>MediaNameParser</c> has read
/// <c>S01E01</c>, <c>1x04</c>, «Temporada 1 Episodio 2» and <c>Cap.803</c> since it was written, and
/// nothing ever asked it where the episode belonged: every scanned file became one loose card of its
/// own. The owner put two shows on the disk on 2026-08-25 — eight seasons and seventy-four episodes
/// of one, three and twenty-five of the other — and got a hundred and two cards.
/// </para>
/// <para>
/// <b>It is the folder that names a series, not the file.</b>
/// <c>D:\Series\Juego de Tronos\Temporada 1\S01E01.mkv</c> has the show's name written once, in the
/// folder; the file name carries a number and whatever the encoder felt like adding. So the series
/// folder is the anchor, and the parsed title is only the fallback for an episode sitting loose in a
/// root. It is also the signal the prototype's own review inbox states out loud: «La carpeta padre
/// coincide con una serie ya catalogada».
/// </para>
/// <para>
/// <b>The key carries the root.</b> Two roots can hold folders of the same name — a library and its
/// backup — and merging them by name would fold a copy into the original and count its episodes
/// twice. Keeping the root in the key makes «the same show on two drives» two entries, which is what
/// the disk actually holds and what the prototype's own model stores.
/// </para>
/// <para>
/// <b>Nothing here does any I/O and nothing here guesses.</b> A file whose name says no season and no
/// episode is not a series and gets no placement: it stays the loose card it already was, which is
/// the honest answer for a film.
/// </para>
/// </remarks>
public static class LocalSeriesPolicy
{
    /// <summary>
    /// A folder that names a season rather than a show: «Temporada 3», «Season 3», «S03», «T03».
    /// </summary>
    private static readonly Regex SeasonFolder = new(
        @"^\s*(?:season|temporada|s|t)\s*[-_.]?\s*\d{1,3}\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    /// <summary>A trailing year in brackets, which a folder name carries and a title does not.</summary>
    private static readonly Regex TrailingYear = new(
        @"\s*[\(\[]\s*(?:19|20)\d{2}\s*[\)\]]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Where this file belongs, or null when it is not an episode of anything.
    /// </summary>
    /// <param name="rootId">The library root the file was found under, which the key is scoped to.</param>
    /// <param name="context">The file name and the folders between the root and it, outermost first.</param>
    /// <param name="parsed">What the name parser made of the file name.</param>
    public static LocalSeriesPlacement? Place(Guid rootId, FileNameContext context, ParsedMediaName parsed)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parsed);
        if (parsed.Kind != ParsedMediaKind.Episode
            || parsed.Season is not { } season
            || parsed.Episode is not { } episode)
        {
            return null;
        }

        var folders = context.ParentFolders;
        var depth = folders.Count;

        // The last folder is either the season's or the show's. It is the season's when it says so,
        // and only then does the one above it get to name the show — an episode in a season folder
        // that sits directly in the root has no show folder at all and falls back to its own name.
        var seasonFolderLast = depth > 0 && SeasonFolder.IsMatch(folders[depth - 1]);
        var showFolderIndex = seasonFolderLast ? depth - 2 : depth - 1;

        var seriesTitle = showFolderIndex >= 0
            ? CleanFolderName(folders[showFolderIndex])
            : string.Empty;
        if (seriesTitle.Length == 0)
        {
            seriesTitle = parsed.CleanTitle.Trim();
        }

        if (seriesTitle.Length == 0)
        {
            return null;
        }

        // The path down to and including the show's folder, which is what two episodes of one show
        // share and two shows never do. With no show folder the title stands in for it, so every
        // episode of «Puerto Sombra» loose in a root still meets in one place.
        var keyParts = showFolderIndex >= 0
            ? folders.Take(showFolderIndex + 1)
            : [seriesTitle];
        var key = rootId.ToString("D", CultureInfo.InvariantCulture)
            + "/"
            + string.Join('/', keyParts.Select(part => part.Trim().ToLowerInvariant()));

        // The episode's own name, and only when it is one: the parser returns the whole cleaned file
        // name, which for «Juego de Tronos - S01E01» is the show's name over again. A row that says
        // the show's name once per episode is a column of the same word.
        var episodeTitle = parsed.CleanTitle.Trim();
        if (episodeTitle.Length == 0
            || string.Equals(episodeTitle, seriesTitle, StringComparison.OrdinalIgnoreCase))
        {
            episodeTitle = string.Empty;
        }

        return new LocalSeriesPlacement(key, seriesTitle, season, episode, episodeTitle);
    }

    /// <summary>The identifier of the series a key stands for, the same one every time.</summary>
    /// <remarks>
    /// Derived and not stored, because there is nowhere to store it before the series exists: the
    /// first episode of a show has to arrive at the same identifier as the seventy-fourth, in a
    /// different scan, on a different day, without either of them having read the other. A hash of
    /// the key gives that for nothing, and the version and variant bits are set so the result is a
    /// well-formed UUID rather than sixteen bytes wearing a GUID's clothes.
    /// </remarks>
    public static Guid ShowIdFor(string seriesKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
        return Derive("show:" + seriesKey);
    }

    /// <summary>The identifier of one episode of that series.</summary>
    public static Guid EpisodeIdFor(string seriesKey, int seasonNumber, int episodeNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
        return Derive(string.Create(
            CultureInfo.InvariantCulture,
            $"episode:{seriesKey}:{seasonNumber}:{episodeNumber}"));
    }

    /// <summary>
    /// A folder name as a title: without the year somebody wrote in brackets, and without the
    /// separators a folder is allowed to carry.
    /// </summary>
    private static string CleanFolderName(string folder)
    {
        var withoutYear = TrailingYear.Replace(folder, string.Empty);
        var spaced = withoutYear.Replace('.', ' ').Replace('_', ' ');
        return string.Join(
            ' ',
            spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static Guid Derive(string value)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = digest.AsSpan(0, 16).ToArray();

        // Version 8, «custom», which is exactly what this is: a UUID whose bits come from a rule
        // this application wrote down rather than from a clock or a random source.
        //
        // Byte 7 and not byte 6, and the difference is .NET's own layout rather than the standard's:
        // Guid keeps its first three fields little-endian, so the byte a canonical UUID calls the
        // seventh — the one carrying the version — is index 7 of the array both the constructor and
        // ToByteArray use. Written the obvious way it set the version on the field's low byte and
        // the test read 0x60 where 0x80 was meant.
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
