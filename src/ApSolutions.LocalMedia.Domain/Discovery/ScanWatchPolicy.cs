// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// Whether a root is followed live by a file watcher, rather than only swept.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of ENG-044. <see cref="ScanPolicy.Continuous"/> was the only thing that
/// switched the live watcher on, and <b>nothing in the tree ever assigned it</b>: every way of
/// adding a root produced <c>Startup | Manual</c> or <c>Manual</c>, and no screen offered the
/// choice. So the whole watching slice — the debounced watcher, its coordinator, its background
/// host — was built, registered, covered by tests and never reached in the assembled application,
/// while the scope record called continuous watching verified.
/// </para>
/// <para>
/// The fix is not to make <c>Continuous</c> the default behind everyone's back, which would follow
/// a network share nobody asked to be followed. It is to let the decision be made where a person
/// can make it — the one setting in Settings that already says «watch changes in local roots» — and
/// to leave the per-root flag as the stronger, explicit statement it was always meant to be.
/// </para>
/// <para>
/// <b>And the setting stops at <see cref="ScanPolicy.Startup"/>, which an existing test taught.</b>
/// <c>WatchCoordinatorTests.A_manual_root_is_not_watched_behind_its_owners_back</c> holds that a
/// root whose owner chose Manual is not followed, and that is not a formality:
/// <c>DeclareCourseFolder</c> adds a course folder as Manual alone on purpose, because the dialog's
/// own help promises the rest of the drive is left alone. A setting that reached every local root
/// would break a promise made in writing. So it reaches the roots that already asked to be kept up
/// to date — the ones carrying <c>Startup</c>, which every folder added the normal way gets — and
/// never the ones that said «only when I ask».
/// </para>
/// </remarks>
public static class ScanWatchPolicy
{
    /// <summary>How long the recovery sweep waits between passes when nobody has said otherwise.</summary>
    /// <remarks>
    /// <b>This number lived in two places saying different things.</b> The settings screen offered
    /// thirty minutes and <c>FallbackScanScheduler</c> ran every fifteen, and neither knew about the
    /// other — which nobody could notice, because the screen's value governed nothing at all
    /// (ENG-010). Fifteen wins: it is the one the code actually ran and the one the archived WP-2
    /// evidence backs in writing, so there is no behaviour on the other side to preserve. It lives
    /// here once and both ends point at it rather than copying the digits.
    /// </remarks>
    public static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromMinutes(15);

    /// <summary>The shortest sweep this application will run, however it is asked.</summary>
    /// <remarks>
    /// A sweep every zero minutes is a scan in a hot loop, and the settings file is plain text that
    /// somebody can open. The bounds are the ones the screen's spinner already paints, so a value
    /// typed there can never be out of range and one edited by hand is brought back into it.
    /// </remarks>
    public static readonly TimeSpan MinimumSweepInterval = TimeSpan.FromMinutes(1);

    /// <summary>The longest sweep this application will run, however it is asked.</summary>
    public static readonly TimeSpan MaximumSweepInterval = TimeSpan.FromMinutes(1440);

    /// <param name="kind">Where the root lives: a fixed disk, a removable drive, or a share.</param>
    /// <param name="policy">What that root itself declares.</param>
    /// <param name="watchLocalRoots">The setting a person can reach.</param>
    public static bool ShouldWatchLive(RootKind kind, ScanPolicy policy, bool watchLocalRoots) =>
        policy.HasFlag(ScanPolicy.Continuous)
        || (watchLocalRoots && kind == RootKind.Local && policy.HasFlag(ScanPolicy.Startup));

    /// <summary>
    /// Whether a root gets the periodic recovery sweep on top of whatever watcher it has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the other half of ENG-044, and it was still broken after the first half was fixed.
    /// <c>FallbackScanScheduler</c> kept asking for <see cref="ScanPolicy.Continuous"/> on its own,
    /// which nothing assigns, so <b>no real root ever reached its loop</b> — it emitted the startup
    /// pass and broke out. That silently cost two things: the recovery sweep <see cref="ScanPolicy"/>
    /// promises for USB and network roots, and the retry that brings a dead live watcher back, which
    /// <c>RootWatchCoordinator</c> feeds from this very schedule.
    /// </para>
    /// <para>
    /// <b>Every root followed live is swept</b>, because the sweep is the watcher's safety net: a
    /// file watcher reports events it can miss, and the recovery pass is what catches them.
    /// </para>
    /// <para>
    /// <b>And a removable or network root is swept even with the setting off</b>, which is the one
    /// asymmetry worth reading twice. The setting says «local roots» and means it: it never spoke
    /// for the drive that gets pulled out or the share across the network, so it cannot switch off
    /// a sweep it never switched on. There the sweep is the only net that exists, because the
    /// setting never gives either of them a live watcher — which is the half of LIB-003 that reads
    /// «recovery for USB/NAS».
    /// </para>
    /// <para>
    /// <b>The mirror case: a local root the setting left alone is not swept either.</b> The screen's
    /// own description sells the sweep as the watcher's backup — «if watching fails, it checks them
    /// again every so often» — so leaving it running after the box is unticked would keep files
    /// appearing every quarter of an hour for somebody who just asked for that to stop. The startup
    /// pass still happens; that one is the root's own decision and not the setting's.
    /// </para>
    /// <para>
    /// <b>And <see cref="ScanPolicy.Manual"/> alone is never swept, in any branch</b>, for the same
    /// written promise that shapes <see cref="ShouldWatchLive"/>: a recovery pass is a scan nobody
    /// asked for.
    /// </para>
    /// </remarks>
    /// <param name="kind">Where the root lives: a fixed disk, a removable drive, or a share.</param>
    /// <param name="policy">What that root itself declares.</param>
    /// <param name="watchLocalRoots">The setting a person can reach.</param>
    public static bool ShouldSweepPeriodically(RootKind kind, ScanPolicy policy, bool watchLocalRoots) =>
        ShouldWatchLive(kind, policy, watchLocalRoots)
        || (kind != RootKind.Local && policy.HasFlag(ScanPolicy.Startup));
}
