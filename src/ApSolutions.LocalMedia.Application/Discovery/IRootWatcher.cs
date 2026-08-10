// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public enum FileChangeKind
{
    Created,
    Changed,
    Renamed,
    Deleted,
}

public sealed record FileChange(
    FileChangeKind Kind,
    string Path,
    string? PreviousPath = null);

public sealed record FileChangeBatch(
    LibraryRootId RootId,
    IReadOnlyList<FileChange> Changes,
    DateTimeOffset ObservedUtc);

public interface IRootWatcher
{
    IAsyncEnumerable<FileChangeBatch> StartAsync(
        LibraryRoot root,
        CancellationToken cancellationToken = default);
}

public interface IFallbackScanScheduler
{
    IAsyncEnumerable<ScanTrigger> ScheduleAsync(
        LibraryRoot root,
        CancellationToken cancellationToken = default);
}
