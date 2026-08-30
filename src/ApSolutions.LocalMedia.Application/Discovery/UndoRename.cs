// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record UndoRenameCommand(RenamePlan Plan, bool Confirmed);

public sealed class UndoRename
{
    private readonly ISafeFileRenamer _safeFileRenamer;

    public UndoRename(ISafeFileRenamer safeFileRenamer) =>
        _safeFileRenamer = safeFileRenamer ?? throw new ArgumentNullException(nameof(safeFileRenamer));

    public Task<RenameExecutionResult> ExecuteAsync(
        UndoRenameCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirmed)
        {
            return Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.NotConfirmed, command.Plan));
        }

        return _safeFileRenamer.UndoAsync(command.Plan, cancellationToken);
    }
}
