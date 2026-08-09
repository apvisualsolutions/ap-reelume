using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

public sealed class FallbackScanScheduler : IFallbackScanScheduler
{
    private readonly IClock _clock;
    private readonly TimeSpan? _recoveryInterval;

    /// <summary>
    /// The interval the assembled application recovers lost events at. Watcher events cover the
    /// common case within a second; this pass exists for what a watcher can silently miss on USB
    /// and network roots, so it is deliberately unhurried.
    /// </summary>
    public static readonly TimeSpan DefaultRecoveryInterval = TimeSpan.FromMinutes(15);

    public FallbackScanScheduler(IClock clock, TimeSpan? recoveryInterval = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (recoveryInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryInterval));
        }

        _recoveryInterval = recoveryInterval;
    }

    public async IAsyncEnumerable<ScanTrigger> ScheduleAsync(
        LibraryRoot root,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        cancellationToken.ThrowIfCancellationRequested();
        if (root.ScanPolicy.HasFlag(ScanPolicy.Startup))
        {
            yield return ScanTrigger.Startup;
        }

        if (_recoveryInterval is not { } interval ||
            !root.ScanPolicy.HasFlag(ScanPolicy.Continuous))
        {
            yield break;
        }

        while (true)
        {
            await _clock.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
            yield return ScanTrigger.Recovery;
        }
    }
}
