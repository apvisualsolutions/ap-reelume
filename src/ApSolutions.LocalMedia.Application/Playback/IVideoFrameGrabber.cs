// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>
/// Takes one still frame out of a video file and writes it where it is told.
/// </summary>
/// <remarks>
/// <para>
/// The port between what decides and what decodes. <see cref="CourseThumbnailPolicy"/> answers which
/// file, which moment and whether it is needed at all, and none of that requires a decoder; this is
/// the half that does, and it is the only half an adapter implements.
/// </para>
/// <para>
/// <b>It was called <c>ICourseFrameGrabber</c> until 2026-09-18</b>, because a course card was the
/// first thing that wanted a frame. ADR-0009 made the frame the third origin of any cover — film,
/// series or course — and said that what belongs to courses is only the wrapper, so the port is named
/// for what it does rather than for its first caller.
/// </para>
/// </remarks>
public interface IVideoFrameGrabber
{
    /// <summary>
    /// Writes a frame of <paramref name="videoPath"/> taken at <paramref name="at"/> to
    /// <paramref name="destinationPath"/>, and answers whether it managed to.
    /// </summary>
    /// <remarks>
    /// It answers <see langword="false"/> rather than throwing when a file has no frame to give.
    /// That is an ordinary state — a container no decoder here understands is already a state this
    /// application names — and a title whose picture cannot be taken is a card without one rather
    /// than an error somebody has to read.
    /// </remarks>
    Task<bool> TryCaptureAsync(
        string videoPath,
        TimeSpan at,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
