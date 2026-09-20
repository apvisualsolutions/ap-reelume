// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

/// <summary>
/// Whether a root is followed live, decided from its kind, its own policy and the one setting a
/// person can reach. This is ENG-044: the live watcher was built whole and never switched on,
/// because the only thing that turned it on was a <see cref="ScanPolicy.Continuous"/> flag that
/// nothing in the tree ever assigned and no screen ever offered.
/// </summary>
public sealed class ScanWatchPolicyTests
{
    [Fact]
    public void A_local_root_is_followed_live_when_the_setting_is_on()
    {
        Assert.True(ScanWatchPolicy.ShouldWatchLive(
            RootKind.Local,
            ScanPolicy.Startup | ScanPolicy.Manual,
            watchLocalRoots: true));
    }

    [Fact]
    public void A_local_root_is_left_alone_when_the_setting_is_off()
    {
        Assert.False(ScanWatchPolicy.ShouldWatchLive(
            RootKind.Local,
            ScanPolicy.Startup | ScanPolicy.Manual,
            watchLocalRoots: false));
    }

    /// <summary>
    /// The setting says «local roots» and means it. A drive that can be pulled out and a share
    /// across the network are what the fallback sweep exists for: a file watcher over either one
    /// reports events it cannot be trusted on, and the recovery pass catches what it misses.
    /// </summary>
    [Theory]
    [InlineData(RootKind.Usb)]
    [InlineData(RootKind.Unc)]
    public void A_removable_or_network_root_is_not_followed_live_by_the_setting(RootKind kind)
    {
        Assert.False(ScanWatchPolicy.ShouldWatchLive(
            kind,
            ScanPolicy.Startup | ScanPolicy.Manual,
            watchLocalRoots: true));
    }

    /// <summary>
    /// <b>The case that corrected this design, and it came from a test that already existed.</b>
    /// <c>WatchCoordinatorTests.A_manual_root_is_not_watched_behind_its_owners_back</c> holds that a
    /// root whose owner chose Manual gets no watcher and no scan they did not ask for — and it is
    /// not abstract: <c>DeclareCourseFolder</c> adds a course folder as Manual alone <b>on
    /// purpose</b>, because the dialog's own help promises the rest of the drive is left alone.
    /// A setting that switched watching on for every local root would break a promise made in
    /// writing to the person reading that dialog.
    /// <para>
    /// So the setting reaches the roots that already asked to be kept up to date — the ones
    /// carrying <see cref="ScanPolicy.Startup"/>, which is what every folder added the normal way
    /// gets — and never the ones that said «only when I ask».
    /// </para>
    /// </summary>
    [Fact]
    public void A_root_that_is_scanned_only_on_request_is_never_followed_live()
    {
        Assert.False(ScanWatchPolicy.ShouldWatchLive(
            RootKind.Local,
            ScanPolicy.Manual,
            watchLocalRoots: true));
    }

    /// <summary>
    /// A root that asked for continuous scanning keeps it whatever the setting says, because that
    /// flag is a decision written against that root and the setting is a default for the rest.
    /// </summary>
    [Theory]
    [InlineData(RootKind.Local)]
    [InlineData(RootKind.Usb)]
    [InlineData(RootKind.Unc)]
    public void A_root_that_declares_continuous_is_followed_live_whatever_the_setting_says(RootKind kind)
    {
        Assert.True(ScanWatchPolicy.ShouldWatchLive(
            kind,
            ScanPolicy.Continuous,
            watchLocalRoots: false));
    }

    /// <summary>
    /// The sweep is the watcher's safety net, so every root that is followed live is also swept:
    /// a watcher reports events it can miss, and the recovery pass is what catches them.
    /// </summary>
    [Fact]
    public void A_local_root_is_swept_while_it_is_followed_live()
    {
        Assert.True(ScanWatchPolicy.ShouldSweepPeriodically(
            RootKind.Local,
            ScanPolicy.Startup | ScanPolicy.Manual,
            watchLocalRoots: true));
    }

