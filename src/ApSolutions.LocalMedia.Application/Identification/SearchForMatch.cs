// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Identification;

/// <summary>
/// Identification asked for by a person, in their own words, for one file they are looking at.
/// </summary>
/// <remarks>
/// <para>
/// It is the manual counterpart of <see cref="IdentifyScannedFiles"/> and behaves the same way on
/// purpose: the words are read the way a file name is read, the candidates that come back replace
/// the ones the file had, and a candidate the scorer trusts on its own is applied without asking.
/// A person who searched and got an obvious answer should not then have to confirm it, and one who
/// got a doubtful answer should find it waiting in the inbox.
/// </para>
/// <para>
/// The review inbox's Search button raised an event nothing in the application listened to, so the
/// press was answered by a search that never happened — this repository's characteristic defect
/// wearing an event rather than a registration. This is what the button reaches now.
/// </para>
/// </remarks>
public sealed class SearchForMatch
{
    private readonly IdentifyMediaFile _identify;
    private readonly ApplyIdentification _applyIdentification;

    public SearchForMatch(IdentifyMediaFile identify, ApplyIdentification applyIdentification)
    {
        _identify = identify ?? throw new ArgumentNullException(nameof(identify));
        _applyIdentification = applyIdentification ?? throw new ArgumentNullException(nameof(applyIdentification));
    }

    /// <summary>
    /// Searches for what was typed and answers with the candidates the file now has.
    /// </summary>
    public async Task<IReadOnlyList<MatchCandidate>> ExecuteAsync(
        MediaFileId mediaFileId,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // Typed words carry no folders around them, so the parser is given the words alone: whatever
        // a person meant is in what they wrote, not in where the file happens to live.
        var result = await _identify.ExecuteAsync(
            new IdentifyMediaFileCommand(mediaFileId, new FileNameContext(text.Trim(), [])),
            cancellationToken).ConfigureAwait(false);

        // Only one candidate can ever reach the automatic state: it takes a score no second entry
        // can also hold.
        if (result.Candidates.FirstOrDefault(candidate => candidate.ReviewState == ReviewState.Automatic)
            is { } confident)
        {
            _ = await _applyIdentification.ExecuteAsync(
                new ApplyIdentificationCommand(
                    mediaFileId,
                    confident.StableKey,
                    ResolveMatch.ToMetadataKind(confident.Kind)),
                cancellationToken).ConfigureAwait(false);
        }

        return result.Candidates;
    }
}
