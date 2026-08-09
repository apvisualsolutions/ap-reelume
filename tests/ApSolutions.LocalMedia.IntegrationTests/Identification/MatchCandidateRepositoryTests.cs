using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Identification;

public sealed class MatchCandidateRepositoryTests
{
    [Fact]
    public async Task Replacement_is_idempotent_ordered_and_preserves_explainable_fields()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var mediaFileId = new MediaFileId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        await SeedMediaFileAsync(factory, mediaFileId);
        var repository = new MatchCandidateRepository(factory);
        var lower = Candidate(mediaFileId, "tmdb:movie:2", 0.70, ReviewState.Suggested);
        var higher = Candidate(mediaFileId, "tmdb:movie:1", 0.95, ReviewState.Automatic) with
        {
            Revision = 3,
            IsDecisionLocked = true,
        };

        await repository.ReplaceForMediaFileAsync(
            mediaFileId,
            [lower, higher],
            TestContext.Current.CancellationToken);
        await repository.ReplaceForMediaFileAsync(
            mediaFileId,
            [lower, higher],
            TestContext.Current.CancellationToken);

        var persisted = await repository.GetForMediaFileAsync(
            mediaFileId,
            TestContext.Current.CancellationToken);

        Assert.Equal(["tmdb:movie:1", "tmdb:movie:2"], persisted.Select(candidate => candidate.StableKey));
        AssertCandidateContract(higher, persisted[0]);
        AssertCandidateContract(lower, persisted[1]);
    }

    [Fact]
    public async Task Candidate_for_a_different_file_rolls_back_the_replacement()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var mediaFileId = new MediaFileId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        await SeedMediaFileAsync(factory, mediaFileId);
        var repository = new MatchCandidateRepository(factory);
        var original = Candidate(mediaFileId, "tmdb:movie:1", 0.95, ReviewState.Automatic);
        await repository.ReplaceForMediaFileAsync(mediaFileId, [original], TestContext.Current.CancellationToken);
        var wrongFile = new MediaFileId(Guid.Parse("40000000-0000-0000-0000-000000000099"));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.ReplaceForMediaFileAsync(
            mediaFileId,
            [Candidate(wrongFile, "tmdb:movie:2", 0.70, ReviewState.Suggested)],
            TestContext.Current.CancellationToken));

        var persisted = await repository.GetForMediaFileAsync(mediaFileId, TestContext.Current.CancellationToken);
        Assert.Single(persisted);
        AssertCandidateContract(original, persisted[0]);
    }

    [Fact]
    public async Task Accepted_manual_choice_survives_rescan_and_stays_out_of_the_inbox()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var mediaFileId = new MediaFileId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        await SeedMediaFileAsync(factory, mediaFileId);
        var repository = new MatchCandidateRepository(factory);
        var original = Candidate(mediaFileId, "tmdb:movie:1", 0.75, ReviewState.Suggested);
        await repository.ReplaceForMediaFileAsync(mediaFileId, [original], TestContext.Current.CancellationToken);

        var resolved = await repository.TrySetReviewStateAsync(
            mediaFileId,
            original.Id,
            expectedRevision: 0,
            ReviewState.Accepted,
            lockDecision: true,
            TestContext.Current.CancellationToken);
        await repository.ReplaceForMediaFileAsync(
            mediaFileId,
            [original with { Score = 0.40, ReviewState = ReviewState.Pending }],
            TestContext.Current.CancellationToken);

        Assert.Equal(MatchDecisionWriteOutcome.Applied, resolved.Outcome);
        var persisted = Assert.Single(await repository.GetForMediaFileAsync(
            mediaFileId,
            TestContext.Current.CancellationToken));
        Assert.Equal(ReviewState.Accepted, persisted.ReviewState);
        Assert.Equal(1, persisted.Revision);
        Assert.True(persisted.IsDecisionLocked);
        Assert.Empty(await repository.ListForReviewAsync(0, 10, TestContext.Current.CancellationToken));

        var stale = await repository.TrySetReviewStateAsync(
            mediaFileId,
            original.Id,
            expectedRevision: 0,
            ReviewState.Rejected,
            lockDecision: true,
            TestContext.Current.CancellationToken);
        Assert.Equal(MatchDecisionWriteOutcome.Conflict, stale.Outcome);
    }

    private static MatchCandidate Candidate(
        MediaFileId mediaFileId,
        string stableKey,
        double score,
        ReviewState state) => new(
            CandidateId.FromStableKey(stableKey),
            mediaFileId,
            stableKey,
            CandidateContentKind.Movie,
            score,
            CandidateScorer.ScoringModelVersion,
            state,
            [new MatchSignal("Identification.Signal.Title", score, 0.5)],
            ["Identification.Signal.Title"]);

    private static void AssertCandidateContract(MatchCandidate expected, MatchCandidate actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.MediaFileId, actual.MediaFileId);
        Assert.Equal(expected.StableKey, actual.StableKey);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Score, actual.Score);
        Assert.Equal(expected.ScoringModelVersion, actual.ScoringModelVersion);
        Assert.Equal(expected.ReviewState, actual.ReviewState);
        Assert.Equal(expected.Signals, actual.Signals);
        Assert.Equal(expected.ExplanationCodes, actual.ExplanationCodes);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.IsDecisionLocked, actual.IsDecisionLocked);
    }

    private static async Task SeedMediaFileAsync(SqliteConnectionFactory factory, MediaFileId mediaFileId)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
            VALUES ($rootId, 'C:\\Media', 0, 0, 1);
            INSERT INTO media_files (
                id, library_root_id, normalized_path, size_bytes, last_write_utc,
                duration_ticks, container, video_codecs, audio_codecs, width, height, is_available)
            VALUES (
                $mediaFileId, $rootId, 'C:\\Media\\Arrival.2016.mkv', 1,
                '2026-08-01T00:00:00.0000000+00:00', NULL, 'mkv', '[]', '[]', NULL, NULL, 1);
            """;
        command.Parameters.AddWithValue("$rootId", "40000000-0000-0000-0000-000000000010");
        command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
