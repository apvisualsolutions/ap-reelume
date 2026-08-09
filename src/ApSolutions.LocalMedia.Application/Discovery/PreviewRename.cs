using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record PreviewRenameCommand(
    string RootPath,
    IReadOnlyList<RenameRequest> Requests);

public sealed class PreviewRename(RenamePolicy policy)
{
    public RenamePlan Execute(PreviewRenameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return policy.CreatePlan(command.RootPath, command.Requests);
    }
}
