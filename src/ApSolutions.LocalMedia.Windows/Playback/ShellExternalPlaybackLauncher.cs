using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Playback;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// Opens a file with its registered Windows handler. The path is passed as a single argument to the
/// shell verb, so no command line is composed and nothing in the file name can be interpreted.
/// </summary>
public sealed class ShellExternalPlaybackLauncher : IExternalPlaybackLauncher
{
    public Task<bool> TryLaunchAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
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
