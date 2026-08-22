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
}
