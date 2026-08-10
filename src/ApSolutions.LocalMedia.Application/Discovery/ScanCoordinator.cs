// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed class ScanCoordinator : IScanCoordinator
{
    private readonly ConcurrentDictionary<LibraryRootId, SemaphoreSlim> _rootLocks = new();
    private readonly ILibraryRootRepository _rootRepository;
    private readonly IMediaFileRepository _mediaFileRepository;
    private readonly IMediaFileEnumerator _mediaFileEnumerator;
    private readonly IMediaProbe _mediaProbe;
    private readonly IApplicationEventPublisher _eventPublisher;

    public ScanCoordinator(
        ILibraryRootRepository rootRepository,
        IMediaFileRepository mediaFileRepository,
        IMediaFileEnumerator mediaFileEnumerator,
        IMediaProbe mediaProbe,
        IApplicationEventPublisher eventPublisher)
    {
        _rootRepository = rootRepository ?? throw new ArgumentNullException(nameof(rootRepository));
        _mediaFileRepository = mediaFileRepository ?? throw new ArgumentNullException(nameof(mediaFileRepository));
        _mediaFileEnumerator = mediaFileEnumerator ?? throw new ArgumentNullException(nameof(mediaFileEnumerator));
        _mediaProbe = mediaProbe ?? throw new ArgumentNullException(nameof(mediaProbe));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<ScanSummary> StartAsync(
        StartScanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.BatchSize);
        var rootLock = _rootLocks.GetOrAdd(command.RootId, static _ => new SemaphoreSlim(1, 1));
        await rootLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ScanRootAsync(command, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            rootLock.Release();
        }
    }

    private async Task<ScanSummary> ScanRootAsync(
        StartScanCommand command,
        CancellationToken cancellationToken)
    {
        var root = await _rootRepository.GetAsync(command.RootId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Library root '{command.RootId.Value:D}' was not found.");
        var checkpoint = await _mediaFileRepository
            .GetScanCheckpointAsync(command.RootId, cancellationToken)
            .ConfigureAwait(false);
        var results = new List<ScanItemResult>();
        var enumeratedCount = 0;
        var mediaCount = 0;
        var probeCount = 0;
        var unchangedCount = 0;
        var errorCount = 0;
        var rootUnavailable = false;
        var maxDispatchDuration = TimeSpan.Zero;
        string? resumeAfterPath = checkpoint;

        try
        {
            await foreach (var batch in _mediaFileEnumerator.EnumerateBatchesAsync(
                               root,
                               checkpoint,
                               command.BatchSize,
                               cancellationToken).ConfigureAwait(false))
            {
                if (batch.Any(item =>
                        item.ErrorCode is "IoError" or "AccessDenied" &&
                        string.Equals(item.Path, root.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    rootUnavailable = true;
                    await _mediaFileRepository
                        .SetRootAvailabilityAsync(root.Id, isAvailable: false, CancellationToken.None)
                        .ConfigureAwait(false);
                }

                var validItems = batch.Where(item => item.ErrorCode is null).ToArray();
                var existingByPath = await _mediaFileRepository.FindByPathsAsync(
                        root.Id,
                        validItems.Select(item => item.Path).ToArray(),
                        cancellationToken)
                    .ConfigureAwait(false);
                var pendingUpserts = new List<MediaFile>(validItems.Length);
                foreach (var item in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    enumeratedCount++;
                    if (item.ErrorCode is not null)
                    {
                        errorCount++;
                        results.Add(new ScanItemResult(item.Path, ScanItemOutcome.Failed, item.ErrorCode));
                    }
                    else
                    {
                        existingByPath.TryGetValue(item.Path, out var existing);
                        var result = await ProcessFileAsync(root.Id, item, existing, cancellationToken)
                            .ConfigureAwait(false);
                        results.Add(result.Item);
                        mediaCount++;
                        probeCount += result.WasProbed ? 1 : 0;
                        unchangedCount += result.Item.Outcome == ScanItemOutcome.Unchanged ? 1 : 0;
                        errorCount += result.Item.Outcome == ScanItemOutcome.Failed ? 1 : 0;
                        mediaCount -= result.Item.Outcome == ScanItemOutcome.Failed ? 1 : 0;
                        if (result.MediaFile is not null)
                        {
                            pendingUpserts.Add(result.MediaFile);
                        }
                    }

                    resumeAfterPath = item.Path;
                }

                if (pendingUpserts.Count > 0)
                {
                    await _mediaFileRepository
                        .UpsertBatchAsync(pendingUpserts, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (resumeAfterPath is not null)
                {
                    await _mediaFileRepository
                        .SaveScanCheckpointAsync(root.Id, resumeAfterPath, CancellationToken.None)
                        .ConfigureAwait(false);
                }

                maxDispatchDuration = Max(
                    maxDispatchDuration,
                    await PublishProgressAsync(
                        root.Id,
                        enumeratedCount,
                        probeCount,
                        resumeAfterPath,
                        isCompleted: false,
                        cancellationToken).ConfigureAwait(false));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CreateSummary(isCancelled: true);
        }

        if (!rootUnavailable)
        {
            await _mediaFileRepository
                .SetRootAvailabilityAsync(root.Id, isAvailable: true, cancellationToken)
                .ConfigureAwait(false);
        }

        await _mediaFileRepository.ClearScanCheckpointAsync(root.Id, cancellationToken).ConfigureAwait(false);
        resumeAfterPath = null;
        maxDispatchDuration = Max(
            maxDispatchDuration,
            await PublishProgressAsync(
                root.Id,
                enumeratedCount,
                probeCount,
                currentPath: null,
                isCompleted: true,
                cancellationToken).ConfigureAwait(false));
        await _eventPublisher
            .PublishAsync(new CatalogChanged(root.Id, mediaCount - unchangedCount), cancellationToken)
            .ConfigureAwait(false);
        return CreateSummary(isCancelled: false);

        ScanSummary CreateSummary(bool isCancelled) => new(
            root.Id,
            enumeratedCount,
            mediaCount,
            probeCount,
            unchangedCount,
            errorCount,
            isCancelled,
            resumeAfterPath,
            results,
            maxDispatchDuration);
    }

    private async Task<ProcessedFile> ProcessFileAsync(
        LibraryRootId rootId,
        EnumeratedFile item,
        MediaFile? existing,
        CancellationToken cancellationToken)
    {
        try
        {
            if (existing is not null &&
                existing.SizeBytes == item.SizeBytes &&
                existing.LastWriteUtc == item.LastWriteUtc)
            {
                return new ProcessedFile(
                    new ScanItemResult(item.Path, ScanItemOutcome.Unchanged),
                    WasProbed: false,
                    MediaFile: null);
            }

            var metadata = await _mediaProbe.ProbeAsync(item.Path, cancellationToken).ConfigureAwait(false);
            var mediaFile = new MediaFile(
                existing?.Id ?? new MediaFileId(Guid.NewGuid()),
                rootId,
                item.Path,
                item.SizeBytes,
                item.LastWriteUtc,
                metadata,
                IsAvailable: true);
            return new ProcessedFile(
                new ScanItemResult(
                    item.Path,
                    existing is null ? ScanItemOutcome.Added : ScanItemOutcome.Updated),
                WasProbed: true,
                mediaFile);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new ProcessedFile(
                new ScanItemResult(item.Path, ScanItemOutcome.Failed, exception.GetType().Name),
                WasProbed: true,
                MediaFile: null);
        }
    }

    private async Task<TimeSpan> PublishProgressAsync(
        LibraryRootId rootId,
        int enumeratedCount,
        int probeCount,
        string? currentPath,
        bool isCompleted,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await _eventPublisher.PublishAsync(
                new ScanProgressChanged(rootId, enumeratedCount, probeCount, currentPath, isCompleted),
                cancellationToken)
            .ConfigureAwait(false);
        return stopwatch.Elapsed;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

    private sealed record ProcessedFile(
        ScanItemResult Item,
        bool WasProbed,
        MediaFile? MediaFile);
}
