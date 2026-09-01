// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Courses;

/// <summary>
/// Which lesson the countdown offers when one ends (CRS-004).
/// </summary>
public sealed class NextLessonPolicyTests
{
    [Fact]
    public void The_next_lesson_is_the_one_after_it_in_watching_order()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro"),
            Lesson(1, 2, "El nodo"),
            Lesson(2, 3, "Máscaras"),
        };

        var next = NextLessonPolicy.FindNext(lessons, lessons[0].Id);

        Assert.Equal("El nodo", next?.Title);
    }

    /// <summary>
    /// The chain crosses into the next module rather than stopping at the end of this one: a course
    /// is one thing somebody is studying, and its modules are how the folders were named.
    /// </summary>
    [Fact]
    public void The_chain_crosses_from_one_module_into_the_next()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro"),
            Lesson(1, 2, "El nodo"),
            Lesson(2, 3, "Máscaras"),
        };

        var next = NextLessonPolicy.FindNext(lessons, lessons[1].Id);

        Assert.Equal("Máscaras", next?.Title);
        Assert.Equal(2, next?.ModuleNumber);
    }

    /// <summary>«Curso terminado», which is what the overlay writes instead of a lesson.</summary>
    [Fact]
    public void The_last_lesson_of_the_course_has_nothing_after_it()
    {
        var lessons = new[] { Lesson(1, 1, "Intro"), Lesson(1, 2, "El nodo") };

        Assert.Null(NextLessonPolicy.FindNext(lessons, lessons[1].Id));
    }

    /// <summary>
    /// A lesson whose file the catalogue has not seen is stepped over rather than offered: the row
    /// refuses to play for the same reason, and a countdown that ran down to it would have spent ten
    /// seconds promising something it then failed at.
    /// </summary>
    [Fact]
    public void A_lesson_with_no_file_is_stepped_over()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro"),
            Lesson(1, 2, "El nodo", hasFile: false),
            Lesson(1, 3, "Máscaras"),
        };

        Assert.Equal("Máscaras", NextLessonPolicy.FindNext(lessons, lessons[0].Id)?.Title);
    }

    [Fact]
    public void A_course_whose_remaining_lessons_have_no_file_offers_nothing()
    {
        var lessons = new[] { Lesson(1, 1, "Intro"), Lesson(1, 2, "El nodo", hasFile: false) };

        Assert.Null(NextLessonPolicy.FindNext(lessons, lessons[0].Id));
    }

    /// <summary>
    /// A watched lesson is offered like any other. Skipping it would be the application deciding
    /// what somebody meant: the thread's «first unwatched» rule answers «where was I» when a course
    /// is opened, and this is the person sitting in front of the lesson that just ended.
    /// </summary>
    [Fact]
    public void A_lesson_already_watched_is_still_what_comes_next()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro"),
            Lesson(1, 2, "El nodo", status: WatchStatus.Watched),
        };

        Assert.Equal("El nodo", NextLessonPolicy.FindNext(lessons, lessons[0].Id)?.Title);
    }

    /// <summary>
    /// A lesson the course does not contain has no next, which is the state a file unmarked while it
    /// played arrives in.
    /// </summary>
    [Fact]
    public void A_lesson_that_is_not_in_the_course_has_nothing_after_it()
    {
        var lessons = new[] { Lesson(1, 1, "Intro") };

        Assert.Null(NextLessonPolicy.FindNext(lessons, new LessonId(Guid.NewGuid())));
    }

    [Fact]
    public void An_empty_course_offers_nothing()
    {
        Assert.Null(NextLessonPolicy.FindNext([], new LessonId(Guid.NewGuid())));
    }

    [Fact]
    public void The_lessons_are_required()
    {
        Assert.Throws<ArgumentNullException>(
            () => NextLessonPolicy.FindNext(null!, new LessonId(Guid.NewGuid())));
    }

    private static CourseLessonProgress Lesson(
        int module,
        int number,
        string title,
        bool hasFile = true,
        WatchStatus status = WatchStatus.NotStarted) => new(
        new LessonId(Guid.NewGuid()),
        hasFile ? new MediaFileId(Guid.NewGuid()) : null,
        module,
        "Módulo " + module,
        number,
        title,
        TimeSpan.FromMinutes(10),
        TimeSpan.Zero,
        status);
}
