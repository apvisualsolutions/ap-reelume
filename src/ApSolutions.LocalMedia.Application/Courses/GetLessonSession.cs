// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Courses;

/// <summary>
/// A playing session that turned out to be a lesson: the course around it and which lesson it is.
/// </summary>
/// <remarks>
/// <see cref="Lessons"/> is the whole course flattened back into watching order, which is the order
/// the modules were built from. The panel draws modules and the countdown walks a sequence, and
/// deriving one from the other at each call site is how the two would end up disagreeing about what
/// «next» means.
/// </remarks>
public sealed record LessonSession(
    CourseDetail Course,
    LessonId LessonId,
    IReadOnlyList<CourseLessonProgress> Lessons);

/// <summary>
/// Answers whether the file that is playing is a lesson, and hands back the course around it
/// (CRS-004).
/// </summary>
/// <remarks>
/// It is asked of the file rather than told by the caller, which is the whole design decision here.
/// A session holds a file: the countdown opens the next lesson with nothing but an id, «Retomar el
/// hilo» opens one from the home rail, and a lesson opened from Explorer is a loose file until the
/// catalogue says otherwise. A course riding along on the request would be missing down every path
/// that forgot to forward it, and the panel's failure mode is <b>absence</b> — so it would go
/// quietly missing rather than visibly wrong, which is the defect this repository keeps finding in
/// itself.
/// </remarks>
public sealed class GetLessonSession
{
    private readonly ICourseRepository _courses;
    private readonly GetCourses _detail;

    public GetLessonSession(ICourseRepository courses, GetCourses detail)
    {
        _courses = courses ?? throw new ArgumentNullException(nameof(courses));
        _detail = detail ?? throw new ArgumentNullException(nameof(detail));
    }

    /// <summary>
    /// The lesson session this file belongs to, or <see langword="null"/> when it is not a lesson.
    /// </summary>
    public async Task<LessonSession?> FindAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default)
    {
        var lesson = await _courses.FindLessonByFileAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            return null;
        }

        // The course row and the lesson row can disagree for exactly one moment: a course unmarked
        // between the two reads. Absent beats half a panel, so the whole session is refused rather
        // than drawn around a course that no longer exists.
        var course = await _detail.GetAsync(lesson.CourseId, cancellationToken).ConfigureAwait(false);
        if (course is null)
        {
            return null;
        }

        var lessons = course.Modules.SelectMany(module => module.Lessons).ToArray();

        // A lesson whose row the reader did not return is a lesson the progress join dropped, and
        // there is no panel to draw for a course that does not contain the thing being played.
        return Array.Exists(lessons, row => row.Id == lesson.Id)
            ? new LessonSession(course, lesson.Id, lessons)
            : null;
    }
}
