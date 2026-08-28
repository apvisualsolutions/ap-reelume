// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;

namespace ApSolutions.LocalMedia.Application.Identification;

/// <summary>
/// The scan coordinator the assembled application actually uses: every scan, whatever triggered it,
/// hands its summary to identification and then to version grouping before the summary is
/// returned. Keeping both hand-offs here means a watcher-triggered scan feeds the review inbox and
/// forms version groups exactly like a manual one — instead of either being a courtesy of
/// whichever caller remembered it.
/// </summary>
public sealed class IdentifyingScanCoordinator : IScanCoordinator
{
    private readonly IScanCoordinator _inner;
    private readonly Func<ReconcileScannedFiles> _reconciliation;
    private readonly Func<IdentifyScannedFiles> _identification;
    private readonly Func<GroupScannedVersions> _grouping;
    private readonly Func<GroupScannedEpisodes> _series;
    private readonly Func<NameScannedTitles> _naming;

    public IdentifyingScanCoordinator(
        IScanCoordinator inner,
        Func<ReconcileScannedFiles> reconciliation,
        Func<IdentifyScannedFiles> identification,
        Func<GroupScannedVersions> grouping,
        Func<GroupScannedEpisodes> series,
        Func<NameScannedTitles> naming)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _reconciliation = reconciliation ?? throw new ArgumentNullException(nameof(reconciliation));
        _identification = identification ?? throw new ArgumentNullException(nameof(identification));
        _grouping = grouping ?? throw new ArgumentNullException(nameof(grouping));
        _series = series ?? throw new ArgumentNullException(nameof(series));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    public async Task<ScanSummary> StartAsync(
        StartScanCommand command,
        CancellationToken cancellationToken = default)
    {
        var summary = await _inner.StartAsync(command, cancellationToken).ConfigureAwait(false);

        // Reconciliation runs first: a moved file has to become its old entity again before
        // identification spends effort on it as a stranger. All three use cases already refuse to
        // replace anybody's decisions, skip a cancelled scan, and hold what needs a person instead
        // of throwing, so the summary always comes back to the caller the scan belongs to.
        _ = await _reconciliation().ExecuteAsync(summary, cancellationToken).ConfigureAwait(false);
        _ = await _identification().ExecuteAsync(summary, cancellationToken).ConfigureAwait(false);
        _ = await _grouping().ExecuteAsync(summary, cancellationToken).ConfigureAwait(false);

        // And last, the folders that are series. It runs after identification so a show a
        // person identified keeps what they said: this one skips every file an identified
        // title already claims, and writes under an identifier derived from the folder,
        // which can never collide with the one identification uses.
        _ = await _series().ExecuteAsync(summary, cancellationToken).ConfigureAwait(false);

        // Then what is left over gets its name read rather than copied. After the series pass on
        // purpose: this one skips episodes, and a file only becomes an episode once that pass has
        // said so. Everything else — the films, and whatever the parser cannot place — keeps a card,
        // and until 2026-08-28 that card said the file name with the year still inside it.
        _ = await _naming().ExecuteAsync(summary, cancellationToken).ConfigureAwait(false);
        return summary;
    }
}
