// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Recovery;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// Waiting for the startup the application actually performs.
/// </summary>
/// <remarks>
/// ARQ-005. <c>CreateShell</c> hands back a container holding the startup view and prepares the
/// database off the interface thread, so a walk that wants the shell has to wait for it instead of
/// assuming it arrived. The wait is bounded, and what it says when the bound expires is the point: a
/// timeout that only reports "it never came" diagnoses nothing, so this one names whatever was left
/// standing in the shell's place — the startup view means the work is still running, and the
/// recovery screen means it finished and refused.
/// </remarks>
internal static class AssembledStartup
{
    /// <summary>
    /// Long enough that a loaded build server is not called a failure, short enough that a real hang
    /// is still reported as one rather than running out the suite's own patience.
    /// </summary>
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Pumps the interface thread until the container holds what the startup settled on.
    /// </summary>
    public static Control FinalContent(Control created)
    {
        var container = Assert.IsType<ContentControl>(created);
        var deadline = DateTime.UtcNow + Ceiling;
        while (DateTime.UtcNow < deadline)
        {
            // The preparation runs on the thread pool; this thread owns the dispatcher, so nothing
            // it posted back can land until the jobs are run.
            Dispatcher.UIThread.RunJobs();
            if (container.Content is ShellView or DatabaseRecoveryView)
            {
                return (Control)container.Content;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException(
            $"The startup did not settle within {Ceiling.TotalSeconds:F0} s. The window is still "
                + $"showing {container.Content?.GetType().Name ?? "nothing at all"}.");
    }
}
