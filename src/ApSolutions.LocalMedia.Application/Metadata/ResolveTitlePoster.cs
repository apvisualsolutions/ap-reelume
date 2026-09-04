// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>
/// Turns what a title stores about its cover into the file that draws it, or nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>One field holds two different things,</b> and that is why this exists. A title's stored cover
/// is either an address a provider sent or the name of a file somebody picked from their own disk,
/// and the two are found in different folders. Asking only the first question is exactly why
/// choosing a cover once said «puesta» and changed nothing on screen.
/// </para>
/// <para>
/// <b>It lived inside the composition root until 2026-09-04, and it was the only copy.</b> Moving it
/// here is not tidiness: the library grid needs the same answer the detail cards need, and a second
/// copy of a rule this delicate is how the two would come to disagree. Nothing exercised the old one
/// — a grep of the tests for its name answered nothing at all — so the rule that decides whether a
/// person sees their own cover was carried by a private method no test could reach.
/// </para>
/// <para>
/// <b>The provider is asked first,</b> and that order is deliberate rather than incidental: a title
/// holding both draws today what it drew yesterday. A personal cover only ever reaches the field
/// with the lock set, and a locked field is one no refresh overwrites — so in practice the two never
/// compete. The day that stops being true, this is the one place that decides it.
/// </para>
/// </remarks>
public sealed class ResolveTitlePoster(IArtworkStore artwork)
{
    private readonly IArtworkStore _artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));

    /// <summary>
    /// The file drawing <paramref name="titleId"/>'s cover, or <see langword="null"/> when the
    /// stored value names neither a provider address nor a personal cover, or when it names one and
    /// the file is not on this disk.
    /// </summary>
    /// <remarks>
    /// A stored value that is neither shape answers nothing on purpose. That field is free text, and
    /// reading an arbitrary path out of it would turn a metadata editor into a reader of any file on
    /// the machine.
    /// </remarks>
    public string? Find(TitleId titleId, string? posterPath)
    {
        if (PosterAddressPolicy.TryBuildPosterAddress(posterPath) is { } address)
        {
            return _artwork.Find(titleId, new Uri(address, UriKind.Absolute));
        }

        return PersonalCoverPathPolicy.TryGetCoverFileName(posterPath) is { } cover
            ? _artwork.FindPersonal(titleId, cover)
            : null;
    }
}
