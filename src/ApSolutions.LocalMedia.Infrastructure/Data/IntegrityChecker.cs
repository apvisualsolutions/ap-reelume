using ApSolutions.LocalMedia.Application.Data;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data;

public sealed class IntegrityChecker(SqliteConnectionFactory connectionFactory) : IDatabaseIntegrityChecker
{
    public async Task<DatabaseIntegrityResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var details = new List<string>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                details.Add(reader.GetString(0));
            }

            var valid = details.Count == 1
                && details[0].Equals("ok", StringComparison.OrdinalIgnoreCase);
            return new DatabaseIntegrityResult(valid, string.Join(Environment.NewLine, details));
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new DatabaseIntegrityResult(
                false,
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
