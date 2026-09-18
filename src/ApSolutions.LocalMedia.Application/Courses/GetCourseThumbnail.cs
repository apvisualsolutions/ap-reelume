// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Courses;

/// <summary>
/// A course's picture: the file holding it, taken from the course's own first lesson (CRS-006).
/// </summary>
/// <remarks>
/// <b>It is the picture the prototype draws, with the one substitution the prototype could not
/// make.</b> Every card there opens with a 16:9 panel filled by a generated gradient, because the
/// design package cannot ship artwork. A course is detected from a folder and never looked up, so
/// that panel would be a placeholder for ever — unless the picture came from the video, which
/// measurement on 2026-09-03 said it can.
/// <para>
/// <b>A cover somebody chose wins.</b> A course's identity is a title's identity, so LIB-018's
/// picker reaches courses like anything else, and a picture the application took for itself must
/// never sit over one a person picked.
/// </para>
/// </remarks>
public sealed class GetCourseThumbnail(
    IAppDataPaths paths,
    IVideoFrameGrabber grabber)
{
    private readonly IAppDataPaths _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    private readonly IVideoFrameGrabber _grabber = grabber ?? throw new ArgumentNullException(nameof(grabber));

    /// <summary>Where this course's taken frame lives, whether or not it exists yet.</summary>
    /// <remarks>
    /// Named by the course's own id rather than by its title: a folder somebody renames is the same
    /// course, and a name would put a second file beside the first every time.
    /// </remarks>
    public string FileFor(CourseId course) =>
        Path.Combine(_paths.CourseThumbnailDirectory, $"{course.Value:N}.png");

    /// <summary>
    /// The picture for this course, taking it first if there is none or the one there went stale.
    /// </summary>
    /// <remarks>
    /// <paramref name="chosenCover"/> is whatever LIB-018 stored for this course. When there is one
    /// this answers with it and decodes nothing at all: the cheapest frame is the one nobody takes.
    /// </remarks>
    public async Task<string?> ExecuteAsync(
        CourseId course,
        IReadOnlyList<CourseLessonProgress> lessons,
        Func<CourseLessonProgress, string?> fileOf,
        string? chosenCover = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        ArgumentNullException.ThrowIfNull(fileOf);

        if (!string.IsNullOrWhiteSpace(chosenCover) && File.Exists(chosenCover))
        {
            return chosenCover;
        }

        var lesson = CourseThumbnailPolicy.Source(lessons);
        var source = lesson is null ? null : fileOf(lesson);
        var destination = FileFor(course);

        var onDisk = source is null ? null : new FileInfo(source);
        var current = onDisk is { Exists: true }
            ? new CourseThumbnailStamp(onDisk.Length, onDisk.LastWriteTimeUtc)
            : (CourseThumbnailStamp?)null;

        var stored = FrameStampFile.Read(destination);
        var action = CourseThumbnailPolicy.Decide(source is not null, stored, current);

        if (action == CourseThumbnailAction.Impossible)
        {
            return null;
        }

        if (action == CourseThumbnailAction.Keep)
        {
            return destination;
        }

        // Nothing to take it from: the lesson is in the catalogue but its file is not on the disk
        // right now, which is a removable drive rather than a defect.
        if (current is null || lesson is null || source is null)
        {
            return File.Exists(destination) ? destination : null;
        }

        Directory.CreateDirectory(_paths.CourseThumbnailDirectory);
        var taken = await _grabber
            .TryCaptureAsync(source, CourseThumbnailPolicy.SeekPosition(lesson.Duration), destination, cancellationToken)
            .ConfigureAwait(false);

        if (!taken)
        {
            return null;
        }

        FrameStampFile.Write(destination, current.Value);
        return destination;
    }
}
