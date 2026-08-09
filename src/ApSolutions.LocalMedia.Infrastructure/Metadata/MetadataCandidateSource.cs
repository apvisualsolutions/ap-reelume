using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Infrastructure.Metadata;

/// <summary>
/// Where the candidates for one file come from.
/// <para>
/// The local pass is whatever the provider already answered and cached, so a library that has been
/// identified once keeps working with the network unplugged. The remote pass is the only one that can
/// open a connection, and it asks permission first: identification is something a person requests, so
/// a scan that finds a thousand files must never turn into a thousand requests nobody asked for.
/// </para>
/// </summary>
public sealed class MetadataCandidateSource : IIdentificationCandidateSource
{
    private readonly IMetadataProvider _provider;
    private readonly IMetadataCache _cache;
    private readonly MetadataLanguage _language;
    private readonly Func<bool> _isRemoteAllowed;

    public MetadataCandidateSource(
        IMetadataProvider provider,
        IMetadataCache cache,
        MetadataLanguage language,
        Func<bool>? isRemoteAllowed = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _isRemoteAllowed = isRemoteAllowed ?? (() => false);
    }

    /// <summary>
    /// Candidates that need no connection. The provider is asked with its own cache in front of it and
    /// a cache miss simply produces nothing, because an offline library must degrade rather than stall.
    /// </summary>
    public async Task<IReadOnlyList<CandidateFacts>> GetLocalAsync(
        ParsedMediaName parsed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        var query = BuildQuery(parsed);
        if (query is null || !await IsCachedAsync(query, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        return Describe(await _provider.SearchAsync(query, _language, cancellationToken).ConfigureAwait(false), parsed);
    }

    /// <summary>Candidates that require a connection, and only when one has been consented to.</summary>
    public async Task<IReadOnlyList<CandidateFacts>> GetRemoteAsync(
        ParsedMediaName parsed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        var query = BuildQuery(parsed);
        if (query is null || !_isRemoteAllowed())
        {
            return [];
        }

        return Describe(await _provider.SearchAsync(query, _language, cancellationToken).ConfigureAwait(false), parsed);
    }

    private static MetadataSearchQuery? BuildQuery(ParsedMediaName parsed) =>
        string.IsNullOrWhiteSpace(parsed.CleanTitle)
            ? null
            : new MetadataSearchQuery(
                parsed.CleanTitle,
                parsed.Year,
                parsed.Kind == ParsedMediaKind.Episode ? MetadataContentKind.Show : MetadataContentKind.Movie);

    private async Task<bool> IsCachedAsync(MetadataSearchQuery query, CancellationToken cancellationToken)
    {
        foreach (var language in _language.OrderedValues())
        {
            var key = new MetadataCacheKey("tmdb", BuildSearchCacheKey(query), language, ProviderVersion);
            if (await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false) is not null)
            {
                return true;
            }
        }

        return false;
    }

    // The provider owns this shape; the source only needs to ask whether an answer is already stored.
    private static string BuildSearchCacheKey(MetadataSearchQuery query)
    {
        var kind = query.Kind == MetadataContentKind.Movie ? "movie" : "tv";
        var normalizedTitle = string.Join(
            '-',
            query.Title.Trim().ToLowerInvariant().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var year = query.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
        return $"search:{kind}:{normalizedTitle}:{year}";
    }

    private const int ProviderVersion = 3;

    private static IReadOnlyList<CandidateFacts> Describe(
        IReadOnlyList<MetadataSearchResult> results,
        ParsedMediaName parsed) =>
    [
        .. results.Select(result => new CandidateFacts(
            CandidateId.FromStableKey(result.Reference.Key),
            result.Reference.Key,
            result.Reference.Kind == MetadataContentKind.Movie
                ? CandidateContentKind.Movie
                : CandidateContentKind.Episode,
            TitleSimilarity(parsed.CleanTitle, result.Title, result.OriginalTitle),
            parsed.Season is null ? null : 1.0,
            parsed.Episode is null ? null : 1.0,
            YearMatch(parsed.Year, result.ReleaseYear),
            DurationMatch: null))
    ];

    /// <summary>
    /// How close two titles are, on the characters that survive normalisation. It never claims more
    /// than it can see: a title the provider answered in another language scores on its original too.
    /// </summary>
    private static double TitleSimilarity(string parsedTitle, string title, string? originalTitle)
    {
        var candidates = originalTitle is null ? [title] : new[] { title, originalTitle };
        return candidates.Max(candidate => Similarity(Normalize(parsedTitle), Normalize(candidate)));
    }

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1;
        }

        var distance = LevenshteinDistance(left, right);
        return Math.Max(0, 1.0 - ((double)distance / Math.Max(left.Length, right.Length)));
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var substitution = previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1);
                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static string Normalize(string value) =>
        new([.. value.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    private static double? YearMatch(int? parsedYear, int? candidateYear) =>
        parsedYear is null || candidateYear is null
            ? null
            : parsedYear == candidateYear ? 1.0 : Math.Max(0, 1.0 - (Math.Abs(parsedYear.Value - candidateYear.Value) / 5.0));
}
