// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Identification;

/// <summary>
/// Every scan identifies what it found, whatever triggered it — and the summary always comes back
/// to the caller the scan belongs to.
/// </summary>
public sealed class IdentifyingScanCoordinatorTests
{
    [Fact]
    public async Task The_summary_the_inner_scan_produced_reaches_identification_and_the_caller()
    {
        var rootId = new LibraryRootId(Guid.NewGuid());
        var summary = new ScanSummary(rootId, 0, 0, 0, 0, 0, false, null, [], TimeSpan.Zero);
        var roots = new RecordingRoots();
        var candidates = new NullCandidates();
        var identification = new IdentifyScannedFiles(
            roots,
            new UntouchedMediaFiles(),
            candidates,
            new IdentifyMediaFile(new MediaNameParser(), new CandidateScorer(), new EmptySource(), candidates),
            TestIdentification.Silent());
        var grouping = new GroupScannedVersions(
            roots,
            new UntouchedMediaFiles(),
            new EmptyGroups(),
            new MediaNameParser(),
            new DuplicateGroupingPolicy(),
            new GroupMediaVersions(new EmptyGroups()));
        var series = new GroupScannedEpisodes(
            roots,
            new UntouchedMediaFiles(),
            new UnwrittenCatalog(),
            new MediaNameParser());
        var reconciliation = new ReconcileScannedFiles(
            new UntouchedMediaFiles(),
            new ReconcileScanResults(new UntouchedMediaFiles(), new FileReconciliationPolicy()),
            new UnreadIdentity(),
            new FileReconciliationPolicy(),
            new PendingReassignments());
        var coordinator = new IdentifyingScanCoordinator(
            new FixedScanCoordinator(summary),
            () => reconciliation,
            () => identification,
            () => grouping,
            () => series);

        var returned = await coordinator.StartAsync(
            new StartScanCommand(rootId, ScanTrigger.Watcher),
            TestContext.Current.CancellationToken);

        Assert.Same(summary, returned);

        // Identification, version grouping and series grouping each asked the repository for this
        // scan's root, which is the first thing all three do with a summary: every hand-off
        // happened. Three and not two since 2026-08-25, when a folder of episodes became a series.
        Assert.Equal([rootId, rootId, rootId], roots.Asked);
    }

    private sealed class UnreadIdentity : IFileIdentityProvider
    {
        public Task<FileIdentity> GetAsync(
            string path,
            TechnicalMetadata technicalMetadata,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedScanCoordinator(ScanSummary summary) : IScanCoordinator
    {
        public Task<ScanSummary> StartAsync(
            StartScanCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(summary);
        }
    }

    private sealed class RecordingRoots : ILibraryRootRepository
    {
        public List<LibraryRootId> Asked { get; } = [];

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>([]);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default)
        {
            Asked.Add(id);
            return Task.FromResult<LibraryRoot?>(null);
        }

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class EmptySource : IIdentificationCandidateSource
    {
        public Task<IReadOnlyList<CandidateFacts>> GetLocalAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CandidateFacts>>([]);

        public Task<IReadOnlyList<CandidateFacts>> GetRemoteAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CandidateFacts>>([]);
    }

    private sealed class NullCandidates : IMatchCandidateRepository
    {
        public Task ReplaceForMediaFileAsync(
            MediaFileId mediaFileId,
            IReadOnlyList<MatchCandidate> candidates,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>([]);

        public Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>([]);

        public Task<MatchDecisionWriteResult> TrySetReviewStateAsync(
            MediaFileId mediaFileId,
            CandidateId candidateId,
            int expectedRevision,
            ReviewState reviewState,
            bool lockDecision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.NotFound, null));
    }

    private sealed class EmptyGroups : IMediaVersionGroupRepository
    {
        public Task<MediaVersionGroup?> FindByContentKeyAsync(
            string contentKey,
            CancellationToken cancellationToken = default) => Task.FromResult<MediaVersionGroup?>(null);

        public Task<MediaVersionGroup?> FindByIdAsync(
            MediaVersionId groupId,
            CancellationToken cancellationToken = default) => Task.FromResult<MediaVersionGroup?>(null);

        public Task<MediaVersionGroup?> FindByMemberAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) => Task.FromResult<MediaVersionGroup?>(null);

        public Task SaveAsync(MediaVersionGroup group, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class UntouchedMediaFiles : IMediaFileRepository
    {
        public Task<MediaFile?> FindByPathAsync(
            LibraryRootId rootId,
            string path,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MediaFile?> FindByIdAsync(MediaFileId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, MediaFile>> FindByPathsAsync(
            LibraryRootId rootId,
            IReadOnlyCollection<string> paths,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpsertBatchAsync(
            IReadOnlyCollection<MediaFile> mediaFiles,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
            FileIdentity identity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
            string fingerprint,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveIdentityAsync(
            MediaFileId mediaFileId,
            FileIdentity identity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FileIdentity?> GetIdentityAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReassignAsync(
            MediaFileId mediaFileId,
            LibraryRootId libraryRootId,
            string newPath,
            FileIdentity identity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetRootAvailabilityAsync(
            LibraryRootId libraryRootId,
            bool isAvailable,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> GetScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveScanCheckpointAsync(
            LibraryRootId rootId,
            string resumeAfterPath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ClearScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>A catalogue that accepts everything and keeps none of it.</summary>
    /// <remarks>
    /// This scene is about the hand-offs and not about what any of them writes: the summary it feeds
    /// them holds no results at all, so nothing here is ever called. What matters is that the third
    /// use case was constructed and reached.
    /// </remarks>
    private sealed class UnwrittenCatalog : ICatalogRepository
    {
        public Task UpsertTitleAsync(CatalogTitle title, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertSeasonAsync(CatalogSeason season, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertEpisodeAsync(CatalogEpisode episode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkEpisodeMediaAsync(
            EpisodeId episodeId,
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
