// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record PreviewRenameCommand(
    string RootPath,
    IReadOnlyList<RenameRequest> Requests);

public sealed class PreviewRename
{
    private readonly RenamePolicy _policy;

    public PreviewRename(RenamePolicy policy) =>
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public RenamePlan Execute(PreviewRenameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _policy.CreatePlan(command.RootPath, command.Requests);
    }
}
