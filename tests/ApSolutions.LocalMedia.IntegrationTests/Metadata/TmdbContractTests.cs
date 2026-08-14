// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

public sealed class TmdbContractTests
{
    [Fact]
    public async Task Search_prefers_Spanish_then_uses_configured_fallback_and_fresh_cache_offline()
    {
        var handler = new ScriptedHttpHandler([
            Json(HttpStatusCode.OK, "{\"results\":[]}"),
            Json(HttpStatusCode.OK, "{\"results\":[{\"id\":329865,\"title\":\"Arrival\",\"original_title\":\"Arrival\",\"release_date\":\"2016-11-11\"}]}", "\"fallback-v1\""),
        ]);
        var cache = new MemoryMetadataCache();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var provider = Provider(client, cache, token: "test-token");

        var first = await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);
        handler.ThrowWhenEmpty = true;
        var offline = await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Equal(["es-ES", "en-US"], handler.Requests.Select(request => request.Language));
        Assert.Equal("Arrival", Assert.Single(first).Title);
        Assert.Equal(first, offline);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.DoesNotContain("path", request.Uri, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("filename", request.Uri, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Stale_entry_revalidates_with_ETag_and_304_refreshes_it()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse(
            "2026-08-03T10:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture));
        var cache = new MemoryMetadataCache();
        var key = new MetadataCacheKey("tmdb", "search:movie:arrival:2016", "es-ES", 3);
        await cache.StoreAsync(new MetadataCacheEntry(
            key,
            "{\"results\":[{\"id\":329865,\"title\":\"La llegada\",\"original_title\":\"Arrival\",\"release_date\":\"2016-11-11\"}]}",
            "\"arrival-v1\"",
            clock.GetUtcNow().AddDays(-2),
            clock.GetUtcNow().AddDays(-1)), TestContext.Current.CancellationToken);
        var handler = new ScriptedHttpHandler([new HttpResponseMessage(HttpStatusCode.NotModified)]);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var provider = Provider(client, cache, "test-token", clock);

        var results = await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Equal("\"arrival-v1\"", Assert.Single(handler.Requests).IfNoneMatch);
        Assert.Equal("La llegada", Assert.Single(results).Title);
        var refreshed = await cache.GetAsync(key, TestContext.Current.CancellationToken);
        Assert.NotNull(refreshed);
        Assert.Equal(clock.GetUtcNow().AddDays(1), refreshed.ExpiresUtc);
    }

    [Fact]
    public async Task Stale_cache_is_returned_when_the_network_is_offline()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse(
            "2026-08-03T10:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture));
        var cache = new MemoryMetadataCache();
        var key = new MetadataCacheKey("tmdb", "search:movie:arrival:2016", "es-ES", 3);
        await cache.StoreAsync(new MetadataCacheEntry(
            key,
            "{\"results\":[{\"id\":329865,\"title\":\"La llegada\",\"original_title\":\"Arrival\",\"release_date\":\"2016-11-11\"}]}",
            null,
            clock.GetUtcNow().AddDays(-2),
            clock.GetUtcNow().AddDays(-1)), TestContext.Current.CancellationToken);
        var handler = new ScriptedHttpHandler([]) { ThrowWhenEmpty = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var provider = Provider(client, cache, "test-token", clock);

        var results = await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Equal("La llegada", Assert.Single(results).Title);
    }

    /// <summary>
    /// The TMDB API terms forbid keeping anything obtained from the API for longer than six months.
    /// The soft time-to-live does not enforce that on its own: when the network fails, or the token
    /// is removed, the provider falls back to whatever the cache holds, and an entry stored two years
    /// ago would still be served and kept. Retention is the hard floor under the soft expiry.
    /// </summary>
    [Fact]
    public async Task Content_kept_longer_than_the_TMDB_retention_limit_is_neither_served_nor_kept()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse(
            "2026-08-03T10:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture));
        var cache = new MemoryMetadataCache();
        var key = new MetadataCacheKey("tmdb", "search:movie:arrival:2016", "es-ES", 3);
        var storedUtc = clock.GetUtcNow() - TmdbOptions.RetentionLimit - TimeSpan.FromDays(1);
        await cache.StoreAsync(new MetadataCacheEntry(
            key,
            "{\"results\":[{\"id\":329865,\"title\":\"La llegada\",\"original_title\":\"Arrival\",\"release_date\":\"2016-11-11\"}]}",
            null,
            storedUtc,
            storedUtc.AddDays(1)), TestContext.Current.CancellationToken);
        var handler = new ScriptedHttpHandler([]) { ThrowWhenEmpty = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var provider = Provider(client, cache, "test-token", clock);

        var results = await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Empty(results);
        Assert.Null(await cache.GetAsync(key, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Without a token there is no request to fall back from, and the expired copy must still go.
    /// </summary>
    [Fact]
    public async Task Expired_retention_is_enforced_even_when_no_token_is_configured()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse(
            "2026-08-03T10:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture));
        var cache = new MemoryMetadataCache();
        var key = new MetadataCacheKey("tmdb", "search:movie:arrival:2016", "es-ES", 3);
        var storedUtc = clock.GetUtcNow() - TmdbOptions.RetentionLimit - TimeSpan.FromDays(30);
        await cache.StoreAsync(new MetadataCacheEntry(
            key,
            "{\"results\":[{\"id\":329865,\"title\":\"La llegada\",\"original_title\":\"Arrival\",\"release_date\":\"2016-11-11\"}]}",
            null,
            storedUtc,
            clock.GetUtcNow().AddYears(5)), TestContext.Current.CancellationToken);
        var handler = new ScriptedHttpHandler([]) { ThrowWhenEmpty = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var provider = Provider(client, cache, token: null, clock);

        var results = await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Empty(results);
        Assert.Empty(handler.Requests);
        Assert.Null(await cache.GetAsync(key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Retry_After_and_server_backoff_are_honored_without_uncancelable_waits()
    {
        var delays = new List<TimeSpan>();
        var limiter = new TmdbRateLimiter((delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });
        var tooMany = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooMany.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
        var handler = new ScriptedHttpHandler([
            tooMany,
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            Json(HttpStatusCode.OK, "{\"results\":[]}"),
        ]);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var provider = Provider(client, new MemoryMetadataCache(), "test-token", limiter: limiter);

        await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", null),
            TestContext.Current.CancellationToken);

        Assert.Equal([TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(2)], delays);

        var cancelHandler = new ScriptedHttpHandler([new HttpResponseMessage(HttpStatusCode.InternalServerError)]);
        using var cancelClient = new HttpClient(cancelHandler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var delayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelLimiter = new TmdbRateLimiter((_, token) =>
        {
            delayEntered.SetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        var cancelProvider = Provider(cancelClient, new MemoryMetadataCache(), "test-token", limiter: cancelLimiter);
        using var cancellation = new CancellationTokenSource();
        var pending = cancelProvider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", null),
            cancellation.Token);
        await delayEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task Missing_token_keeps_local_mode_and_secret_is_redacted()
    {
        var handler = new ScriptedHttpHandler([]) { ThrowWhenEmpty = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var canary = string.Concat("secret-", "canary-token");
        var options = new TmdbOptions(canary);
        Assert.DoesNotContain(canary, options.ToString(), StringComparison.Ordinal);
        var provider = Provider(client, new MemoryMetadataCache(), token: null);

        var results = await provider.SearchAsync(
            new MetadataSearchQuery("Arrival", 2016, MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Empty(results);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Details_are_requested_in_the_primary_language()
    {
        var handler = new ScriptedHttpHandler([
            Json(HttpStatusCode.OK, "{\"id\":329865,\"title\":\"La llegada\",\"original_title\":\"Arrival\",\"overview\":\"Una lingüista investiga una visita.\",\"release_date\":\"2016-11-11\",\"genres\":[{\"name\":\"Ciencia ficción\"}],\"poster_path\":\"/poster.jpg\",\"backdrop_path\":\"/backdrop.jpg\"}"),
        ]);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/") };
        var provider = Provider(client, new MemoryMetadataCache(), "test-token");

        var details = await provider.GetDetailsAsync(
            new MetadataReference("tmdb", "movie:329865", MetadataContentKind.Movie),
            new MetadataLanguage("es-ES", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(details);
        Assert.Equal("La llegada", details.Title);
        Assert.Equal(["Ciencia ficción"], details.Genres);
        Assert.Equal("es-ES", Assert.Single(handler.Requests).Language);
    }

    [Fact]
    public void Credits_surface_contains_required_TMDB_attribution()
    {
        var repositoryRoot = RepositoryLayout.Root;
        var credits = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "About",
            "CreditsView.axaml"));

        Assert.Contains("TMDB", credits, StringComparison.Ordinal);
        Assert.Contains("AboutTmdbAttribution", credits, StringComparison.Ordinal);
    }

    /// <summary>
    /// TMDB dictates the wording, not the gist of it.
    /// </summary>
    /// <remarks>
    /// The attribution read "uses the TMDB API but is not endorsed or certified", which is a summary
    /// of the required sentence rather than the sentence. Their terms name the phrase to display, so
    /// the check pins the parts that were missing — the APIs in the plural, and the third verb — in
    /// both languages, because half a product's users read the other one.
    /// </remarks>
    [Theory]
    [InlineData("Strings.en.axaml", "This product uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.")]
    [InlineData("Strings.es.axaml", "Este producto usa TMDB y las API de TMDB, pero no está avalado, certificado ni aprobado de ningún otro modo por TMDB.")]
    public void The_attribution_string_matches_the_wording_TMDB_requires(string resourceFile, string required)
    {
        var resources = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Resources",
            resourceFile));

        Assert.Contains(
            $"<x:String x:Key=\"AboutTmdbAttribution\">{required}</x:String>",
            resources,
            StringComparison.Ordinal);
    }

    private static TmdbMetadataProvider Provider(
        HttpClient client,
        IMetadataCache cache,
        string? token,
        TimeProvider? clock = null,
        TmdbRateLimiter? limiter = null) => new(
            client,
            cache,
            new TmdbOptions(token),
            limiter ?? new TmdbRateLimiter((_, _) => Task.CompletedTask),
            clock ?? new MutableTimeProvider(DateTimeOffset.Parse(
                "2026-08-01T10:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture)));

    private static HttpResponseMessage Json(HttpStatusCode status, string json, string? etag = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (etag is not null)
        {
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        }

        return response;
    }

    private sealed record RecordedRequest(string Uri, string? Language, string? IfNoneMatch);

    private sealed class ScriptedHttpHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<RecordedRequest> Requests { get; } = [];

        public bool ThrowWhenEmpty { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            var language = request.RequestUri is null
                ? null
                : ReadQueryValue(request.RequestUri.Query, "language");
            Requests.Add(new RecordedRequest(
                uri,
                language,
                request.Headers.IfNoneMatch.SingleOrDefault()?.Tag));
            if (_responses.Count == 0 && ThrowWhenEmpty)
            {
                throw new HttpRequestException("Simulated offline network.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        private static string? ReadQueryValue(string query, string name) => query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .Where(parts => string.Equals(Uri.UnescapeDataString(parts[0]), name, StringComparison.Ordinal))
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .SingleOrDefault();
    }

    private sealed class MemoryMetadataCache : IMetadataCache
    {
        private readonly Dictionary<MetadataCacheKey, MetadataCacheEntry> _entries = [];

        public Task<MetadataCacheEntry?> GetAsync(
            MetadataCacheKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.GetValueOrDefault(key));

        public Task StoreAsync(MetadataCacheEntry entry, CancellationToken cancellationToken = default)
        {
            _entries[entry.Key] = entry;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(MetadataCacheKey key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
