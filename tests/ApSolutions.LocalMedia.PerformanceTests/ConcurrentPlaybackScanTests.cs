// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

/// <summary>
/// Playing something while the library is being scanned. The scan is allowed to take as long as it
/// takes; what it is not allowed to do is interrupt what a person is watching.
/// <para>
/// The player is represented by a periodic loop on its own thread, because that is what a decoder's
/// position callback is: a thing that must be serviced on time. A gap in that loop longer than the
/// dropout budget is exactly what a viewer would see as a stutter.
/// </para>
/// </summary>
public sealed class ConcurrentPlaybackScanTests
{
    /// <summary>The specification's dropout budget: nothing attributable to the scan above this.</summary>
    private const double DropoutBudgetMilliseconds = 250;

    /// <summary>The specification's UI budget: no scan may hold the interface longer than this.</summary>
    private const double UiBlockBudgetMilliseconds = 50;

    [Fact]
    public async Task A_scan_of_ten_thousand_items_never_interrupts_the_beat_a_player_has_to_keep()
    {
        await using var fixture = await Catalog10kBuilder.CreateAsync(TestContext.Current.CancellationToken);
        var scan = fixture.CreateUnchangedScanCoordinator();
        using var playing = new CancellationTokenSource();
        var gaps = new List<double>();

        var player = Task.Factory.StartNew(
            () =>
            {
                var beat = Stopwatch.StartNew();
                var previous = beat.Elapsed;
                while (!playing.IsCancellationRequested)
                {
                    Thread.Sleep(20);
                    var now = beat.Elapsed;
                    gaps.Add((now - previous).TotalMilliseconds);
                    previous = now;
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        var summary = await scan.StartAsync(
            new StartScanCommand(fixture.Root.Id, ScanTrigger.Manual),
            TestContext.Current.CancellationToken);
        await playing.CancelAsync();
        await player;

        Assert.Equal(Catalog10kBuilder.ItemCount, summary.UnchangedCount);
        Assert.NotEmpty(gaps);
        var worst = gaps.Max();
        var samples = new PerformanceSampleSet(
            gaps,
            gaps.Order().ElementAt(gaps.Count / 2),
            gaps.Order().ElementAt(Math.Max(0, (int)Math.Ceiling(gaps.Count * 0.95) - 1)),
            worst);

        await PerformanceEvidence.WriteAsync(
            "playback-beat-during-scan",
            samples,
            DropoutBudgetMilliseconds,
            TestContext.Current.CancellationToken);
        Assert.True(
            worst < DropoutBudgetMilliseconds,
            $"The playback beat lost {worst:F1} ms while the library was scanned.");
        Assert.True(
            summary.MaxEventDispatchDuration.TotalMilliseconds < UiBlockBudgetMilliseconds,
            $"The scan held the interface for {summary.MaxEventDispatchDuration.TotalMilliseconds:F1} ms.");
    }
}
