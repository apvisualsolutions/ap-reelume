// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.TestSupport;

/// <summary>
/// The scan-watch settings, in memory, for every suite that needs them without a file behind them.
/// </summary>
/// <remarks>
/// <para>
/// It lives here because the same six lines had been copied into five test classes by the time
/// ENG-044 widened the port, and every one of them had to be found and edited again. Five copies of
/// a stub are five places a future field can be forgotten in.
/// </para>
/// <para>
/// <b>Most callers pass <c>watchLocalRoots: false</c> on purpose</b>, and it is worth knowing why
/// before changing one: those roots carry <see cref="ScanPolicy.Continuous"/>, so with the setting
/// off they keep measuring what they measured before ENG-044 existed — that the per-root flag, and
/// not the setting, is what starts the watcher.
/// </para>
/// </remarks>
public sealed class InMemoryScanWatchSettings(
    bool watchLocalRoots = true,
    TimeSpan? sweepInterval = null) : IScanWatchSettings
{
    public bool WatchLocalRoots { get; private set; } = watchLocalRoots;

    public TimeSpan SweepInterval { get; private set; } =
        sweepInterval ?? ScanWatchPolicy.DefaultSweepInterval;

    /// <summary>How many times somebody reached the store, which is how «wrote nothing» is seen.</summary>
    public int Writes { get; private set; }

    /// <summary>Whether anybody is still subscribed, which is how a leaked subscription is seen.</summary>
    public bool HasListeners => Changed is not null;

    public event EventHandler? Changed;

    public void SetWatchLocalRoots(bool enabled)
    {
        WatchLocalRoots = enabled;
        Writes++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetSweepInterval(TimeSpan interval)
    {
        SweepInterval = interval;
        Writes++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
