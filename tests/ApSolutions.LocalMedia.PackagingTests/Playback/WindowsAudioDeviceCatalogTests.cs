using System.Globalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Playback;

/// <summary>
/// Reads the real audio endpoints of the machine running the suite and records what layouts they
/// actually offer. A layout no endpoint accepts is written down as a hardware block, never as a pass.
/// </summary>
public sealed class WindowsAudioDeviceCatalogTests
{
    private const string NoEndpointReason =
        "This machine has no active audio endpoint, so the catalog cannot be exercised; "
        + "a hardware absence is recorded as a block, never as a pass.";

    [Fact]
    public async Task The_catalog_lists_the_active_endpoints_with_stable_identifiers()
    {
        var catalog = new WindowsAudioDeviceCatalog();

        var devices = await catalog.GetOutputsAsync(TestContext.Current.CancellationToken);

        Assert.SkipWhen(devices.Count == 0, NoEndpointReason);
        Assert.All(devices, device => Assert.False(string.IsNullOrWhiteSpace(device.Id)));
        Assert.All(devices, device => Assert.False(string.IsNullOrWhiteSpace(device.Name)));
        Assert.All(devices, device => Assert.True(device.IsAvailable));
        Assert.All(devices, device => Assert.Contains(AudioChannelLayout.Stereo, device.SupportedLayouts));
        Assert.Equal(devices.Select(device => device.Id).Distinct().Count(), devices.Count);
        Assert.True(devices.Count(device => device.IsDefault) <= 1);
    }

    [Fact]
    public async Task Two_queries_return_the_same_endpoints()
    {
        var catalog = new WindowsAudioDeviceCatalog();

        var first = await catalog.GetOutputsAsync(TestContext.Current.CancellationToken);
        var second = await catalog.GetOutputsAsync(TestContext.Current.CancellationToken);

        Assert.SkipWhen(first.Count == 0, NoEndpointReason);

        Assert.Equal(
            first.Select(device => device.Id).Order(StringComparer.Ordinal),
            second.Select(device => device.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_layouts_this_machine_can_actually_reach_are_recorded_for_the_matrix()
    {
        var catalog = new WindowsAudioDeviceCatalog();
        var devices = await catalog.GetOutputsAsync(TestContext.Current.CancellationToken);

        Assert.SkipWhen(devices.Count == 0, NoEndpointReason);

        var rows = new List<string> { "layout,endpointsSupporting,verdict" };
        foreach (var layout in AudioOutputPolicy.SelectableLayouts.Reverse())
        {
            var supporting = devices.Count(device => device.SupportedLayouts.Contains(layout));
            var verdict = supporting > 0 ? "verifiable" : "hardware block: no endpoint accepts it";
            rows.Add(string.Create(CultureInfo.InvariantCulture, $"{layout},{supporting},{verdict}"));
        }

        // Device names are recorded because they are models, not personal data; endpoint identifiers
        // are deliberately left out.
        rows.Add(string.Empty);
        rows.Add("endpointName,channels");
        rows.AddRange(devices.Select(device => string.Create(
            CultureInfo.InvariantCulture,
            $"{device.Name},{device.SupportedLayouts.Max(layout => (int)layout)}")));

        var report = Path.Combine(
            GetRepositoryRoot(),
            "artifacts",
            "test-results",
            "T23",
            "green",
            "audio-endpoints.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(report, rows, TestContext.Current.CancellationToken);

        Assert.Contains(devices, device => device.SupportedLayouts.Contains(AudioChannelLayout.Stereo));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
