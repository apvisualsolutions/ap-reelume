// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Personalization;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// Projects the two local inputs the recommender needs straight out of SQLite. Genres, cast, years and
/// marks are read as columns; the library is never serialised, copied, or sent anywhere.
/// </summary>
public sealed class RecommendationReadModel : IRecommendationReadModel
{
    private const string TitleProjection = """
        SELECT t.id, t.release_year,
               COALESCE((SELECT group_concat(genre, char(31)) FROM title_genres WHERE title_id = t.id), ''),
               COALESCE((SELECT group_concat(person_name, char(31)) FROM title_cast WHERE title_id = t.id), ''),
               t.is_available,
               CASE WHEN EXISTS (
                   SELECT 1 FROM watch_state w
                   WHERE w.title_id = t.id AND w.episode_id IS NULL AND w.status = $watched) THEN 1 ELSE 0 END,
               (SELECT p.rating FROM personal_state p
                WHERE p.title_id = t.id AND p.episode_id IS NULL)
        FROM titles t
        """;

    /// <summary>
    /// ASCII unit separator, matching the `char(31)` the projection joins with. A comma would be
    /// wrong here: a genre or a person's name may legitimately contain one.
    /// </summary>
    private const char UnitSeparator = (char)31;

    private readonly SqliteConnectionFactory _connectionFactory;

    public RecommendationReadModel(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<RecommendationTaste> ReadTasteAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {TitleProjection}
            WHERE EXISTS (
                SELECT 1 FROM watch_state w
                WHERE w.title_id = t.id AND w.episode_id IS NULL AND w.status = $watched);
            """;
        _ = command.Parameters.AddWithValue("$watched", (int)Domain.Continuity.WatchStatus.Watched);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var history = new List<WatchedTitle>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            history.Add(new WatchedTitle(
                new TitleId(Guid.Parse(reader.GetString(0))),
                Split(reader.GetString(2)),
                Split(reader.GetString(3)),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        return RecommendationPolicy.Summarize(history);
    }

    public async Task<IReadOnlyList<RecommendationCandidate>> ReadCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {TitleProjection}
            ORDER BY t.id;
            """;
        _ = command.Parameters.AddWithValue("$watched", (int)Domain.Continuity.WatchStatus.Watched);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var candidates = new List<RecommendationCandidate>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            candidates.Add(new RecommendationCandidate(
                new TitleId(Guid.Parse(reader.GetString(0))),
                Split(reader.GetString(2)),
                Split(reader.GetString(3)),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetInt32(4) == 1,
                reader.GetInt32(5) == 1,
                reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        return candidates;
    }

    private static string[] Split(string value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split(UnitSeparator, StringSplitOptions.RemoveEmptyEntries);
}
