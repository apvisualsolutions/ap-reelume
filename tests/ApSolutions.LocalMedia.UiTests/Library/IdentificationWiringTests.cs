using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// Identification is what turns a scanned file into something the review inbox can show. The deep
/// audit found the whole chain registered and never invoked (LIB-006/007): the scan catalogued
/// files, <c>IdentifyMediaFile</c> waited for a caller that did not exist, and the inbox stayed
/// empty forever — a screen anybody could open with nothing that could ever arrive in it.
/// </summary>
public sealed class IdentificationWiringTests
{
    [Fact]
    public void The_scan_hands_what_it_found_to_identification()
    {
        var composition = CompositionSource();

        Assert.Contains(
            "GetRequiredService<IdentifyScannedFiles>",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Identification_rides_inside_the_scan_coordinator_the_application_shares()
    {
        var composition = CompositionSource();

        // The hand-off lives in the IScanCoordinator the whole application resolves, wrapped
        // around the real coordinator: no caller can scan without feeding the inbox.
        Assert.Contains("new IdentifyingScanCoordinator(", composition, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<ScanCoordinator>", composition, StringComparison.Ordinal);
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
