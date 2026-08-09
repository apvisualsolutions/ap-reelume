using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record UndoRenameCommand(RenamePlan Plan, bool Confirmed);

public sealed class UndoRename(ISafeFileRenamer safeFileRenamer)
{
    public Task<RenameExecutionResult> ExecuteAsync(
        UndoRenameCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirmed)
        {
            return Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.NotConfirmed, command.Plan));
        }

        return safeFileRenamer.UndoAsync(command.Plan, cancellationToken);
    }
}
