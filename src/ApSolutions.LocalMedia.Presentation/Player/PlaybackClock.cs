// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// A position inside a film, written as a clock.
/// </summary>
/// <remarks>
/// <para>
/// The hour appears only when there is one, which is what the prototype's transport draws — 49:06
/// beside 2:00:00 — and what a marker row already said. It lives here because it was about to be
/// written a third time: the transport needs it for the position and the duration, and a format
/// copied per surface is a format that disagrees with itself the first time one copy moves.
/// </para>
/// <para>
/// The invariant culture, and on purpose — the reason came with the marker rows and is unchanged:
/// what comes out is digits and colons, and a culture that wrote the separator differently would be
/// describing a duration rather than a position in a film. The words around it are the part that
/// follows the language.
/// </para>
/// <para>
/// <c>MovieDetailsViewModel</c> writes the same shape against the current culture and is deliberately
/// left alone: that is a decision somebody took for a different surface, and pulling it in here would
/// change visible text nobody has measured.
/// </para>
/// </remarks>
public static class PlaybackClock
{
    public static string Format(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// The one line the mini player has room for: where the session is, how long it runs, and how
    /// fast it is going.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is composed here rather than by three bindings side by side, and the reason is the shape
    /// and not the tidiness. The separators the prototype draws — a slash and a middle dot — are
    /// punctuation and not words, so no dictionary holds them; written into the markup they would be
    /// three <c>TextBlock</c>s with two more between them, and the middle one is empty until the
    /// engine reports a duration. That row does not collapse when a member of it goes blank: it
    /// leaves the punctuation stranded, reading <c>0:12 /  · 1×</c>.
    /// </para>
    /// <para>
    /// So the absence is answered here, once, by leaving the length out until there is one. The
    /// speed stays either way: it is the person's own setting and true from the first frame.
    /// </para>
    /// </remarks>
    /// <param name="speed">
    /// The multiplier as the transport already writes it, passed in rather than formatted here. It
    /// follows the current culture — <c>1,5×</c> in Spanish — and the clock beside it deliberately
    /// does not, so the two formats stay where their reasons are.
    /// </param>
    public static string Readout(TimeSpan position, TimeSpan? duration, string speed) =>
        duration is { } length && length > TimeSpan.Zero
            ? $"{Format(position)} / {Format(length)} · {speed}"
            : $"{Format(position)} · {speed}";
}
