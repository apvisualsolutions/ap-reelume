// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public enum RenameExecutionOutcome
{
    Succeeded,
    PartiallyCompleted,
    Failed,
    BlockedByConflict,
    NotConfirmed,
    Undone,
    UnsafeToUndo,
    NothingToUndo,
}

public enum RenameAuditDirection
{
    Execute,
    Undo,
}

public enum RenameAuditStatus
{
    Started,
    Completed,
    Failed,
}

public sealed record RenameAuditEntry(
    long Id,
    Guid PlanId,
    int Sequence,
    RenameAuditDirection Direction,
    string SourcePath,
    string DestinationPath,
    RenameAuditStatus Status,
    DateTimeOffset OccurredAt,
    string? Error);

public sealed record RenameExecutionResult(
    RenameExecutionOutcome Outcome,
    RenamePlan Plan);

public interface ISafeFileRenamer
{
    Task<RenameExecutionResult> ExecuteAsync(
        RenamePlan plan,
        CancellationToken cancellationToken = default);

    Task<RenameExecutionResult> UndoAsync(
        RenamePlan plan,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RenameAuditEntry>> GetAuditLogAsync(
        Guid planId,
        CancellationToken cancellationToken = default);
}
