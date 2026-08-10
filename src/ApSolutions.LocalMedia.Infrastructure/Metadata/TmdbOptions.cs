// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Infrastructure.Metadata;

public sealed class TmdbOptions
{
    public const string EnvironmentVariableName = "AP_LOCALMEDIA_TMDB_TOKEN";

    /// <summary>
    /// The longest anything obtained from TMDB may be kept, which their API terms of use set at six
    /// months.
    /// </summary>
    /// <remarks>
    /// This is not the same promise as <see cref="CacheTimeToLive"/>. The time-to-live decides when a
    /// copy is stale enough to ask again; this decides when a copy may no longer exist at all, and it
    /// holds even on the paths where asking again is impossible — no token configured, or the network
    /// refusing — which are exactly the paths where a stale copy used to be served forever.
    /// </remarks>
    public static readonly TimeSpan RetentionLimit = TimeSpan.FromDays(180);

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
