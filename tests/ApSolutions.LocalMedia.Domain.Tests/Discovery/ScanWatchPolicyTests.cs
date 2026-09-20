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
}
