// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using ApSolutions.LocalMedia.Application.Updates;

namespace ApSolutions.LocalMedia.Windows.Updates;

/// <summary>
/// Gives Windows the verified package and stops there.
/// </summary>
/// <remarks>
/// This application does not install anything. It opens the package the way a person would, and the
/// App Installer takes over: what replaces the running binary is Windows, under its own rules, with
/// its own rollback. An updater that replaced its own files while running would have to decide what
/// to do when it failed halfway, and the only honest answer to that is to never be in that position.
/// <para>
/// Everything that can go wrong comes back as <c>false</c> rather than as an exception. A refusal
/// leaves the running installation and the verified package exactly where they were, which is what
/// makes trying again a normal thing to do rather than a recovery.
/// </para>
/// <para>
/// The refusal that actually happens is not the one this file looks like it is built around.
/// Measured in a clean Windows with nothing registered for `.msix`, the shell call returns nothing
/// and throws nothing: the <c>false</c> comes from the handover reporting that no handler started,
/// not from the catch below. The catch is still right — a machine can refuse in the other ways too —
/// but anybody reading this should know which path is the live one.
/// </para>
/// </remarks>
public sealed class WindowsUpdateLauncher : IUpdateLauncher
{
    private readonly Func<string, bool> _hand;

    /// <summary>
    /// Takes the handover itself as a parameter. Opening a file the way a person would is a shell
    /// call, and this repository keeps those in the composition root — the same place that already
    /// opens the backup folder. It also means everything decided here can be exercised without a
    /// suite being able to start a real installer.
    /// </summary>
    public WindowsUpdateLauncher(Func<string, bool> hand) =>
        _hand = hand ?? throw new ArgumentNullException(nameof(hand));

    public Task<bool> LaunchAsync(StagedUpdate staged, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staged);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(staged.Path))
        {
            return Task.FromResult(false);
        }

        try
        {
            return Task.FromResult(_hand(staged.Path));
        }
        catch (Exception exception)
            when (exception is Win32Exception or InvalidOperationException or IOException
                or System.Security.SecurityException or PlatformNotSupportedException)
        {
            return Task.FromResult(false);
        }
    }
}
