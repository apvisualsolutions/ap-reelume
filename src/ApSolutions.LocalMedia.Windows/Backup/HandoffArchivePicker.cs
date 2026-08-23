// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Windows.Backup;

/// <summary>
/// Answers where an archive goes and which archive comes back, for a run that does not own this
/// machine's profile.
/// </summary>
/// <remarks>
/// <para>
/// It sits beside the two Windows pickers in <c>CompositionRoot</c> because the three are the same
/// exit: which pair the application is built with is decided by the data root, once, in the
/// composition — the same way the trailer link chooses between opening a browser and writing the
/// address down. The person whose profile this is still gets the dialog Windows owns.
/// </para>
/// <para>
/// The isolated answers stay inside the handover folder, which is the whole of the rule: a run
/// handed a root of its own writes nothing and opens nothing outside it. Composing the path rather
/// than being handed one is the part worth naming — a modal dialog is what normally keeps the
/// application from seeing a folder nobody offered it, and here the confinement to that root does
/// the same job.
/// </para>
/// <para>
/// Export writes into the folder and restore reads what is in it, so the two halves chain the way
/// they do for a person: what came out is what goes back. Nothing here invents an archive — with the
/// folder empty, choosing a source answers <see langword="null"/>, which is exactly what a cancelled
/// dialog answers, and the wizard treats it as such.
/// </para>
/// </remarks>
public sealed class HandoffArchivePicker
{
    /// <summary>What the Windows save dialog suggests, so both exits produce the same kind of name.</summary>
    private const string ArchivePattern = "*.zip";

    private readonly string _handoffDirectory;

    public HandoffArchivePicker(string handoffDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        _handoffDirectory = handoffDirectory;
    }

    /// <summary>
    /// Where the export lands. The folder is created because the dialog it stands in for can only
    /// ever hand back somewhere that exists.
    /// </summary>
    public Task<string?> ChooseDestinationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_handoffDirectory);
        var name = $"apsolutions-localmedia-{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}.zip";
        return Task.FromResult<string?>(Path.Combine(_handoffDirectory, name));
    }

    /// <summary>
    /// Which archive to restore from: the newest one lying in the handover folder, and nothing at all
    /// if there is none.
    /// </summary>
    /// <remarks>
    /// Newest rather than first, because a run that exported twice means the second one. Ties are
    /// broken by name so that two archives written inside the same clock tick still resolve to one
    /// answer rather than to whatever the file system happened to enumerate first.
    /// </remarks>
    public Task<string?> ChooseSourceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_handoffDirectory))
        {
            return Task.FromResult<string?>(null);
        }

        var newest = Directory.EnumerateFiles(_handoffDirectory, ArchivePattern)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return Task.FromResult(newest);
    }

    /// <summary>
    /// Which folder to catalogue, for a run that cannot open the Windows picker: a media folder
    /// composed inside the handover root, created because the dialog it stands in for can only
    /// ever hand back somewhere that exists.
    /// </summary>
    /// <remarks>
    /// The fourth answer of the same exit, and confined the same way: a run handed a root of its
    /// own writes nothing and opens nothing outside it.
    /// </remarks>
    public Task<string?> ChooseMediaFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folder = Path.Combine(_handoffDirectory, "media");
        Directory.CreateDirectory(folder);
        return Task.FromResult<string?>(folder);
    }
}
