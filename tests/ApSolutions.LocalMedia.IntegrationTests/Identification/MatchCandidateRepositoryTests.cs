// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
        using var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
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
        using var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
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
        using var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
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

    /// <summary>
    /// The name the provider gave a candidate survives every reading of it, and its absence too.
    /// </summary>
    /// <remarks>
    /// The tray shows this and nothing else a person can weigh: without it a row says
    /// «tmdb:movie:761053» and asks somebody to decide about a title they were never told. Three
    /// readings return a candidate — the file's own, the tray's projection and the single row a
    /// decision reads — and a column added to one of them and forgotten in the others is a name that
    /// appears and disappears depending on which screen you are on.
    ///
    /// The nameless half is the row written before the column existed, which is every row in a
    /// library upgraded rather than created.
    /// </remarks>
    [Fact]
    public async Task A_candidates_name_survives_every_reading_of_it()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var mediaFileId = new MediaFileId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        await SeedMediaFileAsync(factory, mediaFileId);
        var repository = new MatchCandidateRepository(factory);
        var named = Candidate(mediaFileId, "tmdb:movie:1", 0.95, ReviewState.Suggested) with
        {
            DisplayTitle = "Puerto Sombra (2021)",
        };
        var nameless = Candidate(mediaFileId, "tmdb:movie:2", 0.70, ReviewState.Pending);

        await repository.ReplaceForMediaFileAsync(
            mediaFileId,
            [named, nameless],
            TestContext.Current.CancellationToken);

        var byFile = await repository.GetForMediaFileAsync(
            mediaFileId,
            TestContext.Current.CancellationToken);
        Assert.Equal("Puerto Sombra (2021)", byFile[0].DisplayTitle);
        Assert.Null(byFile[1].DisplayTitle);

        var tray = await repository.ListForReviewAsync(
            limit: 10,
            offset: 0,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(
            "Puerto Sombra (2021)",
            Assert.Single(tray, candidate => candidate.StableKey == "tmdb:movie:1").DisplayTitle);
        Assert.Null(Assert.Single(tray, candidate => candidate.StableKey == "tmdb:movie:2").DisplayTitle);

        // And the reading a decision makes, which is the one that writes the row back.
        var decided = await repository.TrySetReviewStateAsync(
            mediaFileId,
            named.Id,
            expectedRevision: 0,
            ReviewState.Accepted,
            lockDecision: true,
            TestContext.Current.CancellationToken);
        Assert.Equal(MatchDecisionWriteOutcome.Applied, decided.Outcome);
        Assert.Equal("Puerto Sombra (2021)", decided.Candidate?.DisplayTitle);
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
