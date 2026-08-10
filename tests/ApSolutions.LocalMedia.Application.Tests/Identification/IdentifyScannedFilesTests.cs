// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Identification;

/// <summary>
/// The caller the audit found missing: the scan produced files and nothing handed them to
/// identification, so the review inbox stayed empty forever (LIB-006/007).
/// </summary>
public sealed class IdentifyScannedFilesTests
{
    private static readonly LibraryRootId RootId = new(Guid.Parse("50000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task Files_without_candidates_are_identified_and_decided_files_are_left_alone()
    {
        var fixture = new Fixture();
        var fresh = fixture.AddFile(@"Movies\Dune.2021.mkv");
        var decided = fixture.AddFile(@"Movies\Arrival.2016.mkv");
        await fixture.Candidates.ReplaceForMediaFileAsync(
            decided,
            [Candidate(decided, "movie:kept")],
            TestContext.Current.CancellationToken);

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(ScanItemOutcome.Added),
            TestContext.Current.CancellationToken);

        Assert.Equal(new IdentifyScannedFilesResult(1, 1, 0), result);
        Assert.NotEmpty(fixture.Candidates.Read(fresh));
        Assert.Equal("movie:kept", Assert.Single(fixture.Candidates.Read(decided)).StableKey);
    }

    [Fact]
    public async Task Unchanged_files_nobody_ever_identified_heal_on_the_next_scan()
    {
        var fixture = new Fixture();
        var file = fixture.AddFile(@"Movies\Dune.2021.mkv");

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(ScanItemOutcome.Unchanged),
            TestContext.Current.CancellationToken);

        Assert.Equal(new IdentifyScannedFilesResult(1, 1, 0), result);
        Assert.NotEmpty(fixture.Candidates.Read(file));
    }

    [Fact]
    public async Task One_unreadable_file_does_not_cost_the_rest_of_the_scan_its_identification()
    {
        var fixture = new Fixture(throwOnTitle: "explosive");
        _ = fixture.AddFile(@"Movies\Explosive.2020.mkv");
        var healthy = fixture.AddFile(@"Movies\Dune.2021.mkv");

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(ScanItemOutcome.Added),
            TestContext.Current.CancellationToken);

