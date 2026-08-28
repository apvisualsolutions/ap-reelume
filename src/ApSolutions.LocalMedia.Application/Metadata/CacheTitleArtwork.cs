// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>
/// Puts a freshly identified title's poster on this disk, once.
/// </summary>
/// <remarks>
/// <para>
/// A use case of its own rather than four lines inside <see cref="Identification.ApplyIdentification"/>,
/// and the reason is the same one that put that class beside <c>ResolveMatch</c> instead of inside
/// it: the decisions here — is there a path, does it compose into an address, is it already on the
/// disk, and what does a failed fetch mean — are four branches that belong somewhere they can be
/// read and asserted, not folded into a method whose subject is the metadata row.
/// </para>
/// <para>
/// <b>Nothing here is fatal.</b> Artwork is decoration over a card that already says everything it
/// needs to in words: a title identified with no poster fetched is a title identified. That is why
/// the answer is a path or nothing rather than a result with an outcome.
/// </para>
/// </remarks>
public sealed class CacheTitleArtwork(IArtworkStore store)
{
    private readonly IArtworkStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Makes sure this title's poster is on the disk, and answers where. Fetches at most once: an
    /// address already cached is answered from the disk without a connection.
    /// </summary>
    public async Task<string?> ExecuteAsync(
        TitleId titleId,
        string? posterPath,
        string alternativeText,
        CancellationToken cancellationToken = default)
    {
        if (PosterAddressPolicy.TryBuildPosterAddress(posterPath) is not { } address)
        {
            return null;
        }

        var source = new Uri(address, UriKind.Absolute);
        return _store.Find(titleId, source) ?? await _store
            .FetchAsync(titleId, source, alternativeText, cancellationToken)
            .ConfigureAwait(false);
    }
}
