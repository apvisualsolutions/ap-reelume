// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed class ManualReassignmentViewModel
{
    private readonly ReconcileScanResults _reconciliation;

    public ManualReassignmentViewModel(ReconcileScanResults reconciliation)
    {
        _reconciliation = reconciliation ?? throw new ArgumentNullException(nameof(reconciliation));
    }

    public ReconcileFileCommand? PendingCommand { get; private set; }

    public MediaFileId? CandidateId { get; private set; }

    public bool CanConfirm => PendingCommand is not null && CandidateId is not null;

    public void Review(ReconcileFileCommand command, MediaFileId candidateId)
    {
        PendingCommand = command ?? throw new ArgumentNullException(nameof(command));
        CandidateId = candidateId;
    }

    public async Task<ReconcileFileResult> ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (PendingCommand is null || CandidateId is null)
        {
            throw new InvalidOperationException("No reassignment candidate is pending confirmation.");
        }

        var result = await _reconciliation
            .ConfirmAsync(PendingCommand, CandidateId.Value, cancellationToken)
            .ConfigureAwait(false);
        PendingCommand = null;
        CandidateId = null;
        return result;
    }
}
