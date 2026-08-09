using System.Reflection;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

public sealed class SearchBudgetTests
{
    [Fact]
    public void Search_budget_owns_warmup_median_and_p95_measurement()
    {
        Assert.NotNull(Assembly.GetExecutingAssembly().GetType(
            "ApSolutions.LocalMedia.PerformanceTests.Fixtures.PerformanceMeasurement",
            throwOnError: false));
    }

    [Fact]
    public async Task First_search_page_p95_is_under_one_hundred_fifty_milliseconds()
    {
        await using var fixture = await Catalog10kBuilder.CreateAsync(TestContext.Current.CancellationToken);
        var samples = await MeasureSearchAsync(fixture);

        await PerformanceEvidence.WriteAsync(
            "first-search-page",
            samples,
            budgetMilliseconds: 150,
            TestContext.Current.CancellationToken);
        Assert.True(
            samples.P95Milliseconds < 150,
            $"First search page p95 was {samples.P95Milliseconds:F3} ms.");
    }

    [Fact]
    public async Task Search_stays_in_budget_while_an_unchanged_scan_runs()
    {
        await using var fixture = await Catalog10kBuilder.CreateAsync(TestContext.Current.CancellationToken);
        var scan = fixture.CreateUnchangedScanCoordinator();
        var scanTask = scan.StartAsync(
            new StartScanCommand(fixture.Root.Id, ScanTrigger.Recovery),
            TestContext.Current.CancellationToken);
        var samples = await MeasureSearchAsync(fixture);
        var summary = await scanTask;

        await PerformanceEvidence.WriteAsync(
            "concurrent-search",
            samples,
            budgetMilliseconds: 150,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, summary.ProbeCount);
        Assert.Equal(Catalog10kBuilder.ItemCount, summary.UnchangedCount);
        Assert.True(
            samples.P95Milliseconds < 150,
            $"Concurrent search p95 was {samples.P95Milliseconds:F3} ms.");
    }

    private static Task<PerformanceSampleSet> MeasureSearchAsync(Catalog10kBuilder fixture) =>
        PerformanceMeasurement.MeasureAsync(async () =>
        {
            var page = await fixture.Catalog.QueryAsync(
                new CatalogQuery(Search: "amelie", PageSize: 50),
                TestContext.Current.CancellationToken);
            Assert.Equal(50, page.Items.Count);
        });
}
