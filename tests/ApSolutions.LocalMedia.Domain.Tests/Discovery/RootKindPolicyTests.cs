// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

/// <summary>
/// A folder's kind, read from its path alone. The drive query is handed in, which is what makes the
/// branch no hosted runner owns a removable drive for — USB — reachable from a test at all.
/// </summary>
public sealed class RootKindPolicyTests
{
    [Fact]
    public void A_unc_prefix_is_unc_before_any_drive_is_asked()
    {
        var asked = false;
        var kind = RootKindPolicy.Detect(@"\\nas\cine", _ =>
        {
            asked = true;
            return DriveType.Network;
        });

        Assert.Equal(RootKind.Unc, kind);
        Assert.False(asked, "A UNC path asked the drive a question its prefix had already answered.");
    }

    [Fact]
    public void A_removable_drive_is_usb_and_a_fixed_one_is_local()
    {
        Assert.Equal(RootKind.Usb, RootKindPolicy.Detect(@"E:\peliculas", _ => DriveType.Removable));
        Assert.Equal(RootKind.Local, RootKindPolicy.Detect(@"D:\cine", _ => DriveType.Fixed));
    }

    [Fact]
    public void A_path_without_a_root_never_asks_and_is_local()
    {
        var kind = RootKindPolicy.Detect("relative-folder", _ => throw new IOException("never"));
        Assert.Equal(RootKind.Local, kind);
    }

    [Fact]
    public void A_blank_path_is_local_without_a_question()
    {
        Assert.Equal(RootKind.Local, RootKindPolicy.Detect("  ", _ => DriveType.Removable));
    }

    /// <summary>
    /// A drive that cannot be asked answers local: a wrong "local" costs a slower watch policy,
    /// while a thrown question would cost the dialog that asked it.
    /// </summary>
    [Fact]
    public void A_drive_that_throws_is_local()
    {
        Assert.Equal(
            RootKind.Local,
            RootKindPolicy.Detect(@"E:\peliculas", _ => throw new IOException("device not ready")));
        Assert.Equal(
            RootKind.Local,
            RootKindPolicy.Detect(@"E:\peliculas", _ => throw new UnauthorizedAccessException()));
    }

    [Fact]
    public void The_drive_query_is_required()
    {
        Assert.Throws<ArgumentNullException>(() => RootKindPolicy.Detect(@"D:\cine", null!));
    }
}
