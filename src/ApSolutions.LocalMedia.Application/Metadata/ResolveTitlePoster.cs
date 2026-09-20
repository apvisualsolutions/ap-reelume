// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using ApSolutions.LocalMedia.Application.Storage;
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
/// <b>The order is <see cref="CoverOrderPolicy"/>'s, and since 2026-09-18 the picked cover wins</b>
/// (LIB-021, ADR-0009). Until then the provider was asked first and the two shared one field, which
/// is how choosing a cover overwrote the provider's and restoring the provider's fields overwrote the
/// choice. Now each has its own field, and this walks the order: the first origin with a file on
/// disk draws, so a picked file that went missing falls through to the provider's.
/// </para>
/// <para>
/// <b>Which order that is stopped being fixed on 2026-09-20</b> (ADR-0009 decision 4). A title that
/// carries one of its own is walked by it; every other title follows the general setting, asked for
/// here rather than at the two call sites, so the rule that decides which picture a person sees
/// stays in one place.
/// </para>
/// </remarks>
public sealed class ResolveTitlePoster(IArtworkStore artwork, IAppDataPaths paths, ICoverOrderSettings coverOrder)
{
    private readonly IArtworkStore _artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));
    private readonly IAppDataPaths _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    private readonly ICoverOrderSettings _coverOrder = coverOrder ?? throw new ArgumentNullException(nameof(coverOrder));

    /// <summary>
    /// Where the frame taken from <paramref name="titleId"/>'s own video lives, whether or not it has
    /// been taken yet. Named by the title's id, so a renamed file keeps its picture.
    /// </summary>
    public static string FrameFileFor(IAppDataPaths paths, TitleId titleId)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.TitleFrameDirectory, titleId.Value.ToString("N", CultureInfo.InvariantCulture) + ".png");
    }

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
    /// <param name="personalCover">
    /// The picked cover's own field (LIB-021). <paramref name="posterPath"/> is still read as a
    /// personal cover too, for a row stored before the field existed that nothing has re-saved yet.
    /// </param>
    /// <param name="coverOrder">
    /// The order this one title overrides the general one with, as its column holds it, or
    /// <see langword="null"/> to follow the general setting. Text that names no origins is refused
    /// and the general order decides, so a row written by hand cannot leave a card empty.
    /// </param>
    public string? Find(TitleId titleId, string? posterPath, string? personalCover = null, string? coverOrder = null)
    {
        foreach (var origin in CoverOrderPolicy.TryParse(coverOrder, out var forThisTitle)
            ? forThisTitle
            : _coverOrder.Current)
        {
            var file = origin switch
            {
                CoverOrigin.Personal => FindPersonal(titleId, personalCover) ?? FindPersonal(titleId, posterPath),
                CoverOrigin.Provider => FindProvider(titleId, posterPath),
                _ => FindFrame(titleId),
            };
            if (file is not null)
            {
                return file;
            }
        }

        return null;
    }

    private string? FindPersonal(TitleId titleId, string? value) =>
        PersonalCoverPathPolicy.TryGetCoverFileName(value) is { } cover
            ? _artwork.FindPersonal(titleId, cover)
            : null;

    // Finding, never taking: a frame that is not on disk yet is no picture. Taking it is the
    // background pass's work, because decoding can take seconds and this is asked while a grid paints.
    private string? FindFrame(TitleId titleId) =>
        FrameFileFor(_paths, titleId) is var frame && File.Exists(frame) ? frame : null;

    private string? FindProvider(TitleId titleId, string? posterPath) =>
        PosterAddressPolicy.TryBuildPosterAddress(posterPath) is { } address
            ? _artwork.Find(titleId, new Uri(address, UriKind.Absolute))
            : null;
}
