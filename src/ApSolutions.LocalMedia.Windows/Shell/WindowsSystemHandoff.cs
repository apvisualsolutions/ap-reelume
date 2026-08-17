// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Lifecycle;

namespace ApSolutions.LocalMedia.Windows.Shell;

/// <summary>
/// The handover as the person whose profile this is gets it: Explorer opens on the folder, and
/// leaving ends the application.
/// </summary>
/// <remarks>
/// <para>
/// The folder is passed as the whole instruction to the shell verb, so no command line is composed
/// and nothing inside the path can be read as an argument — the same shape
/// <see cref="Metadata.ShellExternalLinkLauncher"/> uses for an address.
/// </para>
/// <para>
/// Both calls out are parameters rather than hard-wired, and the coverage gate is what asked for it:
/// with <see cref="Process.Start(ProcessStartInfo)"/> written in, everything past the guard could
/// only be reached by opening a real Explorer window on the machine doing the measuring. What it
/// buys beyond coverage is that what reaches the shell can be asserted, which is the whole point of
/// this class existing separately from the screen that calls it.
/// </para>
/// <para>
/// Reaching Avalonia's desktop lifetime is <em>not</em> done here, and that was measured rather than
/// preferred: <c>IClassicDesktopStyleApplicationLifetime</c> carries a member declaring itself not
/// implementable by user code, so no double can stand in for one and the shutting-down half of that
/// decision cannot be exercised anywhere. It stays in the composition, beside the two identical
/// lifetime lookups that were already there, and what arrives here is the call itself.
/// </para>
/// </remarks>
public sealed class WindowsSystemHandoff : ISystemHandoff
{
    private readonly Func<ProcessStartInfo, Process?> _start;
    private readonly Action _shutdown;

    public WindowsSystemHandoff(Func<ProcessStartInfo, Process?> start, Action shutdown)
    {
        _start = start ?? throw new ArgumentNullException(nameof(start));
        _shutdown = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
    }

    public bool TryOpenFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        // A null process is not a failure: the folder can land in an Explorer window that was
        // already open, and there is no child of this process to report.
        return Hand(folder, requireProcess: false);
    }

    /// <summary>
    /// Opens the package, and here a process that never started <em>is</em> the failure.
    /// </summary>
    /// <remarks>
    /// The asymmetry with the folder is measured rather than stylistic, and it is the refusal that
    /// actually happens: on a clean Windows with nothing registered for <c>.msix</c> the shell call
    /// returns nothing and throws nothing, so a launcher that treated null as success would report an
    /// installation starting while nothing at all had. Where an App Installer exists it answers with
    /// a process. Both halves are archived in docs/evidence/stable/updater-handover.json.
    /// </remarks>
    public bool TryOpenPackage(string package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        return Hand(package, requireProcess: true);
    }

    public void RequestExit() => _shutdown();

    private bool Hand(string path, bool requireProcess)
    {
        try
        {
            using var process = _start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
            return !requireProcess || process is not null;
        }
        catch (Win32Exception)
        {
            // Nothing is registered to take it, or it is gone. The screen keeps offering everything
            // else it can do.
            return false;
        }
    }
}
