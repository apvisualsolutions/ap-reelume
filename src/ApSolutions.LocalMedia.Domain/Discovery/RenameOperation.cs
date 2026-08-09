namespace ApSolutions.LocalMedia.Domain.Discovery;

public sealed record RenameRequest(string SourcePath, string DesiredFileName);

public enum RenameOperationStatus
{
    Planned,
    Completed,
    Failed,
    Undone,
}

public sealed record RenameOperation(
    int Sequence,
    string SourcePath,
    string DestinationPath,
    RenameOperationStatus Status = RenameOperationStatus.Planned,
    string? Error = null)
{
    public bool IsCaseOnlyChange =>
        !string.Equals(SourcePath, DestinationPath, StringComparison.Ordinal)
        && string.Equals(SourcePath, DestinationPath, StringComparison.OrdinalIgnoreCase);
}

public enum RenameConflictKind
{
    InvalidPath,
    SourceOutsideRoot,
    DestinationOutsideRoot,
    FolderMove,
    PathTooLong,
    DuplicateDestination,
    NoChange,
    SourceMissing,
    DestinationExists,
    UnsafeState,
}

public sealed record RenameConflict(
    int RequestIndex,
    RenameConflictKind Kind,
    string Detail);

public sealed record RenamePlan(
    Guid Id,
    string RootPath,
    IReadOnlyList<RenameOperation> Operations,
    IReadOnlyList<RenameConflict> Conflicts,
    bool CanUndo = false)
{
    public bool CanExecute => Operations.Count > 0 && Conflicts.Count == 0;
}
