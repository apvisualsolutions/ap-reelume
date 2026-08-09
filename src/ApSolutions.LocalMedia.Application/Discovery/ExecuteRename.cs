using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record ExecuteRenameCommand(RenamePlan Plan, bool Confirmed);

public sealed class ExecuteRename(ISafeFileRenamer safeFileRenamer)
{
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
            ? safeFileRenamer.ExecuteAsync(command.Plan, cancellationToken)
            : Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.BlockedByConflict, command.Plan));
    }
}
