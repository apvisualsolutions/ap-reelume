// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Infrastructure.Metadata;

public sealed class TmdbMetadataProvider : IMetadataProvider
{
    private const string ProviderName = "tmdb";
    private readonly HttpClient _httpClient;
    private readonly IMetadataCache _cache;
    private readonly TmdbOptions _options;
    private readonly TmdbRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public TmdbMetadataProvider(
        HttpClient httpClient,
        IMetadataCache cache,
        TmdbOptions options,
        TmdbRateLimiter rateLimiter,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        MetadataSearchQuery query,
        MetadataLanguage language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Title);
        foreach (var requestedLanguage in language.OrderedValues())
        {
            var cacheKey = new MetadataCacheKey(
                ProviderName,
                BuildSearchCacheKey(query),
                requestedLanguage,
                _options.ProviderVersion);
            var payload = await GetPayloadAsync(
                cacheKey,
                BuildSearchUri(query, requestedLanguage),
                cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                continue;
            }

            var results = ParseSearchResults(payload, query.Kind, requestedLanguage);
            if (results.Count > 0)
            {
                return results;
            }
        }

        return [];
    }

    public async Task<MetadataDetails?> GetDetailsAsync(
        MetadataReference reference,
        MetadataLanguage language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(language);
        if (!string.Equals(reference.Provider, ProviderName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The reference does not belong to TMDB.", nameof(reference));
        }

        foreach (var requestedLanguage in language.OrderedValues())
        {
            var cacheKey = new MetadataCacheKey(
                ProviderName,
                reference.Key,
                requestedLanguage,
                _options.ProviderVersion);
            var payload = await GetPayloadAsync(
                cacheKey,
                BuildDetailsUri(reference, requestedLanguage),
                cancellationToken).ConfigureAwait(false);
            if (payload is not null)
            {
                return ParseDetails(payload, reference, requestedLanguage);
            }
        }

        return null;
    }

    private async Task<string?> GetPayloadAsync(
        MetadataCacheKey key,
        string requestUri,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        if (cached is not null && now - cached.StoredUtc >= TmdbOptions.RetentionLimit)
        {
            // TMDB's API terms cap how long their content may be kept. Past that, the copy is not
            // merely stale — it may not exist, so it goes before anything decides to serve it.
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            cached = null;
        }

        if (cached is not null && cached.ExpiresUtc > now)
        {
            return cached.Payload;
        }

        if (_options.AccessToken is null)
        {
            return cached?.Payload;
        }

        FetchResult fetched;
        try
        {
            fetched = await _rateLimiter.RunAsync(
                token => SendWithRetryAsync(requestUri, cached?.ETag, token),
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return cached?.Payload;
        }

        if (fetched.StatusCode == HttpStatusCode.NotModified && cached is not null)
        {
            var refreshed = cached with
            {
                StoredUtc = now,
                ExpiresUtc = now.Add(_options.CacheTimeToLive),
            };
            await _cache.StoreAsync(refreshed, cancellationToken).ConfigureAwait(false);
            return refreshed.Payload;
        }

        if (fetched.Payload is null)
        {
            return cached?.Payload;
        }

        var entry = new MetadataCacheEntry(
            key,
            fetched.Payload,
            fetched.ETag,
            now,
            now.Add(_options.CacheTimeToLive));
        await _cache.StoreAsync(entry, cancellationToken).ConfigureAwait(false);
        return entry.Payload;
    }

    private async Task<FetchResult> SendWithRetryAsync(
        string requestUri,
        string? etag,
        CancellationToken cancellationToken)
    {
        var serverFailureCount = 0;
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (etag is not null)
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (ShouldRetry(response.StatusCode) && attempt < _options.MaximumRetries)
            {
                var delay = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2)
                    : TimeSpan.FromSeconds(Math.Pow(2, ++serverFailureCount));
                await _rateLimiter.WaitForRetryAsync(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new FetchResult(response.StatusCode, null, etag);
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new FetchResult(response.StatusCode, payload, response.Headers.ETag?.Tag);
        }
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static string BuildSearchCacheKey(MetadataSearchQuery query)
    {
        var kind = query.Kind == MetadataContentKind.Movie ? "movie" : "tv";
        var normalizedTitle = string.Join(
            '-',
            query.Title.Trim().ToLowerInvariant().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return $"search:{kind}:{normalizedTitle}:{query.Year?.ToString(CultureInfo.InvariantCulture) ?? "none"}";
    }

    private static string BuildSearchUri(MetadataSearchQuery query, string language)
    {
        var kind = query.Kind == MetadataContentKind.Movie ? "movie" : "tv";
        var yearParameter = query.Kind == MetadataContentKind.Movie ? "year" : "first_air_date_year";
        var year = query.Year.HasValue
            ? $"&{yearParameter}={query.Year.Value.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"3/search/{kind}?query={Uri.EscapeDataString(query.Title.Trim())}&language={Uri.EscapeDataString(language)}{year}";
    }

    private static string BuildDetailsUri(MetadataReference reference, string language)
    {
        var expectedPrefix = reference.Kind == MetadataContentKind.Movie ? "movie:" : "tv:";
        if (!reference.Key.StartsWith(expectedPrefix, StringComparison.Ordinal)
            || !long.TryParse(reference.Key[expectedPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || id <= 0)
        {
            throw new ArgumentException("The TMDB reference key is invalid.", nameof(reference));
        }

        var kind = reference.Kind == MetadataContentKind.Movie ? "movie" : "tv";
        return $"3/{kind}/{id.ToString(CultureInfo.InvariantCulture)}?language={Uri.EscapeDataString(language)}";
    }

    private static List<MetadataSearchResult> ParseSearchResults(
        string payload,
        MetadataContentKind kind,
        string language)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("results", out var resultsElement))
        {
            return [];
        }

        var titleProperty = kind == MetadataContentKind.Movie ? "title" : "name";
        var originalTitleProperty = kind == MetadataContentKind.Movie ? "original_title" : "original_name";
        var dateProperty = kind == MetadataContentKind.Movie ? "release_date" : "first_air_date";
        var keyPrefix = kind == MetadataContentKind.Movie ? "movie:" : "tv:";
        var results = new List<MetadataSearchResult>();
        foreach (var item in resultsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement)
                || !item.TryGetProperty(titleProperty, out var titleElement)
                || string.IsNullOrWhiteSpace(titleElement.GetString()))
            {
                continue;
            }

            results.Add(new MetadataSearchResult(
                new MetadataReference(ProviderName, $"{keyPrefix}{idElement.GetInt64()}", kind),
                language,
                titleElement.GetString()!,
                ReadOptionalString(item, originalTitleProperty),
                ReadYear(item, dateProperty)));
        }

        return results;
    }

    private static MetadataDetails MetadataDetailsFromDocument(
        JsonElement root,
        MetadataReference reference,
        string language)
    {
        var titleProperty = reference.Kind == MetadataContentKind.Movie ? "title" : "name";
        var originalTitleProperty = reference.Kind == MetadataContentKind.Movie ? "original_title" : "original_name";
        var dateProperty = reference.Kind == MetadataContentKind.Movie ? "release_date" : "first_air_date";
        var genres = root.TryGetProperty("genres", out var genresElement)
            ? genresElement.EnumerateArray()
                .Select(genre => ReadOptionalString(genre, "name"))
                .Where(name => name is not null)
                .Select(name => name!)
                .ToArray()
            : [];
        return new MetadataDetails(
            reference,
            language,
            root.GetProperty(titleProperty).GetString() ?? string.Empty,
            ReadOptionalString(root, originalTitleProperty),
            ReadOptionalString(root, "overview"),
            ReadYear(root, dateProperty),
            genres,
            ReadOptionalString(root, "poster_path"),
            ReadOptionalString(root, "backdrop_path"));
    }

    private static MetadataDetails ParseDetails(
        string payload,
        MetadataReference reference,
        string language)
    {
        using var document = JsonDocument.Parse(payload);
        return MetadataDetailsFromDocument(document.RootElement, reference, language);
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? ReadYear(JsonElement element, string propertyName)
    {
        var value = ReadOptionalString(element, propertyName);
        return value is not null
            && value.Length >= 4
            && int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                ? year
                : null;
    }

    private sealed record FetchResult(HttpStatusCode StatusCode, string? Payload, string? ETag);
}
