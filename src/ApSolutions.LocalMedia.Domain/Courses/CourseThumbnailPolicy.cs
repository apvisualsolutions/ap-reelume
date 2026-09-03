// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Courses;

/// <summary>
/// What a course's picture was taken from, so it can be told apart from what is there now.
/// </summary>
/// <remarks>
/// The length and the write time rather than a hash of the content: a course's lesson is a video
/// file somebody keeps on their own disk, and hashing one to decide whether a thumbnail is still
/// good would read gigabytes to answer a question about a 40 KB picture. Both together move whenever
/// a file is re-encoded, replaced or truncated, which is every case this has to catch.
/// </remarks>
public readonly record struct CourseThumbnailStamp(long Length, DateTimeOffset ModifiedUtc);

/// <summary>What the grid should do about one course's picture.</summary>
public enum CourseThumbnailAction
{
    /// <summary>Nothing can be taken: the course has no lesson whose file the catalogue has seen.</summary>
    Impossible,

    /// <summary>A frame has to be taken, because there is none or the one there is went stale.</summary>
    Capture,

    /// <summary>The picture already stored still describes the file it came from.</summary>
    Keep,
}

/// <summary>
/// Which frame of which lesson becomes a course's picture, and when the one stored stops being it
/// (CRS-006).
/// </summary>
/// <remarks>
/// <b>Every decision here is separated from the decoder on purpose.</b> Taking a frame needs LibVLC
/// and a real file; deciding <i>which</i> frame, <i>when</i> to take it again and <i>when to give
/// up</i> needs neither, and this repository's tenth rule is that only the half which talks to the
/// machine is excluded from coverage. Putting the arithmetic behind the decoder is what would make
/// this look untestable.
/// <para>
/// <b>The numbers come from a measurement rather than from taste</b> — «docs/evidence/stable/
/// CRS-thumbnail-spike.md», 2026-09-03, against real decoding.
/// </para>
/// </remarks>
public static class CourseThumbnailPolicy
{
    /// <summary>How far into a lesson the frame is taken from.</summary>
    /// <remarks>
    /// A tenth of the way in, and not the beginning: the frame at zero is a black frame, a fade or a
    /// title card in almost every video anybody records, and a grid of black rectangles is worse than
    /// no picture at all. It is far enough to be past an intro and near enough that a seek into a
    /// long file stays cheap — measured at about 460 ms per file at this fraction.
    /// </remarks>
    public const double SeekFraction = 0.10;

    /// <summary>How long to wait for a frame before giving the course up.</summary>
    /// <remarks>
    /// <b>There is a deadline because one file measurably has no frame to give.</b> The spike's
    /// unsupported sample never produced one, and the harness spent 4.5 s finding that out; the four
    /// that worked handed a frame over between 433 and 472 ms after the seek. Three seconds is six
    /// times the slowest success and shorter than the failure, so it separates them — and without
    /// it, one file no decoder understands would hold up every card behind it.
    /// </remarks>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    /// <summary>Where in a lesson of this length the frame is taken from.</summary>
    /// <remarks>
    /// Clamped at zero rather than trusted: a course whose catalogue has no duration for a lesson
    /// would otherwise ask the decoder to seek to a negative position, and what LibVLC does with one
    /// is not something this policy gets to assume.
    /// </remarks>
    public static TimeSpan SeekPosition(TimeSpan duration) =>
        duration <= TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromTicks((long)(duration.Ticks * SeekFraction));

    /// <summary>
    /// The lesson a course's picture is taken from: the first one whose file the catalogue has seen.
    /// </summary>
    /// <remarks>
    /// <b>The first and not the one the thread points at</b>, which is the decision this makes and
    /// the one that could sensibly have gone the other way. The thread's lesson moves every time
    /// somebody watches anything, so a grid keyed on it would redraw itself as a side effect of
    /// watching — a course you are working through would change its face weekly, and the picture
    /// would stop being how you recognise it. The first lesson is a course's cover in the sense a
    /// cover is meant: fixed while the folder is.
    /// <para>
    /// Lessons with no <see cref="CourseLessonProgress.MediaFileId"/> are skipped rather than
    /// refused, because that is a file the catalogue has not seen — there is nothing to open — and a
    /// course whose first lesson is missing still has a second one.
    /// </para>
    /// <para>
    /// It takes lessons already in watching order rather than the modules they came in, and that is
    /// the layering rather than a simplification: a module is an Application-layer view and this is
    /// Domain, which only looks inwards. The caller flattens, which it has to do anyway.
    /// </para>
    /// </remarks>
    public static CourseLessonProgress? Source(IReadOnlyList<CourseLessonProgress> lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);

        foreach (var lesson in lessons)
        {
            if (lesson.MediaFileId is not null)
            {
                return lesson;
            }
        }

        return null;
    }

    /// <summary>What to do about a course's picture, given what is stored and what is on disk.</summary>
    /// <remarks>
    /// <paramref name="current"/> is <see langword="null"/> when the file the picture came from is
    /// gone. That is <see cref="CourseThumbnailAction.Capture"/> rather than
    /// <see cref="CourseThumbnailAction.Impossible"/>: the source lesson is chosen from the
    /// catalogue on every pass, so a missing file means the course now points somewhere else and the
    /// picture has to be taken again from wherever that is.
    /// </remarks>
    public static CourseThumbnailAction Decide(
        bool hasSource,
        CourseThumbnailStamp? stored,
        CourseThumbnailStamp? current)
    {
        if (!hasSource)
        {
            return CourseThumbnailAction.Impossible;
        }

        if (stored is not { } was || current is not { } now)
        {
            return CourseThumbnailAction.Capture;
        }

        return was == now ? CourseThumbnailAction.Keep : CourseThumbnailAction.Capture;
    }
}
