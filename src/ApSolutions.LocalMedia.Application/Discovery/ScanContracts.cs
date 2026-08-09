using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Discovery;

public enum ScanTrigger
{
    Initial,
    Startup,
    Manual,
    Watcher,
    Recovery,
}

public enum ScanItemOutcome
{
    Added,
    Updated,
    Unchanged,
    Skipped,
    Failed,
}

public sealed record StartScanCommand(
    LibraryRootId RootId,
    ScanTrigger Trigger = ScanTrigger.Manual,
    int BatchSize = 128);

public sealed record ScanItemResult(string Path, ScanItemOutcome Outcome, string? ErrorCode = null);

public sealed record ScanSummary(
    LibraryRootId RootId,
    int EnumeratedCount,
    int MediaCount,
    int ProbeCount,
    int UnchangedCount,
    int ErrorCount,
    bool IsCancelled,
    string? ResumeAfterPath,
    IReadOnlyList<ScanItemResult> Results,
    TimeSpan MaxEventDispatchDuration);

public interface IScanCoordinator
{
    Task<ScanSummary> StartAsync(
        StartScanCommand command,
        CancellationToken cancellationToken = default);
}
