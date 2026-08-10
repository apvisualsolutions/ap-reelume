// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApSolutions.LocalMedia.Application.Privacy;

/// <summary>Whether diagnostics may be produced at all, and since when.</summary>
public sealed record DiagnosticsConsent(bool IsGranted, DateTimeOffset? GrantedUtc);

/// <summary>One thing that went wrong, as the application knows it before anything is sanitized.</summary>
public sealed record DiagnosticsErrorSample(string Code, Exception? Exception, int Occurrences);

/// <summary>
/// Everything the builder is given. It deliberately includes history and search terms, which the report
/// must never carry: handing them over and watching them not come out the other side is the only way to
/// prove the allowlist is doing the work.
/// </summary>
public sealed record DiagnosticsInputs(
    string AppVersion,
    string WindowsVersion,
    string RuntimeVersion,
    string Locale,
    bool HardwareAccelerationAvailable,
    bool HdrDisplayPresent,
    int AudioEndpointCount,
    int LibraryItemCount,
    int RootCount,
    IReadOnlyList<DiagnosticsErrorSample> Errors,
    IReadOnlyList<string> History,
    IReadOnlyList<string> SearchTerms);

public sealed record DiagnosticsCapabilities(bool HardwareAcceleration, bool HdrDisplay, string AudioEndpoints);

public sealed record DiagnosticsErrorEntry(string Code, string Type, string Occurrences);

public sealed record DiagnosticsCount(string Name, string Bucket);

/// <summary>
/// The whole report, and the whole of what can ever be written. Every member is a closed type with
/// plain fields; there is no dictionary, no object, and nothing that could carry an entity along.
/// </summary>
public sealed record DiagnosticsReport(
    int FormatVersion,
    string CreatedUtc,
    string AppVersion,
    string WindowsVersion,
    string RuntimeVersion,
    string Locale,
    DiagnosticsCapabilities Capabilities,
    IReadOnlyList<DiagnosticsErrorEntry> Errors,
    IReadOnlyList<DiagnosticsCount> Counts)
{
    public const int CurrentFormatVersion = 1;

    public const string FileName = "diagnostics.json";
}

/// <summary>Builds a report, or refuses to build one at all.</summary>
public interface IDiagnosticsBuilder
{
    /// <summary>The report, or <see langword="null"/> when consent has not been given.</summary>
    DiagnosticsReport? Build(DiagnosticsConsent consent, DiagnosticsInputs inputs);
}

/// <summary>
/// One declared reason for one connection: which component makes it, where it goes, why, and whether it
/// waits for consent. The list of them belongs to the adapter that owns the clients; the shape belongs
/// here so the settings screen can show it without knowing anything about HTTP.
/// </summary>
public sealed record NetworkPurpose(
    string Client,
    string Host,
    string Reason,
    bool RequiresConsent,
    IReadOnlyList<string>? AdditionalHosts = null)
{
    /// <summary>
    /// Every host this purpose may touch: the named one plus the redirect hosts it declares. The
    /// list exists so the registry can tell the whole truth — an asset that redirects to a host the
    /// registry never named is a connection the promise never covered.
    /// </summary>
    public IReadOnlyList<string> Hosts => [Host, .. AdditionalHosts ?? []];

    /// <summary>Whether this purpose covers a host, honouring one leading wildcard label.</summary>
    public bool Allows(string host) =>
        !string.IsNullOrWhiteSpace(host)
        && Hosts.Any(declared => Matches(declared, host.Trim()));

    /// <summary>
    /// One pattern against one host. A pattern like <c>*.githubusercontent.com</c> covers every
    /// subdomain and never the bare domain, so the wildcard cannot widen past what was written.
    /// </summary>
    public static bool Matches(string pattern, string host) =>
        pattern.StartsWith("*.", StringComparison.Ordinal)
            ? host.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
            : host.Equals(pattern, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Where the answer to "may diagnostics be produced?" is kept between runs.</summary>
public interface IPrivacySettings
{
    DiagnosticsConsent Current { get; }

    void Save(DiagnosticsConsent consent);
}

/// <summary>
/// The serializer the report uses.
/// <para>
/// It is wired to a source-generated context and to nothing else. That is the difference between a
/// promise and a guarantee: asking it to write a type the contract never declared does not quietly
/// reflect over that type, it fails. The test that hands it a domain entity is the proof.
/// </para>
/// </summary>
public static class DiagnosticsSerialization
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        TypeInfoResolver = DiagnosticsJsonContext.Default,
        WriteIndented = true,
    };

    public static string Serialize(DiagnosticsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, DiagnosticsJsonContext.Default.DiagnosticsReport);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(DiagnosticsReport))]
internal sealed partial class DiagnosticsJsonContext : JsonSerializerContext;
