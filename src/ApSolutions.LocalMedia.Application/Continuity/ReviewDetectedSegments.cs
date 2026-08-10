// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// The human side of detection: list what was found for an episode, accept it, adjust it, or remove
/// it. Accepting or adjusting locks the row so no later run overwrites a person's judgement.
/// </summary>
public sealed class ReviewDetectedSegments
{
    private readonly IDetectedMarkerRepository _repository;

    public ReviewDetectedSegments(IDetectedMarkerRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public Task<IReadOnlyList<DetectedMarker>> ListForFileAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default) =>
        _repository.GetForFileAsync(fileId, cancellationToken);

    /// <summary>Confirms the range as detected; returns the locked row, or null when it is gone.</summary>
    public async Task<DetectedMarker?> AcceptAsync(Guid markerId, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(markerId, cancellationToken).ConfigureAwait(false) is not { } row)
        {
            return null;
        }

        var accepted = SegmentDetectionPolicy.Accept(row);
        await _repository.SaveAsync(accepted, cancellationToken).ConfigureAwait(false);
        return accepted;
    }

    /// <summary>Adjusts the range; returns the corrected row, or null when the range is invalid or the row is gone.</summary>
    public async Task<DetectedMarker?> CorrectAsync(
        Guid markerId,
        TimeSpan start,
        TimeSpan end,
        TimeSpan? duration,
        CancellationToken cancellationToken = default)
    {
        if (await FindAsync(markerId, cancellationToken).ConfigureAwait(false) is not { } row)
        {
            return null;
        }

        if (SegmentDetectionPolicy.Correct(row, start, end, duration) is not { } corrected)
        {
            return null;
        }

        await _repository.SaveAsync(corrected, cancellationToken).ConfigureAwait(false);
        return corrected;
    }

    /// <summary>Removes the row; a later run may legitimately find the range again.</summary>
    public async Task<bool> DeleteAsync(Guid markerId, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(markerId, cancellationToken).ConfigureAwait(false) is null)
        {
            return false;
        }

        await _repository.DeleteAsync(markerId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private Task<DetectedMarker?> FindAsync(Guid markerId, CancellationToken cancellationToken) =>
        _repository.GetAsync(markerId, cancellationToken);
}
