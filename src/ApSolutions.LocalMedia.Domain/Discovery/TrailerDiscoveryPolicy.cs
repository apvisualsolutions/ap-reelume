// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// Which file next to a film is its trailer (LIB-014).
/// </summary>
/// <remarks>
/// Two conventions, the ones Plex, Jellyfin and Kodi all write: a sibling named
/// <c>&lt;film&gt;-trailer.&lt;ext&gt;</c>, or a <c>Trailers</c> folder beside the film. Nothing is
/// downloaded and nothing is streamed: a trailer plays here only when it is already on the disk.
/// <para>
/// This decides a name and opens nothing. The candidate goes to the same use case a person gets from
/// Explorer, which refuses an extension outside the approved list and never writes a catalogue row.
/// What this must never do is name a path outside the film's own folder: the decoder is this
/// project's largest accepted risk, and a candidate list is only ever as trustworthy as whoever
/// built it.
/// </para>
/// </remarks>
public static class TrailerDiscoveryPolicy
{
    /// <summary>What a trailer's file name ends with, before its extension.</summary>
    public const string Suffix = "-trailer";

    /// <summary>The folder a film may keep its trailers in, directly under the film's own.</summary>
    public const string FolderName = "Trailers";

    /// <summary>
    /// The trailer for <paramref name="mediaPath"/> among <paramref name="candidates"/>, or
    /// <see langword="null"/>. The sibling convention wins over the folder because it names the film,
    /// and ties resolve in ordinal order so two runs over the same folder agree.
    /// </summary>
    public static string? Select(string mediaPath, IReadOnlyList<string> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        ArgumentNullException.ThrowIfNull(candidates);

        var film = Path.GetFullPath(mediaPath);
        var folder = Path.GetDirectoryName(film);
        if (string.IsNullOrEmpty(folder))
        {
            return null;
        }

        var expected = Path.GetFileNameWithoutExtension(film) + Suffix;
        var trailersFolder = Path.Combine(folder, FolderName);
        string? sibling = null;
        string? inFolder = null;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var resolved = Path.GetFullPath(candidate);
            if (PathsAreEqual(resolved, film)
                || !MediaFileExtensions.IsApproved(Path.GetExtension(resolved)))
            {
                continue;
            }

            // Whatever reached here has an approved extension, so it is a file with a folder above
            // it: the only path without one is a root, and a root has no extension to approve.
            var directory = Path.GetDirectoryName(resolved)!;
            if (PathsAreEqual(directory, folder))
            {
                if (Path.GetFileNameWithoutExtension(resolved)
                    .Equals(expected, StringComparison.OrdinalIgnoreCase)
                    && (sibling is null || Ordinal(resolved, sibling) < 0))
                {
                    sibling = resolved;
                }
            }
            else if (PathsAreEqual(directory, trailersFolder)
                && (inFolder is null || Ordinal(resolved, inFolder) < 0))
            {
                inFolder = resolved;
            }
        }

        return sibling ?? inFolder;
    }

    private static int Ordinal(string left, string right) =>
        string.Compare(left, right, StringComparison.Ordinal);

    /// <summary>
    /// Compares two locations the way Windows resolves them, and never by string alone: a trailing
    /// separator or a different casing is the same folder, and treating it as another one is how a
    /// containment check is passed by something that should not have passed it.
    /// </summary>
    private static bool PathsAreEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}
