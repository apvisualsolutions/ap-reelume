using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// What ARM64 parity claims, and the rule that keeps the claim from outliving the evidence.
/// </summary>
/// <remarks>
/// None of this can be run on an x64 machine, and none of it may be simulated: an emulated result
/// would answer a question nobody asked, because the thing being verified is precisely that native
/// ARM64 code decodes, renders, and plays. So the matrix is a report, produced where it can be
/// produced, and this suite is the part that refuses to accept it — a phase is either a real result
/// from an ARM64 machine or a block that says why, and the block becomes illegal the moment the
/// suite itself runs on ARM64.
/// </remarks>
[Trait("Category", "RealMedia")]
public sealed partial class Arm64PlaybackTests
{
    /// <summary>Everything T42 requires of ARM64 hardware, named so a missing one is visible.</summary>
    private static readonly string[] RequiredPhases =
    [
        "native-execution",
        "codec-matrix",
        "hdr-acceleration",
        "audio-output",
        "package-lifecycle",
        "cross-architecture-data",
    ];

    public static TheoryData<string> Phases() => [.. RequiredPhases];

    [Fact]
    public void The_arm64_matrix_exists_and_covers_every_phase_and_invents_none()
    {
        var reported = PhaseElements()
            .Select(phase => phase.GetProperty("id").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(RequiredPhases.Order(StringComparer.Ordinal).ToArray(), reported);
    }

    /// <summary>
    /// The gate. On an ARM64 machine each phase is a real result; anywhere else it is blocked and has
    /// to say why, because "not yet" and "nobody looked" are otherwise the same entry.
    /// </summary>
    [Theory]
    [MemberData(nameof(Phases))]
    public void A_phase_that_needs_arm64_hardware_is_blocked_here_and_required_where_it_could_run(string id)
    {
        var phase = Phase(id);

        Assert.Null(RejectPhase(
            id,
            phase.GetProperty("outcome").GetString(),
            phase.GetProperty("detail").GetString(),
            phase.GetProperty("reason").GetString(),
            HostIsArm64()));
    }

    /// <summary>
    /// The rule above, exercised against reports this machine does not produce.
    /// </summary>
    /// <remarks>
    /// Every real run here reports the same thing — an x64 host with six blocked phases — so the rule
    /// is only ever asked one of the questions it exists to answer. These are the others, and the
    /// third row is the one that matters: a report claiming ARM64 hardware, with every phase passed,
    /// read on a machine that is not ARM64. That is exactly what carrying a stale or hand-edited
    /// matrix into the repository would look like, and it has to be refused.
    /// </remarks>
    [Theory]
    // Blocked with a reason, on a machine that cannot run it: the only shape this machine may file.
    [InlineData("Blocked", "", "No ARM64 machine.", false, false)]
    // Blocked without a reason is indistinguishable from nobody having looked.
    [InlineData("Blocked", "", "", false, true)]
    // Passed while the machine could not have run it.
    [InlineData("Passed", "Everything worked.", "", false, true)]
    // Passed with nothing observed, on hardware that could have observed something.
    [InlineData("Passed", "", "", true, true)]
    // Passed with detail, on ARM64: the shape that closes the phase.
    [InlineData("Passed", "Decoded 12 samples natively.", "", true, false)]
    // Still blocked on hardware that could have answered it.
    [InlineData("Blocked", "", "No ARM64 machine.", true, true)]
    // A failure is a failure, never a block, wherever it is read.
    [InlineData("Failed", "The host did not start.", "", true, true)]
    [InlineData("Failed", "The host did not start.", "", false, true)]
    public void The_rule_refuses_every_shape_a_matrix_must_not_have(
        string outcome, string detail, string reason, bool hostIsArm64, bool rejected)
    {
        var rejection = RejectPhase("example", outcome, detail, reason, hostIsArm64);

        Assert.Equal(rejected, rejection is not null);
    }

    /// <summary>
    /// Why a phase may not be filed as it stands, or <c>null</c> when it may. Kept apart from the
    /// report so it can be asked about cases this hardware cannot produce.
    /// </summary>
    private static string? RejectPhase(
        string id, string? outcome, string? detail, string? reason, bool hostIsArm64)
    {
        if (hostIsArm64)
        {
            if (outcome != "Passed")
            {
                return $"Phase {id} is {outcome} on an ARM64 machine, which could have answered it.";
            }

            return string.IsNullOrWhiteSpace(detail)
                ? $"Phase {id} passed on ARM64 without saying what it observed."
                : null;
        }

        if (outcome != "Blocked")
        {
            return $"Phase {id} is {outcome} on a machine that cannot run it.";
        }

        return string.IsNullOrWhiteSpace(reason)
            ? $"Phase {id} is blocked without a reason, which is indistinguishable from being skipped."
            : null;
    }

    /// <summary>
    /// The declaration cannot survive the hardware. If this suite is running on ARM64 then the report
    /// was produced somewhere that could have run the matrix, and saying otherwise is the one way a
    /// block turns into a permanent excuse.
    /// </summary>
    [Fact]
    public void The_matrix_cannot_claim_a_missing_machine_while_running_on_one()
    {
        var environment = Matrix().GetProperty("environment");

        Assert.Equal(
            HostIsArm64(),
            environment.GetProperty("arm64Host").GetBoolean());
        Assert.Equal(
            RuntimeInformation.OSArchitecture.ToString(),
            environment.GetProperty("hostArchitecture").GetString());
    }

    /// <summary>
    /// The report describes the release it is filed with. A matrix carried over from an earlier
    /// version would be evidence about a build nobody is shipping.
    /// </summary>
    [Fact]
    public void The_matrix_describes_the_version_being_verified()
    {
        var declared = DeclaredVersion();

        Assert.Equal(declared, Matrix().GetProperty("version").GetString());
        Assert.Equal("win-arm64", Matrix().GetProperty("runtime").GetString());
    }

    /// <summary>
    /// The rule that keeps T42 from closing itself. <c>PRD-003</c> is the commitment ARM64 hardware
    /// settles, and it cannot be verified while any phase of the matrix is still blocked.
    /// </summary>
    [Fact]
    public void The_arm64_commitment_is_not_verified_while_any_phase_is_blocked()
    {
        var blocked = PhaseElements()
            .Where(phase => phase.GetProperty("outcome").GetString() != "Passed")
            .Select(phase => phase.GetProperty("id").GetString()!)
            .ToArray();

        if (blocked.Length == 0)
        {
            return;
        }

        var features = File.ReadAllText(Path.Combine(MediaToolchain.RepositoryRoot, "docs", "FEATURES.md"));
        var row = Arm64FeatureRow().Match(features);

        Assert.True(row.Success, "docs/FEATURES.md has no PRD-003 row.");
        Assert.False(
            row.Groups["status"].Value == "VERIFIED",
            $"PRD-003 is VERIFIED while these phases are still blocked: {string.Join(", ", blocked)}.");
    }

    private static bool HostIsArm64() => RuntimeInformation.OSArchitecture == Architecture.Arm64;

    private static string MatrixPath() =>
        Path.Combine(MediaToolchain.RepositoryRoot, "artifacts", "package-arm64", "arm64-matrix.json");

    private static JsonElement Matrix()
    {
        var path = MatrixPath();
        Assert.True(
            File.Exists(path),
            $"{path} does not exist, so nothing is known about ARM64. Run `pwsh ./eng/package-arm64.ps1`.");

        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    private static JsonElement.ArrayEnumerator PhaseElements() => Matrix().GetProperty("phases").EnumerateArray();

    private static JsonElement Phase(string id) =>
        PhaseElements().FirstOrDefault(phase => phase.GetProperty("id").GetString() == id) is { ValueKind: JsonValueKind.Object } found
            ? found
            : throw new InvalidOperationException($"The ARM64 matrix has no phase '{id}'.");

    private static string DeclaredVersion()
    {
        var properties = System.Xml.Linq.XDocument.Load(
            Path.Combine(MediaToolchain.RepositoryRoot, "Directory.Build.props"));
        return properties
            .Descendants()
            .First(element => element.Name.LocalName == "Version")
            .Value
            .Trim();
    }

    [GeneratedRegex(@"(?m)^\|\s*PRD-003\s*\|[^|]*\|[^|]*\|\s*(?<status>[A-Z_]+)\s*\|")]
    private static partial Regex Arm64FeatureRow();
}
