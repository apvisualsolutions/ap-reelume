// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>One row of the canonical scope record.</summary>
internal sealed record FeatureRow(string Id, string Target, string Status, string Criterion, string Evidence)
{
    /// <summary>The evidence documents the row links to, as repository-relative paths.</summary>
    public string[] EvidenceLinks { get; } =
        [.. FeatureMatrix.LinkPattern.Matches(Evidence)
            .Select(match => match.Groups["target"].Value.Trim())
            .Where(target => !target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Select(target => "docs/" + (target.Split('#', 2)[0]))
            .Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// Reads <c>docs/FEATURES.md</c> as data. Every suite that asks a question about scope asks it here,
/// so there is one parser rather than four slightly different ones.
/// </summary>
internal static class FeatureMatrix
{
    internal static readonly Regex LinkPattern =
        new(@"\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex RowPattern = new(
        @"(?m)^\|\s*(?<id>[A-Z0-9]+-[0-9]+)\s*\|(?<feature>[^|]*)\|\s*(?<target>MVP|STABLE|POST_STABLE)\s*\|\s*(?<status>[A-Z_]+)\s*\|(?<criterion>[^|]*)\|(?<evidence>.*)\|\s*$",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>The statuses a commitment can carry without anything further being owed.</summary>
    public static readonly string[] SettledStatuses = ["VERIFIED", "OUT_OF_SCOPE"];

    public static IReadOnlyList<FeatureRow> Rows { get; } = Load();

    public static IReadOnlyList<FeatureRow> Mvp { get; } =
        [.. Rows.Where(row => row.Target == "MVP")];

    /// <summary>The manifest that maps every MVP commitment to how it was resolved.</summary>
    public static JsonElement Manifest()
    {
        var path = RepositoryLayout.PathFromRoot("docs/evidence/mvp/verification-manifest.json");
        Assert.True(
            File.Exists(path),
            "docs/evidence/mvp/verification-manifest.json is missing, so no commitment can be traced to its evidence.");
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    public static IEnumerable<JsonElement> ManifestFeatures() =>
        Manifest().GetProperty("features").EnumerateArray();

    private static IReadOnlyList<FeatureRow> Load()
    {
        var text = File.ReadAllText(RepositoryLayout.PathFromRoot("docs/FEATURES.md"));
        return
        [
            .. RowPattern.Matches(text).Select(match => new FeatureRow(
                match.Groups["id"].Value,
                match.Groups["target"].Value,
                match.Groups["status"].Value,
                match.Groups["criterion"].Value.Trim(),
                match.Groups["evidence"].Value.Trim())),
        ];
    }
}
