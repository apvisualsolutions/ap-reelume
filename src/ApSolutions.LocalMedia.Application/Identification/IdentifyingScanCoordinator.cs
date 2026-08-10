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

    public IdentifyingScanCoordinator(
        IScanCoordinator inner,
        Func<ReconcileScannedFiles> reconciliation,
        Func<IdentifyScannedFiles> identification,
        Func<GroupScannedVersions> grouping)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _reconciliation = reconciliation ?? throw new ArgumentNullException(nameof(reconciliation));
        _identification = identification ?? throw new ArgumentNullException(nameof(identification));
        _grouping = grouping ?? throw new ArgumentNullException(nameof(grouping));
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
        return summary;
    }
}
