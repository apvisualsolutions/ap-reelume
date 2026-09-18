// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>
/// The state of the video a taken frame came from, kept in a small file beside the picture.
/// </summary>
/// <remarks>
/// <para>
/// Beside the picture rather than in the database. It describes a cache entry, and a cache entry that
/// outlived its own record would be worse than one with no record at all: the two would be deleted
/// separately and disagree. Beside it, they are deleted together by anything that clears the folder.
/// </para>
/// <para>
/// It lived as two private methods of <c>GetCourseThumbnail</c> until 2026-09-18, when the frame
/// became a cover for films and series too (LIB-021) and a second copy would have been the start of
/// two formats.
/// </para>
/// </remarks>
public static class FrameStampFile
{
    /// <summary>
    /// The stamp of the file <paramref name="picture"/> was taken from, or nothing when there is no
    /// picture or its marker cannot be read — either way the frame is taken again.
    /// </summary>
    public static CourseThumbnailStamp? Read(string picture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(picture);
        var marker = picture + ".from";
        if (!File.Exists(picture) || !File.Exists(marker))
        {
            return null;
        }

        var parts = File.ReadAllText(marker).Split('|');
        return parts.Length == 2
            && long.TryParse(parts[0], CultureInfo.InvariantCulture, out var length)
            && DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, out var modified)
                ? new CourseThumbnailStamp(length, modified)
                : null;
    }

    /// <summary>Records that <paramref name="picture"/> was taken from a file in state <paramref name="stamp"/>.</summary>
    public static void Write(string picture, CourseThumbnailStamp stamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(picture);
        File.WriteAllText(
            picture + ".from",
            string.Create(CultureInfo.InvariantCulture, $"{stamp.Length}|{stamp.ModifiedUtc:O}"));
    }
}
