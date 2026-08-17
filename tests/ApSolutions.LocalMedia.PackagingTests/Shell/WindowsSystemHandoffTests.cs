// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Diagnostics;
using ApSolutions.LocalMedia.Windows.Shell;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Shell;

/// <summary>
/// The handover the person whose profile this is gets. Nothing here opens a real window or ends a
/// real application: both calls out are handed in, which is what lets the folder that reaches the
/// shell be asserted rather than only assumed.
/// </summary>
/// <remarks>
/// Reaching Avalonia's desktop lifetime is deliberately not part of this class, and it was measured
/// rather than preferred: <c>IClassicDesktopStyleApplicationLifetime</c> carries a member declaring
/// itself not implementable by user code, so no double can stand in for one. What can be asserted
/// here is that leaving asks for the shutdown, once, and that nothing else does.
/// </remarks>
public sealed class WindowsSystemHandoffTests
{
    /// <summary>
    /// What actually reaches the shell: the folder as one whole instruction, with no command line
    /// composed around it and nothing inside the path left to interpret.
    /// </summary>
    [Fact]
    public void An_offered_folder_reaches_the_shell_whole_and_alone()
    {
        var seen = new List<ProcessStartInfo>();
        var handoff = new WindowsSystemHandoff(
            startInfo =>
            {
                seen.Add(startInfo);
                return null;
            },
            NoShutdown);

        Assert.True(handoff.TryOpenFolder(@"C:\somewhere\backups"));

        var startInfo = Assert.Single(seen);
        Assert.Equal(@"C:\somewhere\backups", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Empty(startInfo.ArgumentList);
        Assert.Empty(startInfo.Arguments);
    }

    /// <summary>
    /// An Explorer window that was already open takes the folder and no child process comes back.
    /// That is a success: reporting it as a failure would say nothing happened while a window came
    /// to the front.
    /// </summary>
    [Fact]
    public void A_shell_that_returns_no_process_is_still_a_success() =>
        Assert.True(new WindowsSystemHandoff(_ => null, NoShutdown).TryOpenFolder(@"C:\somewhere"));

    /// <summary>
    /// Nothing registered to show a folder — or a folder that is gone — is a refusal rather than a
    /// crash, the same answer the recorded handover gives when it cannot write its line.
    /// </summary>
    [Fact]
    public void A_shell_that_will_not_take_the_folder_is_refused_rather_than_thrown()
    {
        var handoff = new WindowsSystemHandoff(_ => throw new Win32Exception(2), NoShutdown);

        Assert.False(handoff.TryOpenFolder(@"C:\gone"));
    }

    [Fact]
    public void A_folder_that_names_nothing_is_rejected_before_any_shell_call()
    {
        var seen = new List<ProcessStartInfo>();
        var handoff = new WindowsSystemHandoff(
            startInfo =>
            {
                seen.Add(startInfo);
                return null;
            },
            NoShutdown);

        _ = Assert.Throws<ArgumentException>(() => handoff.TryOpenFolder("   "));
        _ = Assert.Throws<ArgumentNullException>(() => handoff.TryOpenFolder(null!));
        Assert.Empty(seen);
    }

    /// <summary>
    /// A package reaches the shell the same way, whole and alone: the file is the entire
    /// instruction, so nothing in its path can be read as an argument.
    /// </summary>
    [Fact]
    public void A_package_reaches_the_shell_whole_and_alone()
    {
        var seen = new List<ProcessStartInfo>();
        var handoff = new WindowsSystemHandoff(
            startInfo =>
            {
                seen.Add(startInfo);
                return new Process();
            },
            NoShutdown);

        Assert.True(handoff.TryOpenPackage(@"C:\staging\apreelume-0.2.0.msix"));

        var startInfo = Assert.Single(seen);
        Assert.Equal(@"C:\staging\apreelume-0.2.0.msix", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Empty(startInfo.ArgumentList);
        Assert.Empty(startInfo.Arguments);
    }

    /// <summary>
    /// And here a shell that starts nothing is a refusal, which is the opposite of what it means for
    /// a folder — and it is the refusal that actually happens.
    /// </summary>
    /// <remarks>
    /// Measured on a clean Windows with nothing registered for <c>.msix</c>: the call returns null
    /// and throws nothing. Treating that as success would report an installation starting while
    /// nothing at all had.
    /// </remarks>
    [Fact]
    public void A_shell_that_starts_nothing_refuses_the_package_even_though_it_accepts_a_folder()
    {
        var handoff = new WindowsSystemHandoff(_ => null, NoShutdown);

        Assert.False(handoff.TryOpenPackage(@"C:\staging\apreelume-0.2.0.msix"));
        Assert.True(handoff.TryOpenFolder(@"C:\staging"));
    }

    [Fact]
    public void A_shell_that_will_not_take_the_package_is_refused_rather_than_thrown() =>
        Assert.False(new WindowsSystemHandoff(_ => throw new Win32Exception(2), NoShutdown)
            .TryOpenPackage(@"C:\staging\gone.msix"));

    [Fact]
    public void A_package_that_names_nothing_is_rejected_before_any_shell_call()
    {
        var seen = new List<ProcessStartInfo>();
        var handoff = new WindowsSystemHandoff(
            startInfo =>
            {
                seen.Add(startInfo);
                return null;
            },
            NoShutdown);

        _ = Assert.Throws<ArgumentException>(() => handoff.TryOpenPackage("   "));
        _ = Assert.Throws<ArgumentNullException>(() => handoff.TryOpenPackage(null!));
        Assert.Empty(seen);
    }

    /// <summary>
    /// Leaving asks for the shutdown, once, and reaches the shell for nothing on the way.
    /// </summary>
    [Fact]
    public void Asking_to_leave_asks_for_the_shutdown_and_nothing_else()
    {
        var shutdowns = 0;
        var handoff = new WindowsSystemHandoff(NoShellCall, () => shutdowns++);

        handoff.RequestExit();

        Assert.Equal(1, shutdowns);
    }

    [Fact]
    public void A_handover_without_a_way_to_reach_the_system_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new WindowsSystemHandoff(null!, NoShutdown));
        _ = Assert.Throws<ArgumentNullException>(() => new WindowsSystemHandoff(_ => null, null!));
    }

    private static void NoShutdown() =>
        Assert.Fail("The application was asked to shut down by something that is not leaving.");

    private static Process? NoShellCall(ProcessStartInfo startInfo) =>
        throw new InvalidOperationException(
            $"Leaving handed {startInfo.FileName} to the shell instead of only asking to end.");
}
