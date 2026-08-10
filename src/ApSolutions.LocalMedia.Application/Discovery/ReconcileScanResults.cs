// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record ReconcileFileCommand(
    LibraryRootId LibraryRootId,
    string NewPath,
    FileIdentity Identity);

public sealed record ReconcileFileResult(
    ReconciliationDecision Decision,
    MediaFileId? MediaFileId,
    bool RequiresConfirmation,
    bool PreservesProgress);

public sealed class ReconcileScanResults
{
    private readonly IMediaFileRepository _mediaFileRepository;
    private readonly FileReconciliationPolicy _policy;

    public ReconcileScanResults(
        IMediaFileRepository mediaFileRepository,
        FileReconciliationPolicy policy)
    {
        _mediaFileRepository = mediaFileRepository ?? throw new ArgumentNullException(nameof(mediaFileRepository));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async Task<ReconcileFileResult> ReconcileAsync(
        ReconcileFileCommand command,
        CancellationToken cancellationToken = default)
    {
        Validate(command);
        if (command.Identity.HasStableFileId)
        {
            var candidate = await _mediaFileRepository
                .FindByStableIdentityAsync(command.Identity, cancellationToken)
                .ConfigureAwait(false);
            if (candidate is not null)
            {
                return await ReassignExactAsync(command, candidate, cancellationToken).ConfigureAwait(false);
            }
        }

        if (command.Identity.HasFingerprint)
        {
            var candidates = await _mediaFileRepository
                .FindByFingerprintAsync(command.Identity.Fingerprint!, cancellationToken)
                .ConfigureAwait(false);
            if (candidates.Count == 1)
            {
                return await ReassignExactAsync(command, candidates[0], cancellationToken).ConfigureAwait(false);
            }

            if (candidates.Count > 1)
            {
                var probable = _policy.Assess(
                    candidates[0].Identity,
                    command.Identity,
                    candidates.Count,
                    sourceAvailable: true);
                return ToResult(probable, null);
            }
        }

        return ToResult(_policy.Assess(null, command.Identity, 0, sourceAvailable: true), null);
    }

    public async Task<ReconcileFileResult> ConfirmAsync(
        ReconcileFileCommand command,
        MediaFileId confirmedMediaFileId,
        CancellationToken cancellationToken = default)
    {
        Validate(command);
        IdentifiedMediaFile? confirmed = null;
        if (command.Identity.HasStableFileId)
        {
            var stable = await _mediaFileRepository
                .FindByStableIdentityAsync(command.Identity, cancellationToken)
                .ConfigureAwait(false);
            if (stable?.MediaFile.Id == confirmedMediaFileId)
            {
                confirmed = stable;
            }
        }

        if (confirmed is null && command.Identity.HasFingerprint)
        {
            var candidates = await _mediaFileRepository
                .FindByFingerprintAsync(command.Identity.Fingerprint!, cancellationToken)
                .ConfigureAwait(false);
            confirmed = candidates.SingleOrDefault(candidate => candidate.MediaFile.Id == confirmedMediaFileId);
        }

        if (confirmed is null)
        {
            throw new InvalidOperationException("The confirmed media file does not match the discovered identity.");
        }

        await AbsorbOccupantAsync(command, confirmedMediaFileId, cancellationToken).ConfigureAwait(false);
        await _mediaFileRepository.ReassignAsync(
            confirmedMediaFileId,
            command.LibraryRootId,
            command.NewPath,
            command.Identity,
            cancellationToken).ConfigureAwait(false);
        return new ReconcileFileResult(
            ReconciliationDecision.Exact,
            confirmedMediaFileId,
            RequiresConfirmation: false,
            PreservesProgress: true);
    }

    public Task MarkRootUnavailableAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default) =>
        _mediaFileRepository.SetRootAvailabilityAsync(rootId, isAvailable: false, cancellationToken);

    private async Task<ReconcileFileResult> ReassignExactAsync(
        ReconcileFileCommand command,
        IdentifiedMediaFile candidate,
        CancellationToken cancellationToken)
    {
        var assessment = _policy.Assess(
            candidate.Identity,
            command.Identity,
            matchingFingerprintCandidates: 1,
            sourceAvailable: true);
        if (assessment.Decision != ReconciliationDecision.Exact)
        {
            return ToResult(assessment, null);
        }

        await AbsorbOccupantAsync(command, candidate.MediaFile.Id, cancellationToken).ConfigureAwait(false);
        await _mediaFileRepository.ReassignAsync(
            candidate.MediaFile.Id,
            command.LibraryRootId,
            command.NewPath,
            command.Identity,
            cancellationToken).ConfigureAwait(false);
        return ToResult(assessment, candidate.MediaFile.Id);
    }

    /// <summary>
    /// A scan that discovered the moved file already catalogued its new path as a stranger. That
    /// row is this file wearing a fresh id, so it is absorbed before the real entity takes the
    /// path — otherwise the reassignment would leave two rows claiming one file.
    /// </summary>
    private async Task AbsorbOccupantAsync(
        ReconcileFileCommand command,
        MediaFileId reassigned,
        CancellationToken cancellationToken)
    {
        var occupant = await _mediaFileRepository
            .FindByPathAsync(command.LibraryRootId, command.NewPath, cancellationToken)
            .ConfigureAwait(false);
        if (occupant is not null && occupant.Id != reassigned)
        {
            await _mediaFileRepository.RemoveAsync(occupant.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Validate(ReconcileFileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.NewPath);
        if (!command.Identity.HasStableFileId && !command.Identity.HasFingerprint)
        {
            throw new ArgumentException("A stable file id or fingerprint is required.", nameof(command));
        }
    }

    private static ReconcileFileResult ToResult(
        ReconciliationAssessment assessment,
        MediaFileId? mediaFileId) => new(
            assessment.Decision,
            mediaFileId,
            assessment.RequiresConfirmation,
            assessment.PreservesEntityIdentity);
}
