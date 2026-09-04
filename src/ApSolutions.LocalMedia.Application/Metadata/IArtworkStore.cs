// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>
/// Where a title's artwork lives on this disk, and what fetches it there.
/// </summary>
/// <remarks>
/// Two members and they are deliberately asymmetric: <see cref="Find"/> only ever looks at the disk,
/// so a surface that draws a poster can ask it without opening a connection. Everything that reaches
/// the network is behind <see cref="FetchAsync"/>, which is called from the one place a person has
/// already consented to talk to the provider — applying an identification.
/// </remarks>
public interface IArtworkStore
{
    /// <summary>
    /// The local file holding this title's artwork for that address, or <see langword="null"/> when
    /// nothing has been fetched. Touches the disk and never the network.
    /// </summary>
    string? Find(TitleId titleId, Uri source);

    /// <summary>
    /// The local file holding the cover this person chose for this title, or <see langword="null"/>
    /// when there is none by that name. Touches the disk and never the network.
    /// </summary>
    /// <remarks>
    /// <b>It takes a name and not a path, and that is the point.</b> The value the poster field
    /// holds was written by whoever last edited the title — or by a provider, or by a backup made on
    /// another machine — so the directory it names is not this application's to trust or even to
    /// use. The caller keeps only the name, which <c>PersonalCoverPathPolicy</c> has agreed to, and
    /// the folder is composed here out of the application's own data root and the title asked for.
    /// A cover restored onto a different machine is found for exactly that reason.
    /// </remarks>
    string? FindPersonal(TitleId titleId, string coverFileName);

    /// <summary>
    /// Fetches the artwork and answers where it was put, or <see langword="null"/> when it could not
    /// be had. A title without artwork is an ordinary state and not a failure to report.
    /// </summary>
    Task<string?> FetchAsync(
        TitleId titleId,
        Uri source,
        string alternativeText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a file the person chose into this title's own artwork, and answers where it landed.
    /// Touches the disk and never the network.
    /// </summary>
    /// <remarks>
    /// <b>It was on the adapter and not on this port until 2026-09-03, which is why nothing could
    /// call it.</b> The adapter had implemented it, the backup carried what it wrote and the picker
    /// had a property to hold the answer — and the only callers were tests, because a use case can
    /// only reach what the port declares. A method missing from an interface is this repository's
    /// characteristic defect at its quietest: nothing is unresolved, nothing is unregistered, and
    /// there is simply no way through.
    /// </remarks>
    Task<ArtworkReference> ImportPersonalAsync(
        TitleId titleId,
        string sourcePath,
        string alternativeText,
        CancellationToken cancellationToken = default);
}
