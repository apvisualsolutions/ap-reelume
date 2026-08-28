// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>
/// Turns the poster path a metadata provider sent into the one address this application is willing
/// to fetch a picture from, or into nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// Same shape and same reason as <see cref="TrailerLinkPolicy"/>: the value is checked before
/// anything is composed, because composing first leaves a malformed address in existence and every
/// reader downstream then has to remember to distrust it. What arrives is
/// <c>poster_path</c> exactly as TMDB writes it — <c>/wXsQvli6tWqja51pYxXNG1LFIGV.jpg</c> — which is
/// a remote string and therefore not to be trusted with the shape of a URL.
/// </para>
/// <para>
/// The rule is a leading slash, then one file name of <c>[A-Za-z0-9_-]</c> and a dot, and nothing
/// else. Spelled out rather than handed to a pattern, so that what is refused can be read: a second
/// slash (which would let a path climb out of <c>/t/p/&lt;size&gt;/</c>), a dot-dot, a scheme, a
/// query, a fragment, an authority. <see cref="char.IsAsciiLetterOrDigit"/> and not
/// <c>char.IsLetterOrDigit</c>, because a file name here is a fixed alphabet and not every digit
/// Unicode knows.
/// </para>
/// <para>
/// <b>One size and not two.</b> The card draws the poster twice — raised at 158×237 and bled across
/// the header behind a gradient — and TMDB serves a size per address, so two sizes would be two
/// downloads and two cache entries per title. <c>w780</c> is 780×1170: more than twice the raised
/// poster's pixels, which covers any display scaling this application supports, and enough across a
/// 1,180 px header whose near side is covered at 95% opacity. That is the cession, and it is
/// measured rather than assumed.
/// </para>
/// </remarks>
public static class PosterAddressPolicy
{
    /// <summary>The host, which <c>NetworkPurposeRegistry</c> declares for <c>ArtworkCache</c>.</summary>
    public const string Host = "image.tmdb.org";

    /// <summary>The one size fetched. See the remarks for why there is only one.</summary>
    public const string Size = "w780";

    /// <summary>
    /// The absolute address for a well-formed poster path, or <see langword="null"/> for anything else.
    /// </summary>
    public static string? TryBuildPosterAddress(string? posterPath) =>
        IsWellFormedPath(posterPath) ? $"https://{Host}/t/p/{Size}{posterPath}" : null;

    private static bool IsWellFormedPath(string? posterPath)
    {
        // A path is a slash, at least one character of name, a dot, and at least one of extension.
        if (posterPath is null || posterPath.Length < 4 || posterPath[0] != '/')
        {
            return false;
        }

        var dot = posterPath.LastIndexOf('.');
        if (dot < 2 || dot == posterPath.Length - 1)
        {
            return false;
        }

        for (var index = 1; index < posterPath.Length; index++)
        {
            var character = posterPath[index];
            if (index == dot)
            {
                continue;
            }

            if (!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
