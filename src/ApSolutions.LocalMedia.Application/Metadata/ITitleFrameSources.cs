// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>
/// A title's video to take a frame from (LIB-021), with what identifies that file's current state so
/// an unchanged file is never decoded twice.
/// </summary>
/// <param name="Stamp">
/// The file's length and last write, compared the way a course thumbnail's are. The type is named for
/// courses because they were first; what it holds belongs to any video.
/// </param>
public sealed record TitleFrameSource(
    TitleId TitleId,
    string VideoPath,
    TimeSpan? Duration,
    CourseThumbnailStamp Stamp);

/// <summary>Which titles need a frame for a cover, and from which video.</summary>
public interface ITitleFrameSources
{
    /// <summary>
    /// Every film, series and unidentified file that has neither a picked cover nor a provider poster
    /// and whose video is on a disk that is here now. A series answers with its first available
    /// episode outside the specials, because a special is usually an extra and not the series.
    /// </summary>
    Task<IReadOnlyList<TitleFrameSource>> ListWithoutCoverAsync(CancellationToken cancellationToken = default);
}
