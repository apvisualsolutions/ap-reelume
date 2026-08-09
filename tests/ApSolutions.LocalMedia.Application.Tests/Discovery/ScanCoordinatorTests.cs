using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Discovery;

public sealed class ScanCoordinatorTests
{
    [Fact]
    public async Task Scan_is_batched_publishes_progress_and_never_blocks_the_calling_thread()
    {
        var root = CreateRoot();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = Enumerable.Range(0, 1_000)
            .Select(index => FileItem($@"C:\Media\item-{index:D4}.mkv", index))
            .ToArray();
        var enumerator = new FakeMediaFileEnumerator(items) { FirstBatchGate = gate.Task };
        var mediaRepository = new InMemoryMediaFileRepository();
        var probe = new CountingProbe();
        var publisher = new RecordingPublisher();
        var coordinator = CreateCoordinator(root, mediaRepository, enumerator, probe, publisher);

        var scan = coordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Manual, 128),
            TestContext.Current.CancellationToken);

        Assert.False(scan.IsCompleted);
        gate.SetResult();
        var summary = await scan;

        Assert.Equal(128, enumerator.RequestedBatchSize);
        Assert.Equal(1_000, summary.EnumeratedCount);
        Assert.Equal(1_000, summary.MediaCount);
        Assert.Equal(1_000, summary.ProbeCount);
        Assert.Equal(0, summary.UnchangedCount);
        Assert.Equal(0, summary.ErrorCount);
        Assert.False(summary.IsCancelled);
        Assert.Null(summary.ResumeAfterPath);
        Assert.Equal(1_000, summary.Results.Count);
        Assert.Equal(8, mediaRepository.BatchWriteSizes.Count);
        Assert.All(mediaRepository.BatchWriteSizes, size => Assert.InRange(size, 1, 128));
        Assert.True(summary.MaxEventDispatchDuration < TimeSpan.FromMilliseconds(50));
        Assert.True(publisher.Progress.Count >= 9);
        Assert.True(publisher.Progress[^1].IsCompleted);
    }

    [Fact]
    public async Task Cancellation_persists_a_partial_checkpoint_and_the_next_scan_resumes()
    {
        var root = CreateRoot();
        var items = Enumerable.Range(0, 10)
            .Select(index => FileItem($@"C:\Media\episode-{index:D2}.mkv", index))
            .ToArray();
        using var cancellation = new CancellationTokenSource();
        var firstEnumerator = new FakeMediaFileEnumerator(items)
        {
            CancelAfterFirstBatch = cancellation,
        };
        var mediaRepository = new InMemoryMediaFileRepository();
        var probe = new CountingProbe();
        var firstCoordinator = CreateCoordinator(
            root,
            mediaRepository,
            firstEnumerator,
            probe,
            new RecordingPublisher());

        var partial = await firstCoordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Manual, 5),
            cancellation.Token);

        Assert.True(partial.IsCancelled);
        Assert.Equal(5, partial.MediaCount);
        Assert.Equal(items[4].Path, partial.ResumeAfterPath);
        Assert.Equal(
            items[4].Path,
            await mediaRepository.GetScanCheckpointAsync(root.Id, TestContext.Current.CancellationToken));

        var resumedEnumerator = new FakeMediaFileEnumerator(items);
        var resumedCoordinator = CreateCoordinator(
            root,
            mediaRepository,
            resumedEnumerator,
            probe,
            new RecordingPublisher());
        var completed = await resumedCoordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Recovery, 5),
            TestContext.Current.CancellationToken);

        Assert.Equal(items[4].Path, resumedEnumerator.ReceivedAfterPath);
        Assert.False(completed.IsCancelled);
        Assert.Equal(5, completed.MediaCount);
        Assert.Equal(10, mediaRepository.MediaFiles.Length);
        Assert.Null(
            await mediaRepository.GetScanCheckpointAsync(root.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Errors_are_isolated_and_only_new_or_changed_files_are_probed()
    {
        var root = CreateRoot();
        var unchanged = FileItem(@"C:\Media\unchanged.mp4", 1);
        var changed = FileItem(@"C:\Media\changed.mkv", 2);
        var added = FileItem(@"C:\Media\added.avi", 3);
        var denied = new EnumeratedFile(@"C:\Media\denied.mov", 0, DateTimeOffset.UnixEpoch, "AccessDenied");
        var mediaRepository = new InMemoryMediaFileRepository();
        await mediaRepository.UpsertAsync(Media(root.Id, unchanged), TestContext.Current.CancellationToken);
        await mediaRepository.UpsertAsync(
            Media(root.Id, changed) with { LastWriteUtc = changed.LastWriteUtc.AddMinutes(-1) },
            TestContext.Current.CancellationToken);
        var probe = new CountingProbe();
        var coordinator = CreateCoordinator(
            root,
            mediaRepository,
            new FakeMediaFileEnumerator([unchanged, changed, added, denied]),
            probe,
            new RecordingPublisher());

        var summary = await coordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Manual, 10),
            TestContext.Current.CancellationToken);

        Assert.Equal(4, summary.EnumeratedCount);
        Assert.Equal(3, summary.MediaCount);
        Assert.Equal(2, summary.ProbeCount);
        Assert.Equal(1, summary.UnchangedCount);
        Assert.Equal(1, summary.ErrorCount);
        Assert.Contains(summary.Results, result => result.Outcome == ScanItemOutcome.Unchanged);
        Assert.Contains(summary.Results, result => result.Outcome == ScanItemOutcome.Updated);
        Assert.Contains(summary.Results, result => result.Outcome == ScanItemOutcome.Added);
        Assert.Contains(
            summary.Results,
            result => result.Outcome == ScanItemOutcome.Failed && result.ErrorCode == "AccessDenied");
        Assert.Equal(new[] { added.Path, changed.Path }, probe.Paths.Order().ToArray());
    }

    [Fact]
    public async Task A_failed_probe_is_counted_and_does_not_abort_the_root()
    {
        var root = CreateRoot();
        var corrupt = FileItem(@"C:\Media\corrupt.mkv", 1);
        var valid = FileItem(@"C:\Media\valid.mkv", 2);
        var coordinator = CreateCoordinator(
            root,
            new InMemoryMediaFileRepository(),
            new FakeMediaFileEnumerator([corrupt, valid]),
            new SelectiveProbe(corrupt.Path),
            new RecordingPublisher());

        var summary = await coordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Manual, 10),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.EnumeratedCount);
        Assert.Equal(2, summary.ProbeCount);
        Assert.Equal(1, summary.MediaCount);
        Assert.Equal(1, summary.ErrorCount);
        Assert.Contains(
            summary.Results,
            result => result.Path == corrupt.Path && result.Outcome == ScanItemOutcome.Failed);
        Assert.Contains(
            summary.Results,
            result => result.Path == valid.Path && result.Outcome == ScanItemOutcome.Added);
    }

    [Fact]
    public async Task Only_one_scan_per_root_runs_at_a_time()
    {
        var root = CreateRoot();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enumerator = new FakeMediaFileEnumerator([FileItem(@"C:\Media\movie.mp4", 1)])
        {
            FirstBatchGate = gate.Task,
        };
        var coordinator = CreateCoordinator(
            root,
            new InMemoryMediaFileRepository(),
            enumerator,
            new CountingProbe(),
            new RecordingPublisher());

        var first = coordinator.StartAsync(
            new StartScanCommand(root.Id),
            TestContext.Current.CancellationToken);
        var second = coordinator.StartAsync(
            new StartScanCommand(root.Id),
            TestContext.Current.CancellationToken);
        await Task.Yield();

        Assert.Equal(1, enumerator.MaxConcurrentEnumerations);
        gate.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, enumerator.MaxConcurrentEnumerations);
    }

    private static ScanCoordinator CreateCoordinator(
        LibraryRoot root,
        IMediaFileRepository mediaRepository,
        IMediaFileEnumerator enumerator,
        IMediaProbe probe,
        IApplicationEventPublisher publisher) =>
        new(
            new SingleRootRepository(root),
            mediaRepository,
            enumerator,
            probe,
            publisher);

    private static LibraryRoot CreateRoot() => new(
        new LibraryRootId(Guid.NewGuid()),
        @"C:\Media",
        RootKind.Local,
        RootAvailability.Available,
        ScanPolicy.Manual);

    private static EnumeratedFile FileItem(string path, int index) => new(
        path,
        index + 100,
        DateTimeOffset.UnixEpoch.AddMinutes(index));

    private static MediaFile Media(LibraryRootId rootId, EnumeratedFile file) => new(
        new MediaFileId(Guid.NewGuid()),
        rootId,
        file.Path,
        file.SizeBytes,
        file.LastWriteUtc,
        CountingProbe.Metadata,
        true);

    private sealed class SingleRootRepository(LibraryRoot root) : ILibraryRootRepository
    {
        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>([root]);

        public Task<LibraryRoot?> GetAsync(
            LibraryRootId id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LibraryRoot?>(id == root.Id ? root : null);

        public Task AddAsync(LibraryRoot item, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryMediaFileRepository : IMediaFileRepository
    {
        private readonly ConcurrentDictionary<string, MediaFile> _media = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<LibraryRootId, string> _checkpoints = new();

        public MediaFile[] MediaFiles => _media.Values.ToArray();

        public List<int> BatchWriteSizes { get; } = [];

        public Task<MediaFile?> FindByPathAsync(
            LibraryRootId rootId,
            string path,
            CancellationToken cancellationToken = default)
        {
            _media.TryGetValue(Key(rootId, path), out var mediaFile);
            return Task.FromResult(mediaFile);
        }

        public Task<MediaFile?> FindByIdAsync(
            MediaFileId id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_media.Values.FirstOrDefault(mediaFile => mediaFile.Id == id));

        public Task<IReadOnlyDictionary<string, MediaFile>> FindByPathsAsync(
            LibraryRootId rootId,
            IReadOnlyCollection<string> paths,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, MediaFile> found = paths
                .Select(path => (Path: path, Media: _media.GetValueOrDefault(Key(rootId, path))))
                .Where(item => item.Media is not null)
                .ToDictionary(item => item.Path, item => item.Media!, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(found);
        }

        public Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default)
        {
            _media[Key(mediaFile.LibraryRootId, mediaFile.Path)] = mediaFile;
            return Task.CompletedTask;
        }

        public Task UpsertBatchAsync(
            IReadOnlyCollection<MediaFile> mediaFiles,
            CancellationToken cancellationToken = default)
        {
            BatchWriteSizes.Add(mediaFiles.Count);
            foreach (var mediaFile in mediaFiles)
            {
                _media[Key(mediaFile.LibraryRootId, mediaFile.Path)] = mediaFile;
            }

            return Task.CompletedTask;
        }

        public Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
            FileIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IdentifiedMediaFile?>(null);

        public Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
            string fingerprint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IdentifiedMediaFile>>([]);

        public Task SaveIdentityAsync(
            MediaFileId mediaFileId,
            FileIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<FileIdentity?> GetIdentityAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) => Task.FromResult<FileIdentity?>(null);

        public Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReassignAsync(
            MediaFileId mediaFileId,
            LibraryRootId libraryRootId,
            string newPath,
            FileIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetRootAvailabilityAsync(
            LibraryRootId libraryRootId,
            bool isAvailable,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default)
        {
            _checkpoints.TryGetValue(rootId, out var checkpoint);
            return Task.FromResult(checkpoint);
        }

        public Task SaveScanCheckpointAsync(
            LibraryRootId rootId,
            string resumeAfterPath,
            CancellationToken cancellationToken = default)
        {
            _checkpoints[rootId] = resumeAfterPath;
            return Task.CompletedTask;
        }

        public Task ClearScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default)
        {
            _checkpoints.TryRemove(rootId, out _);
            return Task.CompletedTask;
        }

        private static string Key(LibraryRootId rootId, string path) => $"{rootId.Value:D}|{path}";
    }

    private sealed class FakeMediaFileEnumerator(IReadOnlyList<EnumeratedFile> items) : IMediaFileEnumerator
    {
        private int _active;

        public Task? FirstBatchGate { get; init; }

        public CancellationTokenSource? CancelAfterFirstBatch { get; init; }

        public int RequestedBatchSize { get; private set; }

        public string? ReceivedAfterPath { get; private set; }

        public int MaxConcurrentEnumerations { get; private set; }

        public async IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
            LibraryRoot root,
            string? afterPath,
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            RequestedBatchSize = batchSize;
            ReceivedAfterPath = afterPath;
            var active = Interlocked.Increment(ref _active);
            MaxConcurrentEnumerations = Math.Max(MaxConcurrentEnumerations, active);
            try
            {
                if (FirstBatchGate is not null)
                {
                    await FirstBatchGate.WaitAsync(cancellationToken);
                }

                await Task.Yield();
                var remaining = items
                    .Where(item => afterPath is null || string.Compare(item.Path, afterPath, StringComparison.OrdinalIgnoreCase) > 0)
                    .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                for (var offset = 0; offset < remaining.Length; offset += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return remaining.Skip(offset).Take(batchSize).ToArray();
                    if (offset == 0)
                    {
                        CancelAfterFirstBatch?.Cancel();
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class CountingProbe : IMediaProbe
    {
        public static readonly TechnicalMetadata Metadata = new(
            TimeSpan.FromMinutes(42),
            "matroska",
            ["h264"],
            ["aac"],
            1920,
            1080);

        public List<string> Paths { get; } = [];

        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            Paths.Add(path);
            return Task.FromResult(Metadata);
        }
    }

    private sealed class SelectiveProbe(string corruptPath) : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return path == corruptPath
                ? Task.FromException<TechnicalMetadata>(new InvalidDataException("Corrupt media."))
                : Task.FromResult(CountingProbe.Metadata);
        }
    }

    private sealed class RecordingPublisher : IApplicationEventPublisher
    {
        public List<ScanProgressChanged> Progress { get; } = [];

        public Task PublishAsync<TEvent>(
            TEvent applicationEvent,
            CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            if (applicationEvent is ScanProgressChanged progress)
            {
                Progress.Add(progress);
            }

            return Task.CompletedTask;
        }
    }
}
