// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

/// <summary>
/// The recovery pass that catches what a file watcher misses.
/// </summary>
/// <remarks>
/// <para>
/// <b>This used to run for nobody, and that is ENG-044's second half.</b> It asked for
/// <see cref="ScanPolicy.Continuous"/> on its own, and nothing in the application ever assigned that
/// flag — so for every root a person can create, this emitted the startup pass and broke out. Two
/// things went quietly with it: the recovery LIB-003 promises for USB and network roots, and the
/// retry that brings a dead live watcher back, which <c>RootWatchCoordinator</c> feeds from this
/// very schedule. Who gets swept is now <c>ScanWatchPolicy</c>'s decision, so there is one answer
/// and not two.
/// </para>
/// <para>
/// The interval is read at the top of every pass, so a change in Settings is honoured from the next
/// sweep. <b>A wait already under way is not interrupted</b>, and that is accepted rather than
/// overlooked: the interval only moves <i>when</i> a sweep happens, never <i>whether</i> one does,
/// and the «whether» is rebuilt by <c>RootWatchBackground</c> the moment the setting changes.
/// </para>
/// <para>
/// It trusts the interval it is handed, because <c>IScanWatchSettings</c> promises one already
/// within bounds. Clamping here instead would make every test of this class wait a whole minute.
/// </para>
/// </remarks>
public sealed class FallbackScanScheduler : IFallbackScanScheduler
{
    private readonly IClock _clock;
    private readonly IScanWatchSettings _settings;

    public FallbackScanScheduler(IClock clock, IScanWatchSettings settings)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

        if (!ScanWatchPolicy.ShouldSweepPeriodically(
                root.Kind,
                root.ScanPolicy,
                _settings.WatchLocalRoots))
        {
            yield break;
        }

        while (true)
        {
            await _clock.DelayAsync(_settings.SweepInterval, cancellationToken).ConfigureAwait(false);
            yield return ScanTrigger.Recovery;
        }
    }
}
