using System.Diagnostics;
using System.Reflection;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

public sealed class UiThreadBudgetTests
{
    [Fact]
    public void UI_budget_owns_frame_and_scan_dispatch_measurement()
    {
        Assert.NotNull(Assembly.GetExecutingAssembly().GetType(
            "ApSolutions.LocalMedia.PerformanceTests.Fixtures.UiFrameBudgetProbe",
            throwOnError: false));
    }

    [AvaloniaFact]
    public async Task Virtualized_visible_page_frame_p95_is_under_sixteen_point_seven_milliseconds()
    {
        // The frame budget was approved on reference hardware; a shared CI runner rendering in
        // software breaches it spuriously. The budget keeps its gate in eng/run-performance.ps1.
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true",
            "Shared-runner timing is not the hardware the frame budget was approved on.");

        await using var fixture = await Catalog10kBuilder.CreateAsync(TestContext.Current.CancellationToken);
        var viewModel = new LibraryViewModel(fixture.Catalog);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window
        {
            Width = 1024,
            Height = 720,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().First();
        var frameIndex = 0;
        var samples = UiFrameBudgetProbe.Measure(() =>
        {
            scroller.Offset = new Vector(0, (frameIndex++ * 24) % 960);
            view.InvalidateVisual();
            window.InvalidateVisual();
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            GC.KeepAlive(frame);
        }, repetitions: 60);
        window.Close();

        await PerformanceEvidence.WriteAsync(
            "frame-p95",
            samples,
            budgetMilliseconds: 16.7,
            TestContext.Current.CancellationToken);
        Assert.True(
            samples.P95Milliseconds < 16.7,
            $"Visible-page frame p95 was {samples.P95Milliseconds:F3} ms.");
    }

    [Fact]
    public async Task Unchanged_scan_handoff_and_dispatch_stay_under_fifty_milliseconds()
    {
        await using var fixture = await Catalog10kBuilder.CreateAsync(TestContext.Current.CancellationToken);
        var scan = fixture.CreateUnchangedScanCoordinator();
        var handoff = Stopwatch.StartNew();
        var scanTask = scan.StartAsync(
            new StartScanCommand(fixture.Root.Id, ScanTrigger.Recovery),
            TestContext.Current.CancellationToken);
        handoff.Stop();
        var summary = await scanTask;
        var samples = new PerformanceSampleSet(
            [handoff.Elapsed.TotalMilliseconds, summary.MaxEventDispatchDuration.TotalMilliseconds],
            Math.Min(handoff.Elapsed.TotalMilliseconds, summary.MaxEventDispatchDuration.TotalMilliseconds),
            Math.Max(handoff.Elapsed.TotalMilliseconds, summary.MaxEventDispatchDuration.TotalMilliseconds),
            Math.Max(handoff.Elapsed.TotalMilliseconds, summary.MaxEventDispatchDuration.TotalMilliseconds));

        await PerformanceEvidence.WriteAsync(
            "scan-ui-block",
            samples,
            budgetMilliseconds: 50,
            TestContext.Current.CancellationToken);
        await PerformanceEvidence.WriteExactAsync(
            "unchanged-probes",
            summary.ProbeCount,
            expected: 0,
            unit: "count",
            TestContext.Current.CancellationToken);
        Assert.Equal(0, summary.ProbeCount);
        Assert.Equal(Catalog10kBuilder.ItemCount, summary.UnchangedCount);
        Assert.True(
            samples.P95Milliseconds < 50,
            $"Scan UI blocking max was {samples.P95Milliseconds:F3} ms.");
    }
}
