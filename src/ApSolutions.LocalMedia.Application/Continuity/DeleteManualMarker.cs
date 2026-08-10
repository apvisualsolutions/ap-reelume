// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// Removes a range a person created. It refuses to remove anything that did not come from a person,
/// so a later release that adds detection cannot lose its own data through this path.
/// </summary>
public sealed class DeleteManualMarker
{
    private readonly IIntroMarkerRepository _repository;

    public DeleteManualMarker(IIntroMarkerRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>True when a manual range was removed; false when there was nothing to remove.</summary>
    public async Task<bool> ExecuteAsync(Guid markerId, CancellationToken cancellationToken = default)
    {
        var marker = await _repository.GetAsync(markerId, cancellationToken).ConfigureAwait(false);
        if (marker is not { Origin: MarkerOrigin.Manual })
        {
            return false;
        }

        await _repository.DeleteAsync(markerId, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
