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
/// <remarks>
/// The releases and statuses are read from the matrix's own two legend tables rather than written
/// here, for the reason <c>eng/list-pending.ps1</c> already reads them that way: on 2026-09-03 this
/// parser named <c>MVP|STABLE|POST_STABLE</c> in its pattern, and five rows written with a fourth
/// release simply did not match. They were not reported as unparsed — they were absent, so every
/// gate that reads this file measured 60 rows and called it the whole matrix, one of them
/// <c>VERIFIED</c> with nothing checking its evidence. A pattern that recognises only what it was
/// told about answers a narrower question than the one being asked, and answers it with confidence.
/// So the target and the status are now captured as written, and <see cref="Problems"/> names
/// anything the legends do not declare instead of the row vanishing.
/// </remarks>
internal static class FeatureMatrix
{
    internal static readonly Regex LinkPattern =
        new(@"\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>
    /// A feature identifier, in the one shape the whole repository uses: two to five characters of
    /// prefix and three digits. <c>UX-007</c> and <c>A11Y-001</c> are both real.
    /// </summary>
    private const string Identifier = @"[A-Z][A-Z0-9]{1,4}-[0-9]{3}";

    private static readonly Regex RowPattern = new(
        $@"(?m)^\|\s*(?<id>{Identifier})\s*\|(?<feature>[^|]*)\|\s*(?<target>[^|]+?)\s*\|\s*(?<status>[^|]+?)\s*\|(?<criterion>[^|]*)\|(?<evidence>.*)\|\s*$",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>Anything that looks like a feature row, wherever it sits. The second count.</summary>
    private static readonly Regex ScanPattern = new(
        $@"(?m)^\|\s*(?<id>{Identifier})\s*\|",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>A legend row: a single backticked value in the first cell of a two-column table.</summary>
    private static readonly Regex LegendPattern = new(
        @"(?m)^\|\s*`(?<value>[^`]+)`\s*\|",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>The statuses a commitment can carry without anything further being owed.</summary>
    public static readonly string[] SettledStatuses = ["VERIFIED", "OUT_OF_SCOPE"];

    private static readonly string MatrixText =
        File.ReadAllText(RepositoryLayout.PathFromRoot("docs/FEATURES.md"));

    public static IReadOnlyList<FeatureRow> Rows { get; } = Load();

    public static IReadOnlyList<FeatureRow> Mvp { get; } =
        [.. Rows.Where(row => row.Target == "MVP")];

    /// <summary>The releases the matrix declares, in the order it declares them.</summary>
    public static IReadOnlyList<string> DeclaredTargets { get; } = Legend("Versiones / Releases");

    /// <summary>The statuses the matrix declares.</summary>
    public static IReadOnlyList<string> DeclaredStatuses { get; } = Legend("Estados / Statuses");

    /// <summary>
    /// Everything about the matrix this parser could not account for: a row that looks like a
    /// feature and did not parse, a release or a status the legends do not declare.
    /// </summary>
    public static IReadOnlyList<string> Problems { get; } = FindProblems();

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

    private static IReadOnlyList<FeatureRow> Load() =>
    [
        .. RowPattern.Matches(MatrixText).Select(match => new FeatureRow(
            match.Groups["id"].Value,
            match.Groups["target"].Value,
            match.Groups["status"].Value,
            match.Groups["criterion"].Value.Trim(),
            match.Groups["evidence"].Value.Trim())),
    ];

    /// <summary>Reads one legend table, which runs from its heading to the next one.</summary>
    private static IReadOnlyList<string> Legend(string heading)
    {
        var start = MatrixText.IndexOf("## " + heading, StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        var next = MatrixText.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var body = next < 0 ? MatrixText[start..] : MatrixText[start..next];
        return [.. LegendPattern.Matches(body).Select(match => match.Groups["value"].Value)];
    }

    private static List<string> FindProblems()
    {
        var problems = new List<string>();

        if (DeclaredTargets.Count == 0 || DeclaredStatuses.Count == 0)
        {
            problems.Add("The matrix legends could not be read, so no row could be validated against them.");
            return problems;
        }

        var parsed = Rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in ScanPattern.Matches(MatrixText).Select(match => match.Groups["id"].Value))
        {
            if (!parsed.Contains(id))
            {
                problems.Add($"{id} looks like a feature row but did not parse.");
            }
        }

        foreach (var row in Rows)
        {
            if (!DeclaredTargets.Contains(row.Target, StringComparer.Ordinal))
            {
                problems.Add($"{row.Id} targets '{row.Target}', which the matrix does not declare as a release.");
            }

            if (!DeclaredStatuses.Contains(row.Status, StringComparer.Ordinal))
            {
                problems.Add($"{row.Id} has status '{row.Status}', which the matrix does not declare.");
            }
        }

        return problems;
    }
}
