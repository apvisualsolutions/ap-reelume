// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Domain.Courses;

/// <summary>
/// One lesson as the thread sees it: where it sits, how long it runs, and how far in somebody got.
/// </summary>
/// <remarks>
/// <see cref="Duration"/> is the catalogue's, not a column of its own: storing it beside the lesson
/// would be a copy of what the media file already knows, and a copy is a thing that goes stale.
/// </remarks>
public sealed record CourseLessonProgress(
    LessonId Id,

    /// <summary>
    /// LIB-009's identity, and <see langword="null"/> when the catalogue has not seen the file.
    /// Without it a lesson can neither be played nor marked, so the row refuses both rather than
    /// failing when pressed.
    /// </summary>
    MediaFileId? MediaFileId,
    int ModuleNumber,
    string? Module,
    int Number,
    string Title,
    TimeSpan Duration,
    TimeSpan Position,
    WatchStatus Status);

/// <summary>
/// Where somebody left a course off (CRS-002): the lesson, the minute, and whether they were part
/// way through it or had not started it.
/// </summary>
/// <remarks>
/// <see cref="Lesson"/> is <see langword="null"/> exactly when the course is finished, which is what
/// turns «Retomar el hilo» into «Volver a empezar» and what the finished chip is drawn from. It is
/// one flag read off one place rather than two states that could disagree.
/// </remarks>
public sealed record CourseThread(
    LessonId? Lesson,
    int ModuleNumber,
    int LessonNumber,
    string LessonTitle,
    TimeSpan Position,
    TimeSpan Duration,
    bool IsPartial)
{
    public static CourseThread Finished { get; } =
        new(null, 0, 0, string.Empty, TimeSpan.Zero, TimeSpan.Zero, false);

    public bool IsCourseFinished => Lesson is null;
}

/// <summary>What a course card says without opening it (CRS-003).</summary>
public sealed record CourseSummary(int WatchedLessons, int TotalLessons, TimeSpan Remaining)
{
    public bool IsFinished => TotalLessons > 0 && WatchedLessons == TotalLessons;

    /// <summary>
    /// A course with no lessons is not finished, and this is why it is asked separately: a marked
    /// folder whose walk has not run yet would otherwise draw as complete, congratulating somebody
    /// for a course that has not been read.
    /// </summary>
    public bool IsEmpty => TotalLessons == 0;
}

/// <summary>
/// The thread a course keeps for itself (CRS-002), read from progress that already exists.
/// </summary>
/// <remarks>
/// The rule is the one a person would use: the thread points at the <b>first lesson in watching
/// order that is not watched</b>. Not the last one played — that would send somebody back to a
/// lesson they finished and skipped past — and not the furthest reached, which after a jump ahead
/// would quietly abandon everything in between.
/// </remarks>
public static class CourseThreadPolicy
{
    /// <summary>
    /// Where the course is left off. <paramref name="lessons"/> is already in watching order, which
    /// is the order the store hands them back in and the one the file names decided.
    /// </summary>
    public static CourseThread Resolve(IReadOnlyList<CourseLessonProgress> lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);

        foreach (var lesson in lessons)
        {
            if (lesson.Status == WatchStatus.Watched)
            {
                continue;
            }

            return new CourseThread(
                lesson.Id,
                lesson.ModuleNumber,
                lesson.Number,
                lesson.Title,
                lesson.Position,
                lesson.Duration,
                lesson.Status == WatchStatus.InProgress);
        }

        return CourseThread.Finished;
    }

    /// <summary>
    /// How much of the course is done and how much is left. What remains counts the whole of every
    /// unwatched lesson except the one in progress, which counts only what is left of it — somebody
    /// forty minutes into an hour has twenty minutes left, not sixty.
    /// </summary>
    public static CourseSummary Summarise(IReadOnlyList<CourseLessonProgress> lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);

        var watched = 0;
        var remaining = TimeSpan.Zero;
        foreach (var lesson in lessons)
        {
            if (lesson.Status == WatchStatus.Watched)
            {
                watched++;
                continue;
            }

            var left = lesson.Duration - lesson.Position;
            remaining += left > TimeSpan.Zero ? left : lesson.Duration;
        }

        return new CourseSummary(watched, lessons.Count, remaining);
    }

    /// <summary>
    /// The last two lessons somebody finished, newest first — «Lo último que viste». It is what
    /// answers «¿de qué iba?» without making anybody re-watch anything, and it is two because a
    /// longer list stops being a reminder and becomes a second table of contents.
    /// </summary>
    public static IReadOnlyList<CourseLessonProgress> Recap(IReadOnlyList<CourseLessonProgress> lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);

        // The thread is the first lesson that is not watched, so everything before it is watched by
        // definition -- which makes the recap simply the tail of the opening run, newest first, and
        // removes the need to locate the thread at all.
        //
        // It is written as a loop rather than TakeWhile on purpose. A lambda capturing the thread
        // gets a delegate cache whose `dup; brtrue.s` can never be taken: the closure is rebuilt on
        // every call, so the cached field is null every time. That single unreachable branch held
        // the file at 93.75 and, unlike a file already on the debt list, a file that is new against
        // main has to reach 96/96 -- a measured ceiling does not excuse it.
        var opening = new List<CourseLessonProgress>();
        foreach (var lesson in lessons)
        {
            if (lesson.Status != WatchStatus.Watched)
            {
                break;
            }

            opening.Add(lesson);
        }

        opening.Reverse();
        return opening.Count <= 2 ? opening : opening.GetRange(0, 2);
    }
}
