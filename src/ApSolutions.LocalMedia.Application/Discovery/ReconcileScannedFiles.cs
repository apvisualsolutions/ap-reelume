using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record ReconcileScannedFilesResult(
    int AttemptedCount,
    int ReassignedCount,
    int HeldCount,
    int FailedCount);

/// <summary>
/// Runs reconciliation over what a scan catalogued, so a moved file keeps being the entity it was
/// (LIB-002/003). This is the caller the audit found missing: <c>ReconcileScanResults</c> was
/// registered and tested, scans discovered moved files as strangers, and nothing joined the two.
/// </summary>
/// <remarks>
/// The rules are conservative on purpose. A discovered file whose identity matches a cataloged row
/// is reassigned only when the match is exact and the old file is really gone from this scan; a
/// match whose old file was seen in the same scan is a copy, which version grouping owns, not a
/// move. Anything that would need a person is held for the review inbox and keeps no stored
/// identity, so every later scan re-derives the offer until somebody decides. A file whose
/// identity cannot be read is counted and skipped: reconciliation must never be the reason a
/// scan's caller sees a failure.
/// </remarks>
public sealed class ReconcileScannedFiles
{
    private readonly IMediaFileRepository _mediaFiles;
    private readonly ReconcileScanResults _reconcile;
    private readonly IFileIdentityProvider _identityProvider;
    private readonly FileReconciliationPolicy _policy;
    private readonly PendingReassignments _pending;

    public ReconcileScannedFiles(
        IMediaFileRepository mediaFiles,
        ReconcileScanResults reconcile,
        IFileIdentityProvider identityProvider,
        FileReconciliationPolicy policy,
        PendingReassignments pending)
    {
        _mediaFiles = mediaFiles ?? throw new ArgumentNullException(nameof(mediaFiles));
        _reconcile = reconcile ?? throw new ArgumentNullException(nameof(reconcile));
        _identityProvider = identityProvider ?? throw new ArgumentNullException(nameof(identityProvider));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _pending = pending ?? throw new ArgumentNullException(nameof(pending));
    }

    public async Task<ReconcileScannedFilesResult> ExecuteAsync(
        ScanSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.IsCancelled)
        {
            return new ReconcileScannedFilesResult(0, 0, 0, 0);
        }

        // Whatever this scan saw still exists on disk: a match against one of these paths is a
        // copy of a file that is still there, never a move.
        var seenPaths = summary.Results
            .Where(item => item.Outcome is not ScanItemOutcome.Failed)
            .Select(item => item.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var attempted = 0;
        var reassigned = 0;
        var held = 0;
        var failed = 0;
        foreach (var item in summary.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Outcome is not (ScanItemOutcome.Added or ScanItemOutcome.Updated or ScanItemOutcome.Unchanged))
            {
                continue;
            }

            try
            {
                var row = await _mediaFiles.FindByPathAsync(summary.RootId, item.Path, cancellationToken)
                    .ConfigureAwait(false);
                if (row is null)
                {
                    continue;
                }

                // A row that already carries an identity is settled: either it is its own file, or
                // an earlier reconciliation already decided. Updated content gets its identity
                // refreshed, because the old fingerprint described bytes that no longer exist.
                var stored = await _mediaFiles.GetIdentityAsync(row.Id, cancellationToken).ConfigureAwait(false);
                if (stored is not null && item.Outcome is not ScanItemOutcome.Updated)
                {
                    continue;
                }

                attempted++;
                var identity = await _identityProvider
                    .GetAsync(item.Path, row.TechnicalMetadata, cancellationToken)
                    .ConfigureAwait(false);
                if (!identity.HasStableFileId && !identity.HasFingerprint)
                {
                    failed++;
                    continue;
                }

                if (item.Outcome is ScanItemOutcome.Updated)
                {
                    await _mediaFiles.SaveIdentityAsync(row.Id, identity, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var candidates = await FindCandidatesAsync(row.Id, identity, cancellationToken).ConfigureAwait(false);
                if (candidates.Count == 0
                    || candidates.Any(candidate => seenPaths.Contains(candidate.MediaFile.Path)))
                {
                    // Nothing to be, or the matching file is still where it was: this row is its
                    // own entity, and its identity is what lets a future move find it again.
                    await _mediaFiles.SaveIdentityAsync(row.Id, identity, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var command = new ReconcileFileCommand(summary.RootId, item.Path, identity);
                var assessment = _policy.Assess(
                    candidates[0].Identity,
                    identity,
                    candidates.Count,
                    sourceAvailable: true);
                if (assessment.Decision == ReconciliationDecision.Exact && candidates.Count == 1)
                {
                    _ = await _reconcile.ReconcileAsync(command, cancellationToken).ConfigureAwait(false);
                    _pending.Remove(item.Path);
                    reassigned++;
                    continue;
                }

                _pending.Offer(new PendingReassignment(
                    command,
                    [.. candidates.Select(candidate =>
                        new ReassignmentCandidate(candidate.MediaFile.Id, candidate.MediaFile.Path))]));
                held++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failed++;
            }
        }

        return new ReconcileScannedFilesResult(attempted, reassigned, held, failed);
    }

    /// <summary>
    /// Keeps a held file as its own entity: its identity is stored, so no later scan offers the
    /// reassignment again.
    /// </summary>
    public async Task KeepAsNewAsync(
        PendingReassignment item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var row = await _mediaFiles
            .FindByPathAsync(item.Command.LibraryRootId, item.Command.NewPath, cancellationToken)
            .ConfigureAwait(false);
        if (row is not null)
        {
            await _mediaFiles.SaveIdentityAsync(row.Id, item.Command.Identity, cancellationToken)
                .ConfigureAwait(false);
        }

        _pending.Remove(item.Command.NewPath);
    }

    private async Task<IReadOnlyList<IdentifiedMediaFile>> FindCandidatesAsync(
        MediaFileId self,
        FileIdentity identity,
        CancellationToken cancellationToken)
    {
        if (identity.HasStableFileId)
        {
            var stable = await _mediaFiles.FindByStableIdentityAsync(identity, cancellationToken)
                .ConfigureAwait(false);
            if (stable is not null && stable.MediaFile.Id != self)
            {
                return [stable];
            }
        }

        if (!identity.HasFingerprint)
        {
            return [];
        }

        var printed = await _mediaFiles.FindByFingerprintAsync(identity.Fingerprint!, cancellationToken)
            .ConfigureAwait(false);
        return [.. printed.Where(candidate => candidate.MediaFile.Id != self)];
    }
}
