// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Windows.Metadata;

/// <summary>
/// Writes the address down instead of opening it, for a run that does not own this machine's
/// profile.
/// </summary>
/// <remarks>
/// <para>
/// It sits beside <see cref="ShellExternalLinkLauncher"/> because the two are the same exit: which
/// one the application is built with is decided by the data root, once, in the composition. Both ask
/// <see cref="ExternalLinkPolicy"/> first, so what is written down is exactly what the other one
/// would have handed the browser — an assertion on this file is an assertion about the real thing,
/// and the moment the two disagreed it would stop being one.
/// </para>
/// <para>
/// Appending rather than replacing: the record is what this run would have opened, in the order it
/// asked, so a walk that presses the same offer on two different cards can tell the two apart. And
/// it is a plain address per line because that is the whole of what leaves — there is no request, no
/// header and no connection to describe.
/// </para>
/// </remarks>
public sealed class RecordingExternalLinkLauncher : IExternalLinkLauncher
{
    /// <summary>The file inside the handover folder, named for what it holds.</summary>
    public const string FileName = "external-links.txt";

    private readonly Lock _gate = new();

    public RecordingExternalLinkLauncher(string handoffDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        RecordPath = Path.Combine(handoffDirectory, FileName);
    }

    /// <summary>Where the addresses land, so whoever asked can read what would have opened.</summary>
    public string RecordPath { get; }

    public Task<bool> TryLaunchAsync(string link, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(link);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ExternalLinkPolicy.TryAccept(link, out var address))
        {
            return Task.FromResult(false);
        }

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!);
                File.AppendAllText(RecordPath, address.AbsoluteUri + Environment.NewLine);
            }

            return Task.FromResult(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The same answer the shell exit gives when nothing is registered for https: the offer
            // could not be honoured, and the card goes on offering everything else it can do.
            return Task.FromResult(false);
        }
    }
}
