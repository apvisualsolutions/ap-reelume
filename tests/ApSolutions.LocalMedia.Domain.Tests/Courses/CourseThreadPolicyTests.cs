// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Courses;

/// <summary>
/// The thread a course keeps for itself (CRS-002), and the summary a card draws (CRS-003).
/// </summary>
public sealed class CourseThreadPolicyTests
{
    /// <summary>
    /// The first unwatched lesson in watching order, which is not the same as the last one played:
    /// somebody who jumped ahead has left everything in between, and the thread has to say so.
    /// </summary>
    [Fact]
    public void The_thread_points_at_the_first_lesson_that_is_not_watched()
    {
        var thread = CourseThreadPolicy.Resolve(
        [
            Lesson(1, 1, "Intro", WatchStatus.Watched),
            Lesson(1, 2, "El nodo", WatchStatus.NotStarted),
            Lesson(2, 3, "Máscaras", WatchStatus.Watched),
        ]);

        Assert.Equal("El nodo", thread.LessonTitle);
        Assert.Equal(2, thread.LessonNumber);
        Assert.Equal(1, thread.ModuleNumber);
        Assert.False(thread.IsPartial);
        Assert.False(thread.IsCourseFinished);
    }

    [Fact]
    public void A_lesson_left_part_way_through_is_the_thread_and_says_so()
    {
        var thread = CourseThreadPolicy.Resolve(
        [
            Lesson(1, 1, "Intro", WatchStatus.Watched),
            Lesson(1, 2, "El nodo", WatchStatus.InProgress, position: TimeSpan.FromMinutes(4)),
        ]);

        Assert.True(thread.IsPartial);
        Assert.Equal(TimeSpan.FromMinutes(4), thread.Position);
        Assert.Equal(TimeSpan.FromMinutes(10), thread.Duration);
    }

    [Fact]
    public void A_course_with_everything_watched_has_no_thread_left()
    {
        var thread = CourseThreadPolicy.Resolve(
            [Lesson(1, 1, "Intro", WatchStatus.Watched), Lesson(1, 2, "Fin", WatchStatus.Watched)]);

        Assert.True(thread.IsCourseFinished);
        Assert.Null(thread.Lesson);
        Assert.Same(CourseThread.Finished, CourseThread.Finished);
    }

    /// <summary>
    /// A folder just marked, whose walk has not run, is not a finished course — and drawing it as one
    /// would congratulate somebody for a course nobody has read.
    /// </summary>
    [Fact]
    public void A_course_with_no_lessons_is_empty_and_not_finished()
    {
        var summary = CourseThreadPolicy.Summarise([]);

        Assert.True(summary.IsEmpty);
        Assert.False(summary.IsFinished);
        Assert.Equal(0, summary.TotalLessons);
        Assert.True(CourseThreadPolicy.Resolve([]).IsCourseFinished);
    }

    /// <summary>
    /// What is left counts the whole of an unwatched lesson and only the rest of one in progress:
    /// forty minutes into an hour leaves twenty, not sixty.
    /// </summary>
    [Fact]
    public void What_remains_counts_the_rest_of_the_lesson_in_progress_and_not_all_of_it()
    {
        var summary = CourseThreadPolicy.Summarise(
        [
            Lesson(1, 1, "Vista", WatchStatus.Watched),
            Lesson(1, 2, "A medias", WatchStatus.InProgress, position: TimeSpan.FromMinutes(4)),
            Lesson(1, 3, "Sin empezar", WatchStatus.NotStarted),
        ]);

        Assert.Equal(1, summary.WatchedLessons);
        Assert.Equal(3, summary.TotalLessons);
        Assert.Equal(TimeSpan.FromMinutes(16), summary.Remaining);
        Assert.False(summary.IsFinished);
    }

