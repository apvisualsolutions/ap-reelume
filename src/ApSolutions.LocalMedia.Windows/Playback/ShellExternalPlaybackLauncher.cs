// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// Opens a file with its registered Windows handler. The path is passed as a single argument to the
/// shell verb, so no command line is composed and nothing in the file name can be interpreted.
/// </summary>
/// <remarks>
/// The extension is checked here even though every caller already filters by it. The audit filed
/// that as not exploitable and worth doing anyway, and measuring it settled the argument: before
/// this check the launcher handed a `.ps1`, a `.txt` and an extensionless file straight to their
/// registered handlers. This is the one place that talks to the shell, so this is where "only the
/// containers the library catalogues" has to be true — not in the good behaviour of everyone who
/// might one day call it.
/// </remarks>
public sealed class ShellExternalPlaybackLauncher : IExternalPlaybackLauncher
{
    public Task<bool> TryLaunchAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        if (!MediaFileExtensions.IsApproved(Path.GetExtension(path)))
        {
            return Task.FromResult(false);
        }

        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        var startInfo = new ProcessStartInfo(Path.GetFullPath(path))
        {
            UseShellExecute = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            return Task.FromResult(process is not null || startInfo.UseShellExecute);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No handler is registered for this extension; the caller keeps offering other actions.
            return Task.FromResult(false);
        }
    }
}
