// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>
/// One playback session for a file that is not in the library.
/// <para>
/// The identifier is generated for this session and handed to nothing that writes, so it can never
/// become a catalogue row. The folder is carried alongside because the only way to change that is to
/// add the folder — never the single file.
/// </para>
/// </summary>
public sealed record LooseFileSession(
    MediaFileId MediaFileId,
    string Path,
    string DisplayName,
    string FolderPath);

/// <summary>
/// Plays a file the person picked from Explorer.
/// <para>
/// Nothing here touches the catalogue, the media-file store, watch state, or the progress tracker.
/// The tracker only writes after an explicit <c>BeginAsync</c>, which this use case never calls, so
/// a loose session leaves the database exactly as it found it.
/// </para>
/// </summary>
public sealed class OpenLooseFile
{
    private readonly IPlaybackSessionCoordinator _coordinator;

    public OpenLooseFile(IPlaybackSessionCoordinator coordinator) =>
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    public async Task<LooseFileSession> ExecuteAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = System.IO.Path.GetFullPath(path);

        if (!MediaFileExtensions.IsApproved(System.IO.Path.GetExtension(resolved)))
        {
            throw new PlaybackFailureException(new PlaybackFailure(
                PlaybackFailureCode.UnsupportedCodec,
                $"The container {System.IO.Path.GetExtension(resolved)} is not one this release opens."));
        }

        if (!File.Exists(resolved))
        {
            throw new PlaybackFailureException(new PlaybackFailure(
                PlaybackFailureCode.FileNotFound,
                "The file is not where the activation said it was."));
        }

        var session = new LooseFileSession(
            new MediaFileId(Guid.NewGuid()),
            resolved,
            System.IO.Path.GetFileName(resolved),
            System.IO.Path.GetDirectoryName(resolved) ?? string.Empty);

        await _coordinator
            .StartAsync(new PlaybackRequest(session.MediaFileId, session.Path), cancellationToken)
            .ConfigureAwait(false);
        return session;
    }
}
