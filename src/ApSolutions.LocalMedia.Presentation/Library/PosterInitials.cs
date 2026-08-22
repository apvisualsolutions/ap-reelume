// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// The letters a poster shows when there is no artwork, which in this application is always.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for "initials over <c>ControlFillBrush</c>, never a hole": a card with an empty rectangle
/// where the artwork would be reads as something that failed to load, and nothing is loading — this
/// application ships with no artwork at all and no token to fetch any. Two letters of the title turn
/// the same rectangle into a deliberate placeholder, and they differ between cards, which is what
/// makes a grid of them scannable.
/// </para>
/// <para>
/// It is one function in one place because four view models feed the same card. Written twice it
/// would be two rules the day somebody decided that three letters read better.
/// </para>
/// </remarks>
public static class PosterInitials
{
    /// <summary>How many letters a poster carries. Three is a word, one is not a mark.</summary>
    private const int Letters = 2;

    /// <summary>
    /// The first letter of each of the first two words, in upper case; empty for an empty title.
    /// </summary>
    /// <remarks>
    /// Words are split on whitespace and the first letter or digit of each is taken, so a title that
    /// opens with a bracket or a dash contributes the character a reader would call its first.
    /// Articles are not skipped: which words are articles is a question about a language, and the
    /// title of a film is written in whichever one its makers chose.
    /// </remarks>
    public static string From(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var initials = new List<char>(Letters);
        foreach (var word in title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.FirstOrDefault(char.IsLetterOrDigit) is var first and not '\0')
            {
                initials.Add(char.ToUpperInvariant(first));
                if (initials.Count == Letters)
                {
                    break;
                }
            }
        }

        return new string([.. initials]);
    }
}
