// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Identification;

public sealed class IdentifyMediaFileTests
{
    [Fact]
    public async Task Automatic_local_match_is_persisted_without_remote_provider_access()
    {
        var mediaFileId = new MediaFileId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var local = Facts("local:dune:2021", CandidateContentKind.Movie, title: 1, year: 1, duration: 1);
        var source = new RecordingCandidateSource([local], remote: [Facts("remote:unused", CandidateContentKind.Movie, 1)]);
        var repository = new RecordingCandidateRepository();
        var useCase = new IdentifyMediaFile(new MediaNameParser(), new CandidateScorer(), source, repository);

        var result = await useCase.ExecuteAsync(
            new IdentifyMediaFileCommand(mediaFileId, new FileNameContext("Dune.2021.mkv", ["Movies"])),
            TestContext.Current.CancellationToken);

        Assert.False(source.RemoteWasRequested);
        Assert.Equal(ReviewState.Automatic, Assert.Single(result.Candidates).ReviewState);
        Assert.Equal(result.Candidates, repository.Read(mediaFileId));
    }

    [Fact]
    public async Task Uncertain_local_match_adds_remote_candidates_and_orders_ties_stably()
    {
        var mediaFileId = new MediaFileId(Guid.NewGuid());
        var source = new RecordingCandidateSource(
            [Facts("local:z", CandidateContentKind.Movie, title: 0.60)],
            remote:
            [
                Facts("remote:b", CandidateContentKind.Movie, title: 0.80),
                Facts("remote:a", CandidateContentKind.Movie, title: 0.80),
            ]);
        var repository = new RecordingCandidateRepository();
        var useCase = new IdentifyMediaFile(new MediaNameParser(), new CandidateScorer(), source, repository);
        var command = new IdentifyMediaFileCommand(
            mediaFileId,
            new FileNameContext("Uncertain.2021.mkv", ["Movies"]));

        var first = await useCase.ExecuteAsync(command, TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(command, TestContext.Current.CancellationToken);

        Assert.True(source.RemoteWasRequested);
        Assert.Equal(["remote:a", "remote:b", "local:z"], first.Candidates.Select(candidate => candidate.StableKey));
        Assert.Equal(first.Candidates.Select(Project), second.Candidates.Select(Project));
        Assert.Equal(3, repository.Read(mediaFileId).Count);

        static (CandidateId Id, string Key, double Score, ReviewState State, string Signals, string Explanations)
            Project(MatchCandidate candidate) => (
                candidate.Id,
                candidate.StableKey,
                candidate.Score,
                candidate.ReviewState,
                string.Join('|', candidate.Signals.Select(signal => $"{signal.Code}:{signal.Value}:{signal.Weight}")),
                string.Join('|', candidate.ExplanationCodes));
    }

    private static CandidateFacts Facts(
        string stableKey,
        CandidateContentKind kind,
        double title,
        double? season = null,
        double? episode = null,
        double? year = null,
        double? duration = null) => new(
            CandidateId.FromStableKey(stableKey),
            stableKey,
            kind,
            title,
            season,
            episode,
            year,
            duration);

    private sealed class RecordingCandidateSource(
        IReadOnlyList<CandidateFacts> local,
        IReadOnlyList<CandidateFacts> remote) : IIdentificationCandidateSource
    {
        public bool RemoteWasRequested { get; private set; }

        public Task<IReadOnlyList<CandidateFacts>> GetLocalAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default) => Task.FromResult(local);

        public Task<IReadOnlyList<CandidateFacts>> GetRemoteAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default)
        {
            RemoteWasRequested = true;
            return Task.FromResult(remote);
        }
    }

    private sealed class RecordingCandidateRepository : IMatchCandidateRepository
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
