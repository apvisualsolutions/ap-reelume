// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// Guards TST-001: the coverage gate exists, the verification gate runs it as a blocking step,
/// and the tool it merges reports with is declared in the tool manifest. A gate that can be
/// unplugged without a test noticing is a gate in name only.
/// </summary>
public sealed class CoverageGateTests
{
    [Fact]
    public void The_coverage_gate_script_exists_and_holds_the_agreed_thresholds()
    {
        var gatePath = RepositoryLayout.PathFromRoot("eng/check-coverage.ps1");
        Assert.True(File.Exists(gatePath), "eng/check-coverage.ps1 is missing.");

        var script = File.ReadAllText(gatePath);
        Assert.Contains("$MinimumLinePercent = 96.0", script, StringComparison.Ordinal);
        Assert.Contains("$MinimumBranchPercent = 96.0", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// The files the new-file rule cannot see. Newness is decided against the base ref, so a file
    /// that shipped long ago and gets worse is held by nobody — which is measured rather than
    /// feared: <c>PlayerVersionsViewModel</c> went from 60.61%/27.27% to 45.45%/14.29% during
    /// ARQ-004 and no gate said a word.
    /// </summary>
    /// <remarks>
    /// Each declared path is checked to still exist, so the list cannot rot into a set of names for
    /// files nobody has any more — the same rule the orphan list in
    /// <see cref="ServiceConsumptionTests"/> lives under.
    /// </remarks>
    [Fact]
    public void The_coverage_gate_watches_the_files_the_new_file_rule_cannot_see()
    {
        var script = File.ReadAllText(RepositoryLayout.PathFromRoot("eng/check-coverage.ps1"));

        var watched = Regex.Matches(script, @"File\s*=\s*'(?<path>src/[^']+\.cs)'", RegexOptions.None, TimeSpan.FromSeconds(1))
            .Select(match => match.Groups["path"].Value)
            .ToArray();

        Assert.True(
            watched.Length >= 3,
            $"The coverage gate watches {watched.Length} file(s); TST-001 named three, and the list "
            + "is only allowed to shrink by a file reaching the bar, never by being deleted.");
        foreach (var path in watched)
        {
            Assert.True(
                File.Exists(RepositoryLayout.PathFromRoot(path)),
                $"{path} is watched by the coverage gate and does not exist, so the entry holds nothing.");
        }
    }

    /// <summary>
    /// The floor is a ratchet or it is decoration: falling below it fails, and so does rising above
    /// it without the number being raised, because a debt that is quietly paid and never recorded
    /// can come back just as quietly.
    /// </summary>
    [Fact]
    public void A_watched_file_that_slips_or_that_improves_both_stop_the_gate()
    {
        var script = File.ReadAllText(RepositoryLayout.PathFromRoot("eng/check-coverage.ps1"));

        Assert.Contains("floor it is held to", script, StringComparison.Ordinal);
        Assert.Contains("raise its floor", script, StringComparison.Ordinal);
        Assert.Contains("$watchFailures.Count -gt 0", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_verification_gate_runs_the_coverage_gate_and_blocks_on_it()
    {
        var verifyPath = RepositoryLayout.PathFromRoot("eng/verify.ps1");
        var lines = File.ReadAllLines(verifyPath);

        var invocation = Array.FindIndex(
            lines,
            line => line.Contains("check-coverage.ps1", StringComparison.Ordinal));
        Assert.True(invocation >= 0, "eng/verify.ps1 no longer runs the coverage gate.");
        Assert.Contains(
            "Coverage gate failed",
            string.Join('\n', lines[invocation..Math.Min(invocation + 3, lines.Length)]),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_merging_tool_is_declared_in_the_manifest_the_gate_restores()
    {
        var manifestPath = RepositoryLayout.PathFromRoot(".config/dotnet-tools.json");
        Assert.True(File.Exists(manifestPath), ".config/dotnet-tools.json is missing.");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.True(
            manifest.RootElement.GetProperty("tools").TryGetProperty(
                "dotnet-reportgenerator-globaltool",
                out _),
            "dotnet-reportgenerator-globaltool is not declared in the tool manifest.");
    }
}
