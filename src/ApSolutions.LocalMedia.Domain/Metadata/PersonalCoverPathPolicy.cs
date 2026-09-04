// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>
/// Turns the poster field's stored value into the name of a cover this application itself wrote, or
/// into nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The counterpart of <see cref="PosterAddressPolicy"/>, and it exists for the same reason.</b>
/// One field holds two kinds of thing: what a provider sent, and where somebody's own cover landed.
/// That policy answers the first; until 2026-09-04 nothing answered the second, so a cover chosen
/// with the picker was written, locked, carried in the backup — and never drawn, because the only
/// question anybody asked of the field was «is this a provider path», and an absolute path is not.
/// </para>
/// <para>
/// <b>What is kept is the name, and the directory is thrown away.</b> That is the whole of the
/// security argument and it is worth spelling out. The stored value is free text: a person types
/// into it, a provider can write it whenever the field is unlocked, and a restored backup can carry
/// one written on somebody else's machine. Composing a path out of it would turn a text box into a
/// reader of arbitrary files. So the caller composes the path itself, out of the application's own
/// data directory and the title being drawn, and the only thing taken from the stored value is a
/// name this policy has already agreed to.
/// </para>
/// <para>
/// <b>And the agreed name is an alphabet, not a filter.</b> Sixty-four lower-case hexadecimal
/// characters and one approved extension, which is exactly what <c>ArtworkCache</c> writes: the
/// SHA-256 of the bytes it copied. Nothing else can be spelled in that alphabet — not a separator,
/// not a drive's colon, not the two dots of a climb, not the leading pair of a network share, not
/// the colon of an alternate data stream, not a reserved device name. Those are not refused one by
/// one, which is a list somebody has to keep complete; they are simply unspellable.
/// </para>
/// <para>
/// <b>What this does not defend against, said out loud.</b> A directory junction planted inside the
/// application's own data folder would redirect a path composed entirely of trusted pieces. Whoever
/// can plant one can already rewrite the bytes of the image itself, so the guard would buy nothing;
/// it is named here so the next reader knows it was measured and not missed.
/// </para>
/// </remarks>
public static class PersonalCoverPathPolicy
{
    /// <summary>
    /// The length of the name <c>ArtworkCache</c> writes: a SHA-256 rendered as hexadecimal.
    /// </summary>
    public const int NameLength = 64;

    /// <summary>
    /// The name of the personal cover this value points at, or <see langword="null"/> when the value
    /// is not one — which includes every provider path, every hand-typed path, and every attempt to
    /// name a file somewhere else.
    /// </summary>
    /// <remarks>
    /// The directory part is read only to be discarded: a value carried over from another machine
    /// names a folder that does not exist here, and the cover is still found, because the folder was
    /// never the part that mattered. That is what makes a restored backup draw its covers again.
    /// </remarks>
    public static string? TryGetCoverFileName(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
        {
            return null;
        }

        // Both separators, on every platform. This reads a value that may have been written on a
        // machine whose separator is not this one's, and asking the running platform which one to
        // look for is how the same string means two things in two places.
        var start = posterPath.LastIndexOfAny(['/', '\\']) + 1;
        var name = posterPath[start..];

        // The name is the hash, a dot, and an approved container. A dot anywhere else — «<hash>.png.exe»,
        // «<hash>.png:stream» — leaves a remainder the approved list does not contain, so it is
        // refused by the same comparison and not by a rule of its own.
        if (name.Length <= NameLength || name[NameLength] != '.')
        {
            return null;
        }

        for (var index = 0; index < NameLength; index++)
        {
            if (!IsLowerCaseHex(name[index]))
            {
                return null;
            }
        }

        // The extension keeps whatever case the chosen file had, because the import writes it back
        // unchanged; the approved list is the one that decides, and it ignores case.
        return CoverImageRules.IsApprovedExtension(name[NameLength..]) ? name : null;
    }

    /// <summary>
    /// Lower case only, because lower case is what the store writes. Accepting upper case would
    /// widen the alphabet to admit names this application never produces.
    /// </summary>
    private static bool IsLowerCaseHex(char character) =>
        character is >= '0' and <= '9' || character is >= 'a' and <= 'f';
}
