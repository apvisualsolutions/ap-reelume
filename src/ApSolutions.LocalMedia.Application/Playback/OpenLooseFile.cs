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
/// Describes the session a file the person picked from Explorer would play as, and refuses the ones
/// this release will not open.
/// <para>
/// Nothing here touches the catalogue, the media-file store, watch state, or the progress tracker.
/// The tracker only writes after an explicit <c>BeginAsync</c>, which this use case never calls, so
/// a loose session leaves the database exactly as it found it.
/// </para>
/// </summary>
/// <remarks>
/// <b>It does not open anything, and that is the correction of a measured defect.</b> Until
/// 2026-08-17 it started the coordinator itself, so a file activated from Explorer played — measured,
/// <c>engine=Playing</c> — while nothing built the player surfaces, and the video ran with no picture,
/// no transport, and no way to say "this is not in your library" (measured, <c>stages=0</c>,
/// <c>surfaces=0</c>). The cause was two paths opening media where only one had a screen. So opening
/// is the player's, always, through <c>ShellSurfaces.OpenLoosePlayer</c>; what belongs here is the
/// judgement that has to happen <b>before</b> anything is opened — an approved container, and a file
/// that is really there.
/// </remarks>
/// <para>
/// It is static, and that is the compiler's verdict rather than a preference: with the opening gone
/// it holds no state, and a class registered in the container only so it can be injected is ceremony
/// around a judgement. It sits beside the other judgements this application makes without
/// collaborators — <c>RenamePolicy</c>, <c>MediaFileExtensions</c> — and it was taken out of the
/// container in the same change, because a registration nobody resolves is this repository's own
/// characteristic defect.
/// </para>
public static class OpenLooseFile
{
    public static Task<LooseFileSession> ExecuteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
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

        return Task.FromResult(new LooseFileSession(
            new MediaFileId(Guid.NewGuid()),
            resolved,
            System.IO.Path.GetFileName(resolved),
            System.IO.Path.GetDirectoryName(resolved) ?? string.Empty));
    }
}
