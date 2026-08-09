using ApSolutions.LocalMedia.Application.Privacy;

namespace ApSolutions.LocalMedia.Infrastructure.Privacy;

/// <summary>
/// Every connection this application can make, written down.
/// <para>
/// A component that builds an <c>HttpClient</c> and is not named here is a defect, and a test says so by
/// walking the source tree. The registry is the answer to "what does it talk to?" that does not require
/// trusting anybody's memory — including a future contributor's.
/// </para>
/// </summary>
public static class NetworkPurposeRegistry
{
    public static IReadOnlyList<NetworkPurpose> Declared { get; } =
    [
        new NetworkPurpose(
            "TmdbMetadataProvider",
            "api.themoviedb.org",
            "Fetches the metadata a person explicitly asked to identify or refresh.",
            RequiresConsent: true),
        new NetworkPurpose(
            "ArtworkCache",
            "image.tmdb.org",
            "Downloads the artwork of a title that has already been identified.",
            RequiresConsent: true),
        new NetworkPurpose(
            "GitHubReleaseUpdateProvider",
            "api.github.com",
            "Asks whether a newer release has been published, when somebody checks for updates.",
            RequiresConsent: true),
        new NetworkPurpose(
            "VerifiedUpdateDownloader",
            "github.com",
            "Downloads the release package somebody chose to install, and its own storage redirects.",
            RequiresConsent: true,
            AdditionalHosts:
            [
                "objects.githubusercontent.com",
                "*.githubusercontent.com",
            ]),
    ];

    /// <summary>The purpose declared for a component, or null when it has none.</summary>
    public static NetworkPurpose? Find(string client) =>
        string.IsNullOrWhiteSpace(client)
            ? null
            : Declared.FirstOrDefault(purpose =>
                purpose.Client.Equals(client.Trim(), StringComparison.Ordinal));

    public static NetworkPurpose Require(string client) =>
        Find(client) ?? throw new InvalidOperationException(
            $"'{client}' opens connections without a declared network purpose.");

    /// <summary>True for a host some declared purpose owns, and false for every other host.</summary>
    public static bool IsDeclaredHost(string host) =>
        !string.IsNullOrWhiteSpace(host)
        && Declared.Any(purpose => purpose.Allows(host));
}
