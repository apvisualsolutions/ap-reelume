// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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

    /// <summary>
    /// Hosts this application hands to another application and never connects to itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate list because it is a separate promise. <see cref="Declared"/> answers "what does
    /// this process open?", and putting a trailer's host in it would answer that question wrongly in
    /// the dangerous direction — claiming a connection that is never made, and widening
    /// <see cref="IsDeclaredHost"/>, which is what the network canary trusts.
    /// </para>
    /// <para>
    /// Naming them somewhere is not optional either: an address that reaches a browser is still
    /// somewhere a person ends up because of this application, and the source-tree check exists
    /// precisely so no host appears in the code without a reason written next to it. So: declared
    /// here, absent from the connection list, and named in the privacy statement under its own
    /// heading.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<HandedOffDestination> HandedOff { get; } =
    [
        new HandedOffDestination(
            "TrailerLinkPolicy",
            "www.youtube.com",
            "Opens the trailer of a title in the browser, when somebody presses the button for it. "
            + "The application makes no connection: the address goes to the operating system and the "
            + "browser is what goes there."),
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
    /// <remarks>
    /// Deliberately blind to <see cref="HandedOff"/>: a host this application never connects to must
    /// not widen the check that says which connections are allowed.
    /// </remarks>
    public static bool IsDeclaredHost(string host) =>
        !string.IsNullOrWhiteSpace(host)
        && Declared.Any(purpose => purpose.Allows(host));

    /// <summary>True for a host that is handed to another application rather than connected to.</summary>
    public static bool IsHandedOffHost(string host) =>
        !string.IsNullOrWhiteSpace(host)
        && HandedOff.Any(destination =>
            destination.Host.Equals(host.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One address this application can ask the operating system to open, with what opens it and why.
/// </summary>
public sealed record HandedOffDestination(string Client, string Host, string Purpose);
