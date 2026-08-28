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
    /// Fetches the artwork and answers where it was put, or <see langword="null"/> when it could not
    /// be had. A title without artwork is an ordinary state and not a failure to report.
    /// </summary>
    Task<string?> FetchAsync(
        TitleId titleId,
        Uri source,
        string alternativeText,
        CancellationToken cancellationToken = default);
}
