// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.Domain.Privacy;

/// <summary>The eight things a diagnostic report may say. There is no ninth.</summary>
public enum DiagnosticsField
{
    AppVersion,
    WindowsVersion,
    RuntimeVersion,
    Locale,
    Capabilities,
    ErrorCode,
    ErrorType,
    CountBucket,
}

/// <summary>
/// The rule a diagnostic report is built from: an allowlist, not a filter.
/// <para>
/// A filter has to imagine every bad thing in advance and misses the one nobody thought of. A closed
/// list cannot: a field that is not named simply never travels, so forgetting to exclude something is
/// not a way to leak it.
/// </para>
/// </summary>
public static partial class DiagnosticsAllowlist
{
    /// <summary>What replaces a message that carried anything private, in full.</summary>
    public const string RedactedMessage = "[redacted]";

    private static readonly string[] AllowedNames =
    [
        "appversion",
        "windowsversion",
        "runtimeversion",
        "locale",
        "capabilities",
        "errorcode",
        "errortype",
        "countbucket",
    ];

    public static IReadOnlySet<DiagnosticsField> Allowed { get; } =
        new HashSet<DiagnosticsField>(Enum.GetValues<DiagnosticsField>());

    /// <summary>
    /// The categories that must never appear, by the word a field name would use for them. They are
    /// listed so a test can assert the list itself rather than trusting a comment.
    /// </summary>
    public static IReadOnlySet<string> ProhibitedNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "path",
        "filename",
        "title",
        "contentid",
        "providerid",
        "token",
        "history",
        "library",
        "user",
        "machine",
    };

    public static bool IsAllowed(string fieldName) =>
        !string.IsNullOrWhiteSpace(fieldName)
        && AllowedNames.Contains(fieldName.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>
    /// Turns an exception into the chain of type names that produced it. The message is dropped whole
    /// rather than searched, because an exception message is written by whoever threw it and there is no
    /// way to know in advance what it decided to include.
    /// </summary>
    public static string Sanitize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var types = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            types.Add(current.GetType().FullName ?? current.GetType().Name);
            if (types.Count == 4)
            {
                break;
            }
        }

        return string.Join(" -> ", types);
    }

    /// <summary>
    /// Reduces a message that carries a path, a URI, a credential, or an identity to a single redaction
    /// marker. Trimming the offending part would leave the rest, and the rest is usually the sentence
    /// that explains what the offending part was.
    /// </summary>
    public static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        return CarriesSomethingPrivate(message) ? RedactedMessage : message;
    }

    /// <summary>
    /// Turns a count into the bucket it falls in. An exact count of a personal library is itself a fact
    /// about that library, so the exact number never leaves.
    /// </summary>
    public static string Bucket(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return value switch
        {
            0 => "0",
            1 => "1",
            <= 5 => "2-5",
            <= 20 => "6-20",
            <= 100 => "21-100",
            _ => "100+",
        };
    }

    private static bool CarriesSomethingPrivate(string message) =>
        WindowsPath().IsMatch(message)
        || UncPath().IsMatch(message)
        || Uri().IsMatch(message)
        || BearerToken().IsMatch(message)
        || Identity().IsMatch(message);

    [GeneratedRegex(@"[a-zA-Z]:[\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPath();

    [GeneratedRegex(@"\\\\[^\\]+\\", RegexOptions.CultureInvariant)]
    private static partial Regex UncPath();

    [GeneratedRegex(@"[a-zA-Z][a-zA-Z0-9+.\-]*://", RegexOptions.CultureInvariant)]
    private static partial Regex Uri();

    [GeneratedRegex(@"\b(bearer|token|password|secret|apikey)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"\b(user|usuario|machine|equipo|host)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Identity();

    /// <summary>A stamp with no time of day, so a report cannot place somebody at a keyboard.</summary>
    public static string StampDay(DateTimeOffset moment) =>
        moment.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
