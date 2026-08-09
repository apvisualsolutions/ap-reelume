using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// One row per marked piece of content, written as a single upsert. The schema refuses a row with
/// nothing marked and a rating outside one to ten, so the storage cannot hold a state the domain
/// would reject.
/// </summary>
public sealed class PersonalStateRepository : IPersonalStateRepository
{
    private const string Columns = "content_key, title_id, episode_id, is_favorite, is_watch_later, rating";

    private readonly SqliteConnectionFactory _connectionFactory;

    public PersonalStateRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<PersonalState?> GetAsync(
        ContentKey content,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM personal_state
            WHERE content_key = $key;
            """;
        _ = command.Parameters.AddWithValue("$key", content.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<PersonalState>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM personal_state
            ORDER BY content_key;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var states = new List<PersonalState>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            states.Add(Read(reader));
        }

        return states;
    }

    public async Task SaveAsync(
        PersonalState state,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsEmpty)
        {
            throw new ArgumentException(
                "A state with nothing marked is removed rather than stored.",
                nameof(state));
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO personal_state (
                content_key, title_id, episode_id, is_favorite, is_watch_later, rating, updated_utc)
            VALUES ($key, $titleId, $episodeId, $isFavorite, $isWatchLater, $rating, $updatedUtc)
            ON CONFLICT(content_key) DO UPDATE SET
                title_id = excluded.title_id,
                episode_id = excluded.episode_id,
                is_favorite = excluded.is_favorite,
                is_watch_later = excluded.is_watch_later,
                rating = excluded.rating,
                updated_utc = excluded.updated_utc;
            """;
        _ = command.Parameters.AddWithValue("$key", state.Content.Value);
        _ = command.Parameters.AddWithValue("$titleId", state.Content.TitleId.Value.ToString("D"));
        _ = command.Parameters.AddWithValue(
            "$episodeId",
            state.Content.EpisodeId is { } episode ? episode.Value.ToString("D") : DBNull.Value);
        _ = command.Parameters.AddWithValue("$isFavorite", state.IsFavorite ? 1 : 0);
        _ = command.Parameters.AddWithValue("$isWatchLater", state.IsWatchLater ? 1 : 0);
        _ = command.Parameters.AddWithValue("$rating", (object?)state.Rating ?? DBNull.Value);
        _ = command.Parameters.AddWithValue(
            "$updatedUtc",
            updatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(ContentKey content, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM personal_state WHERE content_key = $key;";
        _ = command.Parameters.AddWithValue("$key", content.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static PersonalState Read(SqliteDataReader reader) => new()
    {
        Content = reader.IsDBNull(2)
            ? ContentKey.ForTitle(new TitleId(Guid.Parse(reader.GetString(1))))
            : ContentKey.ForEpisode(
                new TitleId(Guid.Parse(reader.GetString(1))),
                new EpisodeId(Guid.Parse(reader.GetString(2)))),
        IsFavorite = reader.GetInt32(3) == 1,
        IsWatchLater = reader.GetInt32(4) == 1,
        Rating = reader.IsDBNull(5) ? null : reader.GetInt32(5),
    };
}
