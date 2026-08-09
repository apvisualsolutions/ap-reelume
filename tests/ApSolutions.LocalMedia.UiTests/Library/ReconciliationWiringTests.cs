using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// Moved-file reconciliation existed as parts and never as a behaviour (LIB-002/003): the use
/// case, the policy, and the manual flow all had tests, and no scan ever invoked them — a moved
/// file was catalogued as a stranger and its progress orphaned with the old row.
/// </summary>
public sealed class ReconciliationWiringTests
{
    [Fact]
    public void Every_scan_hands_what_it_found_to_reconciliation_before_identification()
    {
        var composition = CompositionSource();

        Assert.Contains(
            "GetRequiredService<ReconcileScannedFiles>",
            composition,
            StringComparison.Ordinal);

        // The order is part of the design: a moved file becomes its old entity again before
        // identification spends any effort on it as a stranger.
        Assert.True(
            composition.IndexOf("GetRequiredService<ReconcileScannedFiles>", StringComparison.Ordinal)
            < composition.IndexOf("GetRequiredService<IdentifyScannedFiles>", StringComparison.Ordinal),
            "Reconciliation has to run before identification in the shared pipeline.");
    }

    [Fact]
    public void The_inbox_receives_the_reassignment_flow_and_its_queue()
    {
        var composition = CompositionSource();

        Assert.Contains("PendingReassignments", composition, StringComparison.Ordinal);
        Assert.Contains("ManualReassignmentViewModel", composition, StringComparison.Ordinal);
    }

    private static string CompositionSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs");
        Assert.True(File.Exists(path), "CompositionRoot.cs was not found where the host keeps it.");
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
