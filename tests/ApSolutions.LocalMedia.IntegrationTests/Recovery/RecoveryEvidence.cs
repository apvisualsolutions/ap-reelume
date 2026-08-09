using System.Text.Json;

namespace ApSolutions.LocalMedia.IntegrationTests.Recovery;

/// <summary>
/// What a failure did, said in one of four words. There is deliberately no word for "it worked": a
/// recovery case that reports success is a case that did not fail, and that is a defect in the case.
/// </summary>
public enum RecoveryOutcome
{
    /// <summary>The failure was absorbed and the work carried on unaffected.</summary>
    Continued,

    /// <summary>The work carried on with less than it had, and said so.</summary>
    Degraded,

    /// <summary>The work stopped and everything needed to resume it survived.</summary>
    Recoverable,

    /// <summary>The work refused to proceed, and refused without destroying anything.</summary>
    AbortedSafely,
}

/// <summary>
/// Records one row of the recovery matrix. Rows are written as files so the harness can assemble the
/// matrix from a real run rather than from a table somebody typed.
/// </summary>
public static class RecoveryEvidence
{
    public const string ResultsVariable = "APSOLUTIONS_LOCALMEDIA_RECOVERY_RESULTS";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static async Task RecordAsync(
        string caseId,
        string failure,
        RecoveryOutcome outcome,
        string detail,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        var directory = Environment.GetEnvironmentVariable(ResultsVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var row = new
        {
            caseId,
            failure,
            outcome = outcome.ToString(),
            detail,
        };
        await File.WriteAllTextAsync(
            Path.Combine(directory, $"{caseId}.json"),
            JsonSerializer.Serialize(row, SerializerOptions),
            cancellationToken).ConfigureAwait(false);
    }
}
