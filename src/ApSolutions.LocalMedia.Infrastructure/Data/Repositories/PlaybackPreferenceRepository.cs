using System.Globalization;
using ApSolutions.LocalMedia.Domain.Continuity;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// One row per scope key. Every column is nullable because an unset field must fall through to the
/// next scope instead of overwriting it with a default.
/// </summary>
public sealed class PlaybackPreferenceRepository : IPlaybackPreferenceRepository
{
    private const string Columns = """
        scope, scope_key, audio_language, audio_channels, audio_codec, audio_prefer_external,
        subtitle_language, subtitle_channels, subtitle_codec, subtitle_prefer_external,
        subtitles_enabled, speed_multiplier, volume_percent, audio_output_device_id,
        subtitle_font_size_percent, subtitle_font_family, subtitle_foreground, subtitle_background,
        subtitle_background_opacity, subtitle_outline_thickness
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    public PlaybackPreferenceRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<PlaybackPreference?> GetAsync(
        PreferenceScope scope,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM playback_preferences
            WHERE scope = $scope AND scope_key = $key;
            """;
        command.Parameters.AddWithValue("$scope", (int)scope);
        command.Parameters.AddWithValue("$key", scopeKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preference);
        ArgumentException.ThrowIfNullOrWhiteSpace(preference.ScopeKey);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO playback_preferences (
                scope, scope_key, audio_language, audio_channels, audio_codec, audio_prefer_external,
                subtitle_language, subtitle_channels, subtitle_codec, subtitle_prefer_external,
                subtitles_enabled, speed_multiplier, volume_percent, audio_output_device_id,
                subtitle_font_size_percent, subtitle_font_family, subtitle_foreground,
                subtitle_background, subtitle_background_opacity, subtitle_outline_thickness,
                updated_at)
            VALUES (
                $scope, $key, $audioLanguage, $audioChannels, $audioCodec, $audioExternal,
                $subtitleLanguage, $subtitleChannels, $subtitleCodec, $subtitleExternal,
                $subtitlesEnabled, $speed, $volume, $device,
                $fontSize, $fontFamily, $foreground, $background, $backgroundOpacity, $outline,
                $updatedAt)
            ON CONFLICT(scope, scope_key) DO UPDATE SET
                audio_language = excluded.audio_language,
                audio_channels = excluded.audio_channels,
                audio_codec = excluded.audio_codec,
                audio_prefer_external = excluded.audio_prefer_external,
                subtitle_language = excluded.subtitle_language,
                subtitle_channels = excluded.subtitle_channels,
                subtitle_codec = excluded.subtitle_codec,
                subtitle_prefer_external = excluded.subtitle_prefer_external,
                subtitles_enabled = excluded.subtitles_enabled,
                speed_multiplier = excluded.speed_multiplier,
                volume_percent = excluded.volume_percent,
                audio_output_device_id = excluded.audio_output_device_id,
                subtitle_font_size_percent = excluded.subtitle_font_size_percent,
                subtitle_font_family = excluded.subtitle_font_family,
                subtitle_foreground = excluded.subtitle_foreground,
                subtitle_background = excluded.subtitle_background,
                subtitle_background_opacity = excluded.subtitle_background_opacity,
                subtitle_outline_thickness = excluded.subtitle_outline_thickness,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$scope", (int)preference.Scope);
        command.Parameters.AddWithValue("$key", preference.ScopeKey);
        AddSelection(command, "audio", preference.Audio);
        AddSelection(command, "subtitle", preference.Subtitle);
        AddNullable(command, "$subtitlesEnabled", preference.SubtitlesEnabled is { } enabled ? enabled ? 1 : 0 : null);
        AddNullable(command, "$speed", preference.SpeedMultiplier);
        AddNullable(command, "$volume", preference.VolumePercent);
        AddNullable(command, "$device", preference.AudioOutputDeviceId);
        AddNullable(command, "$fontSize", preference.SubtitleStyle?.FontSizePercent);
        AddNullable(command, "$fontFamily", preference.SubtitleStyle?.FontFamily);
        AddNullable(command, "$foreground", preference.SubtitleStyle?.ForegroundHex);
        AddNullable(command, "$background", preference.SubtitleStyle?.BackgroundHex);
        AddNullable(command, "$backgroundOpacity", preference.SubtitleStyle?.BackgroundOpacity);
        AddNullable(command, "$outline", preference.SubtitleStyle?.OutlineThickness);
        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        PreferenceScope scope,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM playback_preferences WHERE scope = $scope AND scope_key = $key;";
        command.Parameters.AddWithValue("$scope", (int)scope);
        command.Parameters.AddWithValue("$key", scopeKey);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddSelection(SqliteCommand command, string prefix, TrackSelection? selection)
    {
        AddNullable(command, $"${prefix}Language", selection?.Language);
        AddNullable(command, $"${prefix}Channels", selection?.Channels);
        AddNullable(command, $"${prefix}Codec", selection?.Codec);
        AddNullable(
            command,
            $"${prefix}External",
            selection is null ? null : selection.PreferExternal ? 1 : 0);
    }

    private static void AddNullable(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static PlaybackPreference Read(SqliteDataReader reader) => new()
    {
        Scope = (PreferenceScope)reader.GetInt32(0),
        ScopeKey = reader.GetString(1),
        Audio = ReadSelection(reader, 2),
        Subtitle = ReadSelection(reader, 6),
        SubtitlesEnabled = reader.IsDBNull(10) ? null : reader.GetInt32(10) == 1,
        SpeedMultiplier = reader.IsDBNull(11) ? null : reader.GetDouble(11),
        VolumePercent = reader.IsDBNull(12) ? null : reader.GetInt32(12),
        AudioOutputDeviceId = reader.IsDBNull(13) ? null : reader.GetString(13),
        SubtitleStyle = ReadStyle(reader, 14),
    };

    private static TrackSelection? ReadSelection(SqliteDataReader reader, int offset)
    {
        var language = reader.IsDBNull(offset) ? null : reader.GetString(offset);
        var channels = reader.IsDBNull(offset + 1) ? (int?)null : reader.GetInt32(offset + 1);
        var codec = reader.IsDBNull(offset + 2) ? null : reader.GetString(offset + 2);
        if (reader.IsDBNull(offset + 3))
        {
            return language is null && channels is null && codec is null
                ? null
                : new TrackSelection(language, channels, codec, PreferExternal: false);
        }

        return new TrackSelection(language, channels, codec, reader.GetInt32(offset + 3) == 1);
    }

    private static SubtitleStyle? ReadStyle(SqliteDataReader reader, int offset)
    {
        if (reader.IsDBNull(offset) || reader.IsDBNull(offset + 1))
        {
            return null;
        }

        return SubtitleStyle.Create(
            reader.GetDouble(offset),
            reader.GetString(offset + 1),
            reader.IsDBNull(offset + 2) ? SubtitleStyle.EngineDefault.ForegroundHex : reader.GetString(offset + 2),
            reader.IsDBNull(offset + 3) ? SubtitleStyle.EngineDefault.BackgroundHex : reader.GetString(offset + 3),
            reader.IsDBNull(offset + 4) ? SubtitleStyle.EngineDefault.BackgroundOpacity : reader.GetDouble(offset + 4),
            reader.IsDBNull(offset + 5) ? SubtitleStyle.EngineDefault.OutlineThickness : reader.GetDouble(offset + 5));
    }
}