        Assert.Equal(new IdentifyScannedFilesResult(2, 1, 1), result);
        Assert.NotEmpty(fixture.Candidates.Read(healthy));
    }

    [Fact]
    public async Task The_folders_between_the_root_and_the_file_reach_the_parser()
    {
        var fixture = new Fixture();
        _ = fixture.AddFile(@"Show\Season 1\Show.1x02.mkv");

        _ = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(ScanItemOutcome.Added),
            TestContext.Current.CancellationToken);

        var parsed = Assert.Single(fixture.Source.ParsedNames);
        Assert.Equal(1, parsed.Season);
        Assert.Equal(2, parsed.Episode);
    }

    [Fact]
    public async Task A_cancelled_scan_is_left_alone()
    {
        var fixture = new Fixture();
        var file = fixture.AddFile(@"Movies\Dune.2021.mkv");

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(ScanItemOutcome.Added) with { IsCancelled = true },
            TestContext.Current.CancellationToken);

        Assert.Equal(new IdentifyScannedFilesResult(0, 0, 0), result);
        Assert.Empty(fixture.Candidates.Read(file));
    }

    [Fact]
    public async Task A_root_the_catalogue_no_longer_knows_identifies_nothing()
    {
        var fixture = new Fixture(rootExists: false);
        var file = fixture.AddFile(@"Movies\Dune.2021.mkv");

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(ScanItemOutcome.Added),
            TestContext.Current.CancellationToken);

        Assert.Equal(new IdentifyScannedFilesResult(0, 0, 0), result);
        Assert.Empty(fixture.Candidates.Read(file));
    }

    private static MatchCandidate Candidate(MediaFileId mediaFileId, string stableKey) => new(
        CandidateId.FromStableKey(stableKey),
        mediaFileId,
        stableKey,
        CandidateContentKind.Movie,
        Score: 1.0,
        ScoringModelVersion: 1,
        ReviewState.Accepted,
        Signals: [],
        ExplanationCodes: []);

    private sealed class Fixture
    {
        private const string RootPath = @"C:\library";
        private readonly List<ScanItemResult> _items = [];
        private readonly Dictionary<string, MediaFile> _filesByPath = new(StringComparer.OrdinalIgnoreCase);

        public Fixture(string? throwOnTitle = null, bool rootExists = true)
        {
            Source = new RecordingSource(throwOnTitle);
            Candidates = new InMemoryCandidateRepository();
            UseCase = new IdentifyScannedFiles(
                new SingleRootRepository(rootExists
                    ? new LibraryRoot(RootId, RootPath, RootKind.Local, RootAvailability.Available, ScanPolicy.Manual)
                    : null),
                new PathOnlyMediaFileRepository(_filesByPath),
                Candidates,
                new IdentifyMediaFile(new MediaNameParser(), new CandidateScorer(), Source, Candidates));
        }

        public RecordingSource Source { get; }

        public InMemoryCandidateRepository Candidates { get; }

        public IdentifyScannedFiles UseCase { get; }

        public MediaFileId AddFile(string relativePath)
        {
            var path = System.IO.Path.Combine(RootPath, relativePath);
            var file = new MediaFile(
                new MediaFileId(Guid.NewGuid()),
                RootId,
                path,
                SizeBytes: 2,
                DateTimeOffset.UnixEpoch,
                new TechnicalMetadata(TimeSpan.FromMinutes(1), "mkv", ["h264"], ["aac"], 1920, 1080));
            _filesByPath[path] = file;
            _items.Add(new ScanItemResult(path, ScanItemOutcome.Added));
            return file.Id;
        }

        public ScanSummary Summary(ScanItemOutcome outcome) => new(
            RootId,
            _items.Count,
            _items.Count,
            _items.Count,
            UnchangedCount: 0,
            ErrorCount: 0,
            IsCancelled: false,
            ResumeAfterPath: null,
            [.. _items.Select(item => item with { Outcome = outcome })],
            TimeSpan.Zero);
    }

    private sealed class RecordingSource(string? throwOnTitle) : IIdentificationCandidateSource
    {
        public List<ParsedMediaName> ParsedNames { get; } = [];

        public Task<IReadOnlyList<CandidateFacts>> GetLocalAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default)
        {
            if (throwOnTitle is not null
                && parsed.CleanTitle.Contains(throwOnTitle, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This name refuses to be identified.");
            }

            ParsedNames.Add(parsed);
            return Task.FromResult<IReadOnlyList<CandidateFacts>>(
            [
                new CandidateFacts(
                    CandidateId.FromStableKey($"local:{parsed.CleanTitle}"),
                    $"local:{parsed.CleanTitle}",
                    parsed.Kind == ParsedMediaKind.Episode
                        ? CandidateContentKind.Episode
                        : CandidateContentKind.Movie,
                    TitleSimilarity: 1,
                    SeasonMatch: parsed.Season is null ? null : 1,
                    EpisodeMatch: parsed.Episode is null ? null : 1,
                    YearMatch: parsed.Year is null ? null : 1,
                    DurationMatch: 1),
            ]);
        }

        public Task<IReadOnlyList<CandidateFacts>> GetRemoteAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CandidateFacts>>([]);
    }

    private sealed class SingleRootRepository(LibraryRoot? root) : ILibraryRootRepository
    {
        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>(root is null ? [] : [root]);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(root?.Id == id ? root : null);

        public Task AddAsync(LibraryRoot added, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PathOnlyMediaFileRepository(IReadOnlyDictionary<string, MediaFile> filesByPath)
        : IMediaFileRepository
    {
        public Task<MediaFile?> FindByPathAsync(
            LibraryRootId rootId,
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(filesByPath.TryGetValue(path, out var file) && file.LibraryRootId == rootId
                ? file
                : null);

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

    private sealed class InMemoryCandidateRepository : IMatchCandidateRepository
    {
        private readonly Dictionary<MediaFileId, IReadOnlyList<MatchCandidate>> _candidates = [];

        public Task ReplaceForMediaFileAsync(
            MediaFileId mediaFileId,
            IReadOnlyList<MatchCandidate> candidates,
            CancellationToken cancellationToken = default)
        {
            _candidates[mediaFileId] = [.. candidates];
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) => Task.FromResult(Read(mediaFileId));

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

        public IReadOnlyList<MatchCandidate> Read(MediaFileId mediaFileId) =>
            _candidates.GetValueOrDefault(mediaFileId, []);
    }
}
