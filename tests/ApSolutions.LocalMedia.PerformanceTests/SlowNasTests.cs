using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

/// <summary>
/// A network share that answers slowly. The scan is allowed to take as long as the share makes it
/// take; what it may not do is hold the interface, and it may not have more than one enumeration in
/// flight against the same root — a slow share punished by parallel requests only gets slower.
/// </summary>
public sealed class SlowNasTests
{
    private const double UiBlockBudgetMilliseconds = 50;
    private static readonly TimeSpan PerFileDelay = TimeSpan.FromMilliseconds(20);

    [Fact]
    public async Task A_slow_share_slows_the_scan_and_nothing_else()
    {
        await using var fixture = await Catalog10kBuilder.CreateAsync(TestContext.Current.CancellationToken);
        var enumerator = new SlowEnumerator(fixture.ScanItems.Take(200).ToArray(), PerFileDelay);
        var coordinator = fixture.CreateScanCoordinator(enumerator);

        var elapsed = Stopwatch.StartNew();
        var summary = await coordinator.StartAsync(
            new StartScanCommand(fixture.Root.Id, ScanTrigger.Manual),
            TestContext.Current.CancellationToken);
        elapsed.Stop();

        Assert.Equal(200, summary.EnumeratedCount);
        Assert.True(
            elapsed.Elapsed > PerFileDelay,
            "The slow share was not actually slow, so the measurement means nothing.");
        Assert.True(
            summary.MaxEventDispatchDuration.TotalMilliseconds < UiBlockBudgetMilliseconds,
            $"A slow share held the interface for {summary.MaxEventDispatchDuration.TotalMilliseconds:F1} ms.");
        Assert.Equal(1, enumerator.MaximumConcurrentEnumerations);

        await PerformanceEvidence.WriteAsync(
            "slow-nas-ui-block",
            new PerformanceSampleSet(
                [summary.MaxEventDispatchDuration.TotalMilliseconds],
                summary.MaxEventDispatchDuration.TotalMilliseconds,
                summary.MaxEventDispatchDuration.TotalMilliseconds,
                summary.MaxEventDispatchDuration.TotalMilliseconds),
            UiBlockBudgetMilliseconds,
            TestContext.Current.CancellationToken);
    }

    /// <summary>An enumerator that takes its time and counts how many callers are inside it at once.</summary>
    private sealed class SlowEnumerator(IReadOnlyList<EnumeratedFile> files, TimeSpan delay)
        : IMediaFileEnumerator
    {
        private int _concurrent;
        private int _maximumConcurrent;

        public int MaximumConcurrentEnumerations => Volatile.Read(ref _maximumConcurrent);

        public async IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
            LibraryRoot root,
            string? afterPath,
            int batchSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            var current = Interlocked.Increment(ref _concurrent);
            InterlockedMaximum(ref _maximumConcurrent, current);
            try
            {
                var batch = new List<EnumeratedFile>(batchSize);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (afterPath is not null
                        && string.Compare(file.Path, afterPath, StringComparison.OrdinalIgnoreCase) <= 0)
                    {
                        continue;
                    }

                    // The delay is per file, which is what a share that answers slowly actually costs.
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    batch.Add(file);
                    if (batch.Count == batchSize)
                    {
                        yield return batch;
                        batch = new List<EnumeratedFile>(batchSize);
                    }
                }

                if (batch.Count > 0)
                {
                    yield return batch;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }

        private static void InterlockedMaximum(ref int target, int value)
        {
            var observed = Volatile.Read(ref target);
            while (value > observed)
            {
                var exchanged = Interlocked.CompareExchange(ref target, value, observed);
                if (exchanged == observed)
                {
                    return;
                }

                observed = exchanged;
            }
        }
    }
}
