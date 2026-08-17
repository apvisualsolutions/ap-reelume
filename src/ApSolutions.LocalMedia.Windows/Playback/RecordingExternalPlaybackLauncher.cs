// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Windows.Shell;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// Writes down the file that would have been opened with the system's own player, for a run that
/// does not own this machine's profile.
/// </summary>
/// <remarks>
/// <para>
/// The ninth exit covered by the isolation rule, and it sits beside
/// <see cref="ShellExternalPlaybackLauncher"/> for the reason all the others do: which one the
/// application is built with is decided by the data root, once, in the composition. Pressing "Open
/// with an external application" against the real one starts a <b>real process</b> —
/// <c>UseShellExecute</c> hands the path to whatever Windows plays it with — so a harness measuring
/// this screen would open the system player on the machine measuring it.
/// </para>
/// <para>
/// The two refusals are repeated here rather than assumed, and that is the whole reason this class
/// can stand in for the other: a probe that reads a written-down handover only says something about
/// the real launcher if the two agree on <b>what never gets handed over</b>. An extension outside the
/// approved list and a file that is not there are both refusals of the real one, so they are refusals
/// here — measured the same way, before anything is written.
/// </para>
/// </remarks>
public sealed class RecordingExternalPlaybackLauncher : IExternalPlaybackLauncher
{
    private readonly RecordingSystemHandoff _handoff;

    public RecordingExternalPlaybackLauncher(RecordingSystemHandoff handoff) =>
        _handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));

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

        return Task.FromResult(_handoff.RecordPlayedExternally(Path.GetFullPath(path)));
    }
}
