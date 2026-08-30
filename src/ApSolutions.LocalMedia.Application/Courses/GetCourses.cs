// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Courses;

/// <summary>
/// The lessons of a course with the progress already joined on (CRS-002, CRS-003).
/// </summary>
/// <remarks>
/// This is a read model and not a repository: it exists because a course card needs one answer built
/// from three tables — the lessons, the media files that give them their length, and the watch state
/// that says how far in somebody got — and doing that with three round trips per course would be
/// three round trips per course.
/// <para>
/// The status is read and not recomputed. PLY-009 stores it, and a threshold change rewrites every
/// stored row precisely so that nothing downstream has to re-derive it and get a different answer.
/// </para>
/// </remarks>
public interface ICourseLessonReader
{
    Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
        CourseId courseId,
        CancellationToken cancellationToken = default);

    /// <summary>Every course's lessons in one pass, for the grid.</summary>
    Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>One course as the grid draws it (CRS-003).</summary>
public sealed record CourseCard(
    CourseId Id,
    string Title,
    string RelativePath,
    CourseSummary Summary,
    CourseThread Thread,
    DateTimeOffset? LastOpenedUtc);

/// <summary>A module and its lessons, in watching order.</summary>
public sealed record CourseModuleView(int Number, string? Title, IReadOnlyList<CourseLessonProgress> Lessons)
{
    public int WatchedLessons =>
        Lessons.Count(lesson => lesson.Status == Domain.Continuity.WatchStatus.Watched);
}

/// <summary>A course opened: its header, its modules, and its thread (CRS-002, CRS-003).</summary>
public sealed record CourseDetail(
    CourseId Id,
    string Title,
    string RelativePath,
    DateTimeOffset? LastOpenedUtc,
    CourseSummary Summary,
    CourseThread Thread,
    IReadOnlyList<CourseLessonProgress> Recap,
    IReadOnlyList<CourseModuleView> Modules,
    int ModuleCount,
    TimeSpan TotalDuration);

/// <summary>
/// The courses grid and one course's card, built from the store and the pure thread policy.
/// </summary>
public sealed class GetCourses
{
    private readonly ICourseRepository _courses;
    private readonly ICourseLessonReader _lessons;

    public GetCourses(ICourseRepository courses, ICourseLessonReader lessons)
    {
        _courses = courses ?? throw new ArgumentNullException(nameof(courses));
        _lessons = lessons ?? throw new ArgumentNullException(nameof(lessons));
    }

    public async Task<IReadOnlyList<CourseCard>> ListAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _courses.ListAsync(cancellationToken).ConfigureAwait(false);
        if (courses.Count == 0)
        {
            return [];
        }

        var byCourse = await _lessons.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        var cards = new List<CourseCard>(courses.Count);
        foreach (var course in courses)
        {
            // A course the reader knows nothing about is a folder marked and not yet walked, which is
            // a state the grid draws rather than one it hides.
            var lessons = byCourse.TryGetValue(course.Id, out var found) ? found : [];
            cards.Add(new CourseCard(
                course.Id,
                course.Title,
                course.RelativePath,
                CourseThreadPolicy.Summarise(lessons),
                CourseThreadPolicy.Resolve(lessons),
                course.LastOpenedAtUtc));
        }

        return cards;
    }

    public async Task<CourseDetail?> GetAsync(CourseId id, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (course is null)
        {
            return null;
        }

        var lessons = await _lessons.ReadAsync(id, cancellationToken).ConfigureAwait(false);
        var modules = lessons
            .GroupBy(lesson => (lesson.ModuleNumber, lesson.Module))
            .Select(group => new CourseModuleView(group.Key.ModuleNumber, group.Key.Module, [.. group]))
            .ToArray();

        return new CourseDetail(
            course.Id,
            course.Title,
            course.RelativePath,
            course.LastOpenedAtUtc,
            CourseThreadPolicy.Summarise(lessons),
            CourseThreadPolicy.Resolve(lessons),
            CourseThreadPolicy.Recap(lessons),
            modules,

            // The modules a person sees, which is not the same as the groups: lessons loose in the
            // course folder are grouped too, and a course with only those has no modules at all.
            modules.Count(module => module.Title is not null),
            lessons.Aggregate(TimeSpan.Zero, (total, lesson) => total + lesson.Duration));
    }
}