    /// <summary>
    /// A position past the end — which a re-probed shorter version can produce — must not subtract
    /// time from what is left. The lesson counts whole, which is the safe direction to be wrong in.
    /// </summary>
    [Fact]
    public void A_position_past_the_end_never_makes_what_remains_smaller()
    {
        var summary = CourseThreadPolicy.Summarise(
            [Lesson(1, 1, "Rara", WatchStatus.InProgress, position: TimeSpan.FromMinutes(30))]);

        Assert.Equal(TimeSpan.FromMinutes(10), summary.Remaining);
    }

    [Fact]
    public void Everything_watched_is_finished()
    {
        var summary = CourseThreadPolicy.Summarise(
            [Lesson(1, 1, "Una", WatchStatus.Watched), Lesson(1, 2, "Dos", WatchStatus.Watched)]);

        Assert.True(summary.IsFinished);
        Assert.Equal(TimeSpan.Zero, summary.Remaining);
    }

    /// <summary>
    /// The recap is what was watched <i>before</i> the thread, newest first — not the newest in the
    /// whole course, or a lesson somebody skipped ahead to would answer «¿de qué iba?» with
    /// something they have not reached.
    /// </summary>
    [Fact]
    public void The_recap_is_the_last_two_watched_before_the_thread_newest_first()
    {
        var recap = CourseThreadPolicy.Recap(
        [
            Lesson(1, 1, "Una", WatchStatus.Watched),
            Lesson(1, 2, "Dos", WatchStatus.Watched),
            Lesson(1, 3, "Tres", WatchStatus.Watched),
            Lesson(2, 4, "Aquí", WatchStatus.NotStarted),
            Lesson(2, 5, "Adelantada", WatchStatus.Watched),
        ]);

        Assert.Equal(["Tres", "Dos"], recap.Select(lesson => lesson.Title));
    }

    [Fact]
    public void A_finished_course_recaps_its_own_ending()
    {
        var recap = CourseThreadPolicy.Recap(
        [
            Lesson(1, 1, "Una", WatchStatus.Watched),
            Lesson(1, 2, "Dos", WatchStatus.Watched),
            Lesson(1, 3, "Tres", WatchStatus.Watched),
        ]);

        Assert.Equal(["Tres", "Dos"], recap.Select(lesson => lesson.Title));
    }

    [Fact]
    public void Nothing_watched_yet_recaps_nothing()
    {
        var recap = CourseThreadPolicy.Recap([Lesson(1, 1, "Una", WatchStatus.NotStarted)]);

        Assert.Empty(recap);
    }

    [Fact]
    public void There_is_nothing_to_read_from_nothing()
    {
        Assert.Throws<ArgumentNullException>(() => CourseThreadPolicy.Resolve(null!));
        Assert.Throws<ArgumentNullException>(() => CourseThreadPolicy.Summarise(null!));
        Assert.Throws<ArgumentNullException>(() => CourseThreadPolicy.Recap(null!));
    }

    /// <summary>
    /// The key is PLY-008's, in the shape it already has: course where a title goes, lesson where an
    /// episode goes. A film's key has no episode part and is not a lesson's.
    /// </summary>
    [Fact]
    public void A_lesson_stores_its_progress_under_the_key_the_store_already_has()
    {
        var course = new CourseId(Guid.NewGuid());
        var lesson = new LessonId(Guid.NewGuid());

        var key = CourseProgressKey.For(course, lesson);

        Assert.Equal(course.Value, key.TitleId.Value);
        Assert.Equal(lesson.Value, key.EpisodeId!.Value.Value);
        Assert.Equal((course, lesson), CourseProgressKey.Read(key));
        Assert.Null(CourseProgressKey.Read(ContentKey.ForTitle(new TitleId(Guid.NewGuid()))));
    }

    private static CourseLessonProgress Lesson(
        int module,
        int number,
        string title,
        WatchStatus status,
        TimeSpan? position = null) => new(
        new LessonId(Guid.NewGuid()),
        module,
        "Módulo " + module,
        number,
        title,
        TimeSpan.FromMinutes(10),
        position ?? TimeSpan.Zero,
        status);
}