    /// <summary>
    /// <b>The case that tells this rule apart from the one above it.</b> The setting says «local
    /// roots» and never spoke for the drive that gets pulled out or the share across the network,
    /// so it cannot switch their sweep off — and off is what they would be, because the setting
    /// never gives either of them a live watcher in the first place. The sweep is the only net
    /// they have, which is the half of LIB-003 that says «recovery for USB/NAS».
    /// </summary>
    [Theory]
    [InlineData(RootKind.Usb)]
    [InlineData(RootKind.Unc)]
    public void A_removable_or_network_root_is_swept_whatever_the_setting_says(RootKind kind)
    {
        Assert.True(ScanWatchPolicy.ShouldSweepPeriodically(
            kind,
            ScanPolicy.Startup | ScanPolicy.Manual,
            watchLocalRoots: false));
    }

    /// <summary>
    /// The mirror of the case above, and the one that keeps the setting honest. The screen sells the
    /// sweep as the watcher's backup, so leaving it running after the box is unticked would keep
    /// files appearing every quarter of an hour for somebody who just asked for that to stop.
    /// </summary>
    [Fact]
    public void A_local_root_the_setting_left_alone_is_not_swept_either()
    {
        Assert.False(ScanWatchPolicy.ShouldSweepPeriodically(
            RootKind.Local,
            ScanPolicy.Startup | ScanPolicy.Manual,
            watchLocalRoots: false));
    }

    /// <summary>
    /// A recovery pass is a scan nobody asked for, so the promise <c>DeclareCourseFolder</c> makes in
    /// writing binds this rule exactly as it binds <see cref="ScanWatchPolicy.ShouldWatchLive"/> —
    /// and on every kind of root, not only the local one.
    /// </summary>
    [Theory]
    [InlineData(RootKind.Local)]
    [InlineData(RootKind.Usb)]
    [InlineData(RootKind.Unc)]
    public void A_root_that_is_scanned_only_on_request_is_never_swept(RootKind kind)
    {
        Assert.False(ScanWatchPolicy.ShouldSweepPeriodically(
            kind,
            ScanPolicy.Manual,
            watchLocalRoots: true));
    }

    /// <summary>
    /// The per-root flag still outranks the setting here, exactly as it does for the live watcher.
    /// </summary>
    [Theory]
    [InlineData(RootKind.Local)]
    [InlineData(RootKind.Usb)]
    [InlineData(RootKind.Unc)]
    public void A_root_that_declares_continuous_is_swept_whatever_the_setting_says(RootKind kind)
    {
        Assert.True(ScanWatchPolicy.ShouldSweepPeriodically(
            kind,
            ScanPolicy.Continuous,
            watchLocalRoots: false));
    }

    /// <summary>
    /// <b>The interval lived in two places saying different numbers.</b> The screen offered thirty
    /// minutes and the scheduler ran every fifteen, and neither knew about the other — the screen's
    /// value governed nothing at all, so nobody could notice. Fifteen wins: it is the one the code
    /// actually ran and the one the archived WP-2 evidence backs in writing, so there is no
    /// behaviour to preserve on the other side. It lives here, once, and both ends point at it.
    /// </summary>
    [Fact]
    public void The_sweep_runs_at_the_interval_the_evidence_backs()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), ScanWatchPolicy.DefaultSweepInterval);
    }

    /// <summary>
    /// The bounds the screen's spinner already paints, so that a settings file edited by hand cannot
    /// ask for a sweep every zero minutes — which is a scan in a hot loop — nor for one so far away
    /// it never arrives.
    /// </summary>
    [Fact]
    public void The_sweep_interval_has_bounds_the_screen_can_offer()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), ScanWatchPolicy.MinimumSweepInterval);
        Assert.Equal(TimeSpan.FromMinutes(1440), ScanWatchPolicy.MaximumSweepInterval);
        Assert.InRange(
            ScanWatchPolicy.DefaultSweepInterval,
            ScanWatchPolicy.MinimumSweepInterval,
            ScanWatchPolicy.MaximumSweepInterval);
    }
}
