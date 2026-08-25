// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

public sealed class MatchCandidateRepository : IMatchCandidateRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public MatchCandidateRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task ReplaceForMediaFileAsync(
        MediaFileId mediaFileId,
        IReadOnlyList<MatchCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Any(candidate => candidate.MediaFileId != mediaFileId))
        {
            throw new ArgumentException("Every candidate must belong to the requested media file.", nameof(candidates));
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var lockedStableKeys = new HashSet<string>(StringComparer.Ordinal);
        await using (var readLocked = connection.CreateCommand())
        {
            readLocked.Transaction = transaction;
            readLocked.CommandText = """
                SELECT stable_key
                FROM match_candidates
                WHERE media_file_id = $mediaFileId AND decision_locked = 1;
                """;
            readLocked.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            await using var reader = await readLocked.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lockedStableKeys.Add(reader.GetString(0));
            }
        }

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = """
                DELETE FROM match_candidates
                WHERE media_file_id = $mediaFileId AND decision_locked = 0;
                """;
            delete.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var candidate in candidates)
        {
            if (lockedStableKeys.Contains(candidate.StableKey))
            {
                continue;
            }

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO match_candidates (
                    candidate_id, media_file_id, stable_key, content_kind, score,
                    scoring_model_version, review_state, signals_json,
                    explanation_codes_json, revision, decision_locked, display_title)
                VALUES (
                    $candidateId, $mediaFileId, $stableKey, $contentKind, $score,
                    $modelVersion, $reviewState, $signals, $explanations, $revision, $locked,
                    $displayTitle);
                """;
            insert.Parameters.AddWithValue("$candidateId", candidate.Id.Value.ToString("D"));
            insert.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            insert.Parameters.AddWithValue("$stableKey", candidate.StableKey);
            insert.Parameters.AddWithValue("$contentKind", (int)candidate.Kind);
            insert.Parameters.AddWithValue("$score", candidate.Score);
            insert.Parameters.AddWithValue("$modelVersion", candidate.ScoringModelVersion);
            insert.Parameters.AddWithValue("$reviewState", (int)candidate.ReviewState);
            insert.Parameters.AddWithValue("$signals", JsonSerializer.Serialize(candidate.Signals));
            insert.Parameters.AddWithValue("$explanations", JsonSerializer.Serialize(candidate.ExplanationCodes));
            insert.Parameters.AddWithValue("$revision", candidate.Revision);
            insert.Parameters.AddWithValue("$locked", candidate.IsDecisionLocked ? 1 : 0);
            insert.Parameters.AddWithValue(
                "$displayTitle",
                (object?)candidate.DisplayTitle ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT candidate_id, stable_key, content_kind, score, scoring_model_version,
                   review_state, signals_json, explanation_codes_json, revision, decision_locked,
                   display_title
            FROM match_candidates
            WHERE media_file_id = $mediaFileId
            ORDER BY score DESC, stable_key COLLATE BINARY;
            """;
        command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var candidates = new List<MatchCandidate>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            candidates.Add(new MatchCandidate(
                new CandidateId(Guid.Parse(reader.GetString(0))),
                mediaFileId,
                reader.GetString(1),
                (CandidateContentKind)reader.GetInt32(2),
                reader.GetDouble(3),
                reader.GetInt32(4),
                (ReviewState)reader.GetInt32(5),
                JsonSerializer.Deserialize<MatchSignal[]>(reader.GetString(6)) ?? [],
                JsonSerializer.Deserialize<string[]>(reader.GetString(7)) ?? [],
                reader.GetInt32(8),
                reader.GetBoolean(9),
                MediaFilePath: null,
                DisplayTitle: reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return candidates;
    }

    public async Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (limit is < 1 or > 101)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT candidate_id, candidate.media_file_id, stable_key, content_kind, score,
                   scoring_model_version, review_state, signals_json,
                   explanation_codes_json, revision, decision_locked,
                   media.normalized_path, display_title
            FROM match_candidates candidate
            LEFT JOIN media_files media ON media.id = candidate.media_file_id
            WHERE candidate.review_state IN ($pending, $suggested)
              AND NOT EXISTS (
                  SELECT 1
                  FROM match_candidates accepted
                  WHERE accepted.media_file_id = candidate.media_file_id
                    AND accepted.review_state = $accepted
                    AND accepted.decision_locked = 1)
            ORDER BY
                CASE candidate.review_state WHEN $pending THEN 0 ELSE 1 END,
                candidate.score,
                candidate.stable_key COLLATE BINARY
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$pending", (int)ReviewState.Pending);
        command.Parameters.AddWithValue("$suggested", (int)ReviewState.Suggested);
        command.Parameters.AddWithValue("$accepted", (int)ReviewState.Accepted);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var candidates = new List<MatchCandidate>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            candidates.Add(ReadCandidate(reader, mediaFileColumn: 1, pathColumn: 11, titleColumn: 12));
        }

        return candidates;
    }

    public async Task<MatchDecisionWriteResult> TrySetReviewStateAsync(
        MediaFileId mediaFileId,
        CandidateId candidateId,
        int expectedRevision,
        ReviewState reviewState,
        bool lockDecision,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);

        if (reviewState is not (ReviewState.Accepted or ReviewState.Rejected))
        {
            throw new ArgumentOutOfRangeException(nameof(reviewState));
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE match_candidates
                SET review_state = $reviewState,
                    revision = revision + 1,
                    decision_locked = $locked
                WHERE media_file_id = $mediaFileId
                  AND candidate_id = $candidateId
                  AND revision = $expectedRevision
                  AND decision_locked = 0;
                """;
            update.Parameters.AddWithValue("$reviewState", (int)reviewState);
            update.Parameters.AddWithValue("$locked", lockDecision ? 1 : 0);
            update.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            update.Parameters.AddWithValue("$candidateId", candidateId.Value.ToString("D"));
            update.Parameters.AddWithValue("$expectedRevision", expectedRevision);
            var updated = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (updated == 1)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                var candidate = await FindAsync(
                    connection,
                    mediaFileId,
                    candidateId,
                    transaction: null,
                    cancellationToken).ConfigureAwait(false);
                return new MatchDecisionWriteResult(MatchDecisionWriteOutcome.Applied, candidate);
            }
        }

        var existing = await FindAsync(
            connection,
            mediaFileId,
            candidateId,
            transaction,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return existing is null
            ? new MatchDecisionWriteResult(MatchDecisionWriteOutcome.NotFound, null)
            : new MatchDecisionWriteResult(MatchDecisionWriteOutcome.Conflict, existing);
    }

    private static async Task<MatchCandidate?> FindAsync(
        SqliteConnection connection,
        MediaFileId mediaFileId,
        CandidateId candidateId,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT candidate_id, media_file_id, stable_key, content_kind, score,
                   scoring_model_version, review_state, signals_json,
                   explanation_codes_json, revision, decision_locked, display_title
            FROM match_candidates
            WHERE media_file_id = $mediaFileId AND candidate_id = $candidateId;
            """;
        command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
        command.Parameters.AddWithValue("$candidateId", candidateId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadCandidate(reader, mediaFileColumn: 1, titleColumn: 11)
            : null;
    }

    /// <summary>
    /// One row as a candidate. The path is read only where the projection selected it — the review
    /// tray's, which is the one place a candidate is shown to a person rather than scored.
    /// </summary>
    private static MatchCandidate ReadCandidate(
        SqliteDataReader reader,
        int mediaFileColumn,
        int pathColumn = -1,
        int titleColumn = -1) => new(
        new CandidateId(Guid.Parse(reader.GetString(0))),
        new MediaFileId(Guid.Parse(reader.GetString(mediaFileColumn))),
        reader.GetString(2),
        (CandidateContentKind)reader.GetInt32(3),
        reader.GetDouble(4),
        reader.GetInt32(5),
        (ReviewState)reader.GetInt32(6),
        JsonSerializer.Deserialize<MatchSignal[]>(reader.GetString(7)) ?? [],
        JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [],
        reader.GetInt32(9),
        reader.GetBoolean(10),
        pathColumn >= 0 && !reader.IsDBNull(pathColumn) ? reader.GetString(pathColumn) : null,
        titleColumn >= 0 && !reader.IsDBNull(titleColumn) ? reader.GetString(titleColumn) : null);
}
