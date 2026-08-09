using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Domain.Privacy;

namespace ApSolutions.LocalMedia.Infrastructure.Privacy;

/// <summary>
/// Turns what the application knows into what it is allowed to say.
/// <para>
/// Nothing here copies an input across untouched except the four version strings the allowlist names.
/// Counts become buckets, exceptions become type names, and the history and search terms it is handed
/// are simply never read.
/// </para>
/// </summary>
public sealed class AllowlistedDiagnosticsBuilder : IDiagnosticsBuilder
{
    public DiagnosticsReport? Build(DiagnosticsConsent consent, DiagnosticsInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(consent);
        ArgumentNullException.ThrowIfNull(inputs);
        if (!consent.IsGranted)
        {
            return null;
        }

        return new DiagnosticsReport(
            DiagnosticsReport.CurrentFormatVersion,
            DiagnosticsAllowlist.StampDay(consent.GrantedUtc ?? DateTimeOffset.UnixEpoch),
            inputs.AppVersion,
            inputs.WindowsVersion,
            inputs.RuntimeVersion,
            inputs.Locale,
            new DiagnosticsCapabilities(
                inputs.HardwareAccelerationAvailable,
                inputs.HdrDisplayPresent,
                DiagnosticsAllowlist.Bucket(inputs.AudioEndpointCount)),
            [.. inputs.Errors.Select(Describe)],
            [
                new DiagnosticsCount("libraryItems", DiagnosticsAllowlist.Bucket(inputs.LibraryItemCount)),
                new DiagnosticsCount("roots", DiagnosticsAllowlist.Bucket(inputs.RootCount)),
                new DiagnosticsCount("errors", DiagnosticsAllowlist.Bucket(inputs.Errors.Count)),
            ]);
    }

    /// <summary>
    /// One error as a code, a chain of type names, and a bucket. The code is the application's own and
    /// is checked against the same message rules as anything else, because a code is still a string
    /// somebody wrote.
    /// </summary>
    private static DiagnosticsErrorEntry Describe(DiagnosticsErrorSample sample) => new(
        DiagnosticsAllowlist.SanitizeMessage(sample.Code),
        sample.Exception is null ? "none" : DiagnosticsAllowlist.Sanitize(sample.Exception),
        DiagnosticsAllowlist.Bucket(Math.Max(sample.Occurrences, 0)));
}
