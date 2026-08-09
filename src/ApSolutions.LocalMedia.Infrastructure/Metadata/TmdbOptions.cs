namespace ApSolutions.LocalMedia.Infrastructure.Metadata;

public sealed class TmdbOptions
{
    public const string EnvironmentVariableName = "AP_LOCALMEDIA_TMDB_TOKEN";

    public TmdbOptions(
        string? accessToken,
        int providerVersion = 3,
        TimeSpan? cacheTimeToLive = null,
        int maximumRetries = 2)
    {
        AccessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken;
        ProviderVersion = providerVersion > 0
            ? providerVersion
            : throw new ArgumentOutOfRangeException(nameof(providerVersion));
        CacheTimeToLive = cacheTimeToLive ?? TimeSpan.FromDays(1);
        MaximumRetries = maximumRetries >= 0
            ? maximumRetries
            : throw new ArgumentOutOfRangeException(nameof(maximumRetries));
    }

    public string? AccessToken { get; }

    public int ProviderVersion { get; }

    public TimeSpan CacheTimeToLive { get; }

    public int MaximumRetries { get; }

    public static TmdbOptions FromExternalSources(Func<string?>? ciResourceToken = null) =>
        new(Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? ciResourceToken?.Invoke());

    public override string ToString() =>
        $"TMDB v{ProviderVersion}; token={(AccessToken is null ? "absent" : "configured")}";
}
