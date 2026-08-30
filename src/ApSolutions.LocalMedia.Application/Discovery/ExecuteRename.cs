// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record ExecuteRenameCommand(RenamePlan Plan, bool Confirmed);

public sealed class ExecuteRename
{
    private readonly ISafeFileRenamer _safeFileRenamer;

    public ExecuteRename(ISafeFileRenamer safeFileRenamer) =>
        _safeFileRenamer = safeFileRenamer ?? throw new ArgumentNullException(nameof(safeFileRenamer));

    public Task<RenameExecutionResult> ExecuteAsync(
        ExecuteRenameCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirmed)
        {
            return Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.NotConfirmed, command.Plan));
        }

        return command.Plan.CanExecute
            ? _safeFileRenamer.ExecuteAsync(command.Plan, cancellationToken)
            : Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.BlockedByConflict, command.Plan));
    }
}
