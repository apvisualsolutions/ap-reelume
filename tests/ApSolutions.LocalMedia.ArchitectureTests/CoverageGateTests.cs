using System.Text.Json;

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
