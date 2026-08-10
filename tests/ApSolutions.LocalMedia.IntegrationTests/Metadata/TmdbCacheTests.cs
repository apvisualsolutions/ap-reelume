// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

public sealed class TmdbCacheTests
{
    [Fact]
    public async Task Cache_round_trips_normalized_key_language_version_etag_and_dates()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var cache = new SqliteMetadataCache(factory);
        var key = new MetadataCacheKey("tmdb", "search:movie:arrival:2016", "es-ES", 3);
        var entry = new MetadataCacheEntry(
            key,
            "{\"results\":[]}",
            "\"etag-v1\"",
            DateTimeOffset.Parse("2026-08-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-08-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        await cache.StoreAsync(entry, TestContext.Current.CancellationToken);

        Assert.Equal(entry, await cache.GetAsync(key, TestContext.Current.CancellationToken));
        Assert.Null(await cache.GetAsync(key with { Language = "en-US" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Store_replaces_the_same_cache_key_transactionally()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var cache = new SqliteMetadataCache(factory);
        var key = new MetadataCacheKey("tmdb", "movie:329865", "es-ES", 3);
        var first = new MetadataCacheEntry(
            key,
            "{\"title\":\"Arrival\"}",
            null,
            DateTimeOffset.Parse("2026-08-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-08-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var replacement = first with
        {
            Payload = "{\"title\":\"La llegada\"}",
            ETag = "\"etag-v2\"",
        };

        await cache.StoreAsync(first, TestContext.Current.CancellationToken);
        await cache.StoreAsync(replacement, TestContext.Current.CancellationToken);

        Assert.Equal(replacement, await cache.GetAsync(key, TestContext.Current.CancellationToken));
    }
}
