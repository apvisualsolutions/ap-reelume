// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Domain.Catalog;

/// <summary>What a scanned file is called on a card, and what year it says under the name.</summary>
/// <param name="DisplayTitle">The name the grid sorts, searches and paints. Never empty.</param>
/// <param name="Year">The year the card writes beside it, or nothing when the name held none.</param>
public sealed record ScannedTitle(string DisplayTitle, int? Year);

/// <summary>
/// The name a file that nobody has identified goes by.
/// </summary>
/// <remarks>
/// <para>
/// It was the file name, verbatim, from the day the projection was written: «El Faro de Piedra 2019»
/// on the card, with the year inside the title and the release year column empty beside it. Meanwhile
/// <see cref="MediaNameParser"/> had been taking that same name apart since the first week — it is
/// what the review inbox matches against a provider, and since 2026-08-25 it is what turns a folder
/// of episodes into a series. Two readings of one file name, and the one on the screen was the raw
/// one.
/// </para>
/// <para>
/// The rule is one sentence and it is here rather than in the repository that writes it, for the
/// reason this tree keeps rediscovering: a rule inside an adapter is a rule only a machine with a
/// database can ask about. This is a pure function over what the parser already returned.
/// </para>
/// <para>
/// <b>The file name is the floor, not the ceiling.</b> A parse can legitimately leave nothing behind
/// — «2019.mkv» is a year and no title, and a name made entirely of release tags is all noise — and a
/// blank card is worse than a messy one. So an empty clean title falls back to exactly what this
/// projection used to write always.
/// </para>
/// </remarks>
public static class ScannedTitlePolicy
{
    /// <summary>The title and year for one scanned file, given the name and what was read out of it.</summary>
    /// <param name="fileName">The file's own name, with or without its extension.</param>
    /// <param name="parsed">What <see cref="IMediaNameParser"/> made of that name.</param>
    public static ScannedTitle For(string fileName, ParsedMediaName parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // Trimmed and not null-checked: CleanTitle is a non-nullable member of the parser's own
        // record, so `?. ?? ""` would be two branches nothing in this repository can take — the
        // defect of the house wearing the face of caution, which the coverage gate reads as a hole.
        var cleaned = parsed.CleanTitle.Trim();
        return cleaned.Length == 0
            ? new ScannedTitle(Fallback(fileName), Year: null)
            : new ScannedTitle(cleaned, parsed.Year);
    }

    /// <summary>
    /// The name with its extension taken off, and the whole name when taking it off leaves nothing.
    /// </summary>
    /// <remarks>
    /// The second half is not hypothetical on Windows: a file called <c>.mkv</c> is a legal name whose
    /// stem is the empty string, and this projection is <c>NOT NULL</c>.
    /// </remarks>
    private static string Fallback(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(stem) ? fileName : stem;
    }
}
