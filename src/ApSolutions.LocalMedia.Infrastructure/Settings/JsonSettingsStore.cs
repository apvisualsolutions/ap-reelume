// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApSolutions.LocalMedia.Application.Settings;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsPath;
    private readonly object _fileLock;

    public JsonSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
        _fileLock = PathLocks.GetOrAdd(_settingsPath, static _ => new object());
    }

    public T? Read<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_fileLock)
        {
            var values = ReadValues();
            return values.TryGetValue(key, out var value)
                ? value.Deserialize<T>(SerializerOptions)
                : default;
        }
    }

    public void Write<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_fileLock)
        {
            var values = ReadValues();
            values[key] = JsonSerializer.SerializeToElement(value, SerializerOptions);
            WriteValuesAtomically(values);
        }
    }

    private Dictionary<string, JsonElement> ReadValues()
    {
        if (!File.Exists(_settingsPath))
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        using var stream = new FileStream(
            _settingsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream, SerializerOptions)
            ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    }

    private void WriteValuesAtomically(Dictionary<string, JsonElement> values)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The settings path must have a parent directory.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, values, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_settingsPath))
            {
                File.Replace(temporaryPath, _settingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _settingsPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
