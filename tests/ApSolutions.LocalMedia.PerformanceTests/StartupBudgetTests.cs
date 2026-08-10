// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using ApSolutions.LocalMedia.Presentation.Library;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

public sealed class StartupBudgetTests
{
    [Fact]
    public void Startup_budget_owns_the_deterministic_ten_thousand_item_fixture()
    {
        var assembly = Assembly.GetExecutingAssembly();

        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.PerformanceTests.Fixtures.Catalog10kBuilder",
            throwOnError: false));
        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.PerformanceTests.Fixtures.PerformanceSampleSet",
            throwOnError: false));
    }

    [Fact]
    public async Task Warm_catalog_reaches_a_useful_library_surface_under_three_seconds()
    {
        await using var fixture = await Catalog10kBuilder.CreateAsync(TestContext.Current.CancellationToken);
        var statistics = await fixture.ReadStatisticsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Catalog10kBuilder.ItemCount, statistics.MediaFiles);
        Assert.Equal(Catalog10kBuilder.EpisodeCount, statistics.Episodes);
        Assert.Equal(Catalog10kBuilder.MovieCount, statistics.Movies);
        Assert.Equal(Catalog10kBuilder.UnavailableCount, statistics.Unavailable);
        Assert.Equal(Catalog10kBuilder.DuplicateCount, statistics.Duplicates);

        var samples = await PerformanceMeasurement.MeasureAsync(async () =>
        {
            var viewModel = new LibraryViewModel(fixture.Catalog);
            await viewModel.LoadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(50, viewModel.Items.Count);
        });

        await PerformanceEvidence.WriteAsync(
            "useful-window",
            samples,
            budgetMilliseconds: 3_000,
            TestContext.Current.CancellationToken);
        Assert.True(
            samples.P95Milliseconds < 3_000,
            $"Useful window p95 was {samples.P95Milliseconds:F3} ms.");
    }
}
