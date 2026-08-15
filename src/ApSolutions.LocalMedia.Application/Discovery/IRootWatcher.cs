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

/// <summary>
/// What the watcher saw. <paramref name="EventsLost"/> says the operating system dropped changes
/// the watcher never got to see — it means "I have lost events", never "I cannot go on", so the
/// batch that carries it is a request to rescan and not the end of the watching.
/// </summary>
public sealed record FileChangeBatch(
    LibraryRootId RootId,
    IReadOnlyList<FileChange> Changes,
    DateTimeOffset ObservedUtc,
    bool EventsLost = false);

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
