namespace ApSolutions.LocalMedia.Domain.Updates;

/// <summary>
/// A release as some source describes it. Nothing here is trusted: this is the claim, and
/// <see cref="UpdatePolicy"/> is what decides whether the claim is worth acting on.
/// </summary>
/// <remarks>
/// The summary travels in both languages because confirming an update means reading what changed,
/// and a summary in a language the person does not read turns the confirmation into a formality.
/// </remarks>
public sealed record UpdateRelease(
    string? Version,
    string? Runtime,
    string? Url,
    string? Sha256,
    long SizeInBytes,
    string? SummaryEs,
    string? SummaryEn)
{
    /// <summary>
    /// True only when whoever produced this description proved the checksums were signed by the
    /// release key (SEC-003). It is a verdict, not a claim from the source: the provider sets it
    /// after verifying the minisign signature against the key this binary embeds.
    /// </summary>
    public bool Sha256Signed { get; init; }
}

/// <summary>Why a release was not offered. <see cref="None"/> means it was.</summary>
public enum UpdateRejection
{
    None,

    /// <summary>The source has no release at all. An answer, not a failure.</summary>
    NoReleaseAvailable,

    /// <summary>The download would not be protected in transit.</summary>
    InsecureDownload,

    /// <summary>
    /// The download, or one of its redirects, would leave the hosts the network purpose registry
    /// declares. The address that is actually fetched has to be one the promise covers.
    /// </summary>
    UndeclaredHost,

    /// <summary>No usable SHA-256, so what arrived could never be checked against what was promised.</summary>
    UnusableHash,

    /// <summary>
    /// The checksums were not proved signed by the release key, so the hash and the package it
    /// vouches for come from the same unsigned answer (SEC-003).
    /// </summary>
    UnsignedChecksums,

    /// <summary>The package targets another architecture and would install but not start.</summary>
    WrongRuntime,

    /// <summary>Same version or older.</summary>
    NotNewer,

    /// <summary>No declared size, so an interrupted download could not be told from a complete one.</summary>
    UndeclaredSize,

    /// <summary>The summary is missing in at least one language.</summary>
    IncompleteSummary,
}

/// <summary>The answer, with the reason it is that answer.</summary>
public sealed record UpdateDecision(bool IsOffered, UpdateRejection Rejection, string Reason)
{
    public static UpdateDecision Offered(UpdateRelease release) =>
        new(true, UpdateRejection.None, $"Version {release.Version} is offered for {release.Runtime}.");

    public static UpdateDecision Refused(UpdateRejection rejection, string reason) =>
        new(false, rejection, reason);
}

/// <summary>
/// Whether the application is willing to be replaced by what a source is offering.
/// </summary>
/// <remarks>
/// Every rule runs before anything is downloaded. A check that runs afterwards has already let the
/// network decide what got written to disk, and by then the only remaining question is whether to
/// delete it.
///
/// The order of the rules is the order of severity rather than the order of the fields, because a
/// manifest that fails several is reported by the one that matters: telling someone their release
/// notes are missing, when the download was going to arrive over plain HTTP, buries the finding.
/// </remarks>
public static class UpdatePolicy
{
    public static UpdateDecision Decide(UpdateRelease? release, string installedVersion, string installedRuntime)
    {
        if (release is null)
        {
            return UpdateDecision.Refused(
                UpdateRejection.NoReleaseAvailable,
                "The update source published no release, so there is nothing to offer.");
        }

        if (!IsHttps(release.Url))
        {
            return UpdateDecision.Refused(
                UpdateRejection.InsecureDownload,
                $"The download is not an HTTPS address: '{release.Url}'.");
        }

        if (!IsSha256(release.Sha256))
        {
            return UpdateDecision.Refused(
                UpdateRejection.UnusableHash,
                "The release declares no usable SHA-256, so what arrived could not be checked against it.");
        }

        if (!release.Sha256Signed)
        {
            // A hash that travels unsigned next to the package it vouches for proves only that
            // both came from the same place. The signature is what makes the hash worth checking.
            return UpdateDecision.Refused(
                UpdateRejection.UnsignedChecksums,
                "The release's checksums are not signed by the release key, so the hash vouches for nothing.");
        }

        if (!string.Equals(release.Runtime, installedRuntime, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateDecision.Refused(
                UpdateRejection.WrongRuntime,
                $"The release targets '{release.Runtime}' and this installation is '{installedRuntime}'.");
        }

        if (!IsNewerThan(release.Version, installedVersion))
        {
            return UpdateDecision.Refused(
                UpdateRejection.NotNewer,
                $"Version '{release.Version}' is not newer than the installed '{installedVersion}'.");
        }

        if (release.SizeInBytes <= 0)
        {
            return UpdateDecision.Refused(
                UpdateRejection.UndeclaredSize,
                "The release declares no size, so an interrupted download could not be told from a complete one.");
        }

        if (string.IsNullOrWhiteSpace(release.SummaryEs) || string.IsNullOrWhiteSpace(release.SummaryEn))
        {
            return UpdateDecision.Refused(
                UpdateRejection.IncompleteSummary,
                "The release does not summarise what changed in both Spanish and English.");
        }

        return UpdateDecision.Offered(release);
    }

    private static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    /// <summary>
    /// Compares the release's version with the installed one.
    /// </summary>
    /// <remarks>
    /// A pre-release is never newer than the release it precedes, and never newer than anything
    /// here: this application ships stable versions, so an unparseable or pre-release version is the
    /// one case where refusing costs nothing and accepting could downgrade someone.
    /// </remarks>
    private static bool IsNewerThan(string? offered, string installed) =>
        TryParse(offered, out var candidate)
        && TryParse(installed, out var current)
        && candidate > current;

    private static bool TryParse(string? value, out Version parsed)
    {
        parsed = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('-', StringComparison.Ordinal))
        {
            return false;
        }

        if (!Version.TryParse(value.Trim(), out var version))
        {
            return false;
        }

        parsed = version;
        return true;
    }
}
