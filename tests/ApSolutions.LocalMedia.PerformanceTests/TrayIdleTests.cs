using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using ApSolutions.LocalMedia.Windows.Tray;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

/// <summary>
/// What the application costs while nobody is using it.
/// <para>
/// C6 recorded handles climbing about seven a minute with the tray enabled and left alone, and could
/// not attribute it. This measures the tray on its own, in a child process, so the reading is the
/// tray's and not the whole application's — and it also checks the least interesting and most
/// important thing: that the process is still running at the end.
/// </para>
/// </summary>
public sealed class TrayIdleTests
{
    private const string PhaseVariable = "AP_LOCALMEDIA_TRAY_PHASE";
    private const string ReportVariable = "AP_LOCALMEDIA_TRAY_REPORT";
    private const int IdleSeconds = 60;

    /// <summary>
    /// Generous next to the seven per minute C6 saw, and far below what a real leak would produce. A
    /// tray that is doing nothing should not be allocating anything at all.
    /// </summary>
    private const double AllowedHandlesPerMinute = 30;

    private static readonly string[] ProfilerVariables =
    [
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "CORECLR_PROFILER_PATH",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "COR_PROFILER_PATH",
    ];

    [Fact]
    public async Task An_enabled_tray_left_alone_neither_leaks_handles_nor_takes_the_process_with_it()
    {
        var measurement = await MeasureInChildAsync("visible");

        Assert.True(measurement.Survived, "The process did not survive the idle window.");
        Assert.InRange(measurement.HandlesPerMinute, double.MinValue, AllowedHandlesPerMinute);
        await PerformanceEvidence.WriteAsync(
            "tray-idle-handles",
            new PerformanceSampleSet(
                [measurement.HandlesPerMinute],
                measurement.HandlesPerMinute,
                measurement.HandlesPerMinute,
                measurement.HandlesPerMinute),
            AllowedHandlesPerMinute,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_hidden_tray_costs_the_same_as_no_tray_at_all()
    {
        var measurement = await MeasureInChildAsync("hidden");

        Assert.True(measurement.Survived, "The process did not survive the idle window.");
        Assert.InRange(measurement.HandlesPerMinute, double.MinValue, AllowedHandlesPerMinute);
    }

    /// <summary>Runs one idle window in this process. Only the child started above enters here.</summary>
    [AvaloniaFact]
    public async Task Tray_idle_child_fixture()
    {
        var phase = Environment.GetEnvironmentVariable(PhaseVariable);
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable);
        if (string.IsNullOrWhiteSpace(phase) || string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        using var tray = new WindowsTrayService("AP Reelume", "Open", "Exit");
        if (string.Equals(phase, "visible", StringComparison.Ordinal))
        {
            tray.Show();
        }

        var process = Process.GetCurrentProcess();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        process.Refresh();
        var first = process.HandleCount;
        var started = Stopwatch.StartNew();

        while (started.Elapsed < TimeSpan.FromSeconds(IdleSeconds))
        {
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        process.Refresh();
        var last = process.HandleCount;
        var perMinute = (last - first) / started.Elapsed.TotalMinutes;
        await File.WriteAllTextAsync(
            reportPath,
            string.Create(CultureInfo.InvariantCulture, $"{first};{last};{perMinute};{tray.IsVisible}"),
            TestContext.Current.CancellationToken);
    }

    private static async Task<IdleMeasurement> MeasureInChildAsync(string phase)
    {
        var reportPath = Path.Combine(
            Path.GetTempPath(),
            "APSolutions.LocalMedia.Tests",
            $"tray-{phase}-{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(Path.Combine(
            RepositoryRoot(),
            "tests",
            "ApSolutions.LocalMedia.PerformanceTests",
            "ApSolutions.LocalMedia.PerformanceTests.csproj"));
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add("FullyQualifiedName~Tray_idle_child_fixture");
        startInfo.Environment[PhaseVariable] = phase;
        startInfo.Environment[ReportVariable] = reportPath;
        foreach (var profiling in ProfilerVariables)
        {
            startInfo.Environment[profiling] = string.Empty;
        }

        using var child = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the tray probe.");
        var output = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(output, error);

        if (!File.Exists(reportPath))
        {
            Assert.Fail($"The tray probe wrote nothing. stdout={await output}; stderr={await error}");
        }

        var values = (await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken))
            .Split(';');
        File.Delete(reportPath);
        return new IdleMeasurement(
            int.Parse(values[0], CultureInfo.InvariantCulture),
            int.Parse(values[1], CultureInfo.InvariantCulture),
            double.Parse(values[2], CultureInfo.InvariantCulture),
            Survived: child.ExitCode == 0);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private sealed record IdleMeasurement(
        int FirstHandles,
        int LastHandles,
        double HandlesPerMinute,
        bool Survived);
}
