// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Courses;

/// <summary>
/// Takes one still frame out of a video file and writes it where it is told.
/// </summary>
/// <remarks>
/// The port between what decides and what decodes. <see cref="CourseThumbnailPolicy"/> answers which
/// lesson, which moment and whether it is needed at all, and none of that requires a decoder; this is
/// the half that does, and it is the only half an adapter implements.
/// </remarks>
public interface ICourseFrameGrabber
{
    /// <summary>
    /// Writes a frame of <paramref name="videoPath"/> taken at <paramref name="at"/> to
    /// <paramref name="destinationPath"/>, and answers whether it managed to.
    /// </summary>
    /// <remarks>
    /// It answers <see langword="false"/> rather than throwing when a file has no frame to give.
    /// That is an ordinary state — a container no decoder here understands is already a state this
    /// application names — and a course whose picture cannot be taken is a card without one rather
    /// than an error somebody has to read.
    /// </remarks>
    Task<bool> TryCaptureAsync(
        string videoPath,
        TimeSpan at,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

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
    ICourseFrameGrabber grabber)
{
    private readonly IAppDataPaths _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    private readonly ICourseFrameGrabber _grabber = grabber ?? throw new ArgumentNullException(nameof(grabber));

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

        var stored = Stored(destination);
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

        Remember(destination, current.Value);
        return destination;
    }

    /// <summary>
    /// The stamp of the file a stored picture was taken from, or nothing when there is no picture.
    /// </summary>
    /// <remarks>
    /// Kept in a small file beside the picture rather than in the database. It describes a cache
    /// entry, and a cache entry that outlived its own record would be worse than one with no record
    /// at all: the two would be deleted separately and disagree. Beside it, they are deleted
    /// together by anything that clears the folder.
    /// </remarks>
    private static CourseThumbnailStamp? Stored(string destination)
    {
        var marker = destination + ".from";
        if (!File.Exists(destination) || !File.Exists(marker))
        {
            return null;
        }

        var parts = File.ReadAllText(marker).Split('|');
        return parts.Length == 2
            && long.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var length)
            && DateTimeOffset.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var modified)
                ? new CourseThumbnailStamp(length, modified)
                : null;
    }

    private static void Remember(string destination, CourseThumbnailStamp stamp) =>
        File.WriteAllText(
            destination + ".from",
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{stamp.Length}|{stamp.ModifiedUtc:O}"));
}
