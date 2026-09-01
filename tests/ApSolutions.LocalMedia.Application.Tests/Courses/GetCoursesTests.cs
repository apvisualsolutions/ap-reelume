// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Courses;

/// <summary>The courses grid and one opened course (CRS-002, CRS-003).</summary>
public sealed class GetCoursesTests
{
    private static readonly LibraryRootId RootId = new(Guid.NewGuid());

    [Fact]
    public async Task The_grid_carries_each_course_with_its_progress_and_its_thread()
    {
        var id = new CourseId(Guid.NewGuid());
        var courses = new StubCourses { Courses = { Course(id, "Composición") } };
        var lessons = new StubLessons
        {
            ByCourse =
            {
                [id] =
                [
                    Lesson(1, 1, "Intro", WatchStatus.Watched),
                    Lesson(1, 2, "El nodo", WatchStatus.InProgress, TimeSpan.FromMinutes(4)),
                ],
            },
        };

        var card = Assert.Single(await new GetCourses(courses, lessons)
            .ListAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Composición", card.Title);
        Assert.Equal(1, card.Summary.WatchedLessons);
        Assert.Equal(2, card.Summary.TotalLessons);
        Assert.Equal(TimeSpan.FromMinutes(6), card.Summary.Remaining);
        Assert.Equal("El nodo", card.Thread.LessonTitle);
        Assert.True(card.Thread.IsPartial);
    }

    /// <summary>
    /// A folder marked whose walk has not run yet has no lessons, and the grid draws it rather than
    /// hiding it — that is the «Se abrirá al escanear» state of the prototype.
    /// </summary>
    [Fact]
    public async Task A_course_the_reader_knows_nothing_about_is_still_a_card()
    {
        var courses = new StubCourses { Courses = { Course(new CourseId(Guid.NewGuid()), "Recién marcada") } };

        var card = Assert.Single(await new GetCourses(courses, new StubLessons())
            .ListAsync(TestContext.Current.CancellationToken));

        Assert.True(card.Summary.IsEmpty);
        Assert.False(card.Summary.IsFinished);
    }

    [Fact]
    public async Task No_courses_reads_nothing_at_all()
    {
        var lessons = new StubLessons();

        Assert.Empty(await new GetCourses(new StubCourses(), lessons)
            .ListAsync(TestContext.Current.CancellationToken));
        Assert.False(lessons.WasRead);
    }

    [Fact]
    public async Task An_opened_course_groups_its_lessons_into_modules()
    {
        var id = new CourseId(Guid.NewGuid());
        var courses = new StubCourses { Courses = { Course(id, "Composición") } };
        var lessons = new StubLessons
        {
            ByCourse =
            {
                [id] =
                [
                    Lesson(1, 1, "Intro", WatchStatus.Watched, module: "Módulo uno"),
                    Lesson(1, 2, "El nodo", WatchStatus.Watched, module: "Módulo uno"),
                    Lesson(2, 3, "Máscaras", WatchStatus.NotStarted, module: "Módulo dos"),
                ],
            },
        };

        var detail = await new GetCourses(courses, lessons)
            .GetAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Equal(["Módulo uno", "Módulo dos"], detail.Modules.Select(module => module.Title));
        Assert.Equal([2, 1], detail.Modules.Select(module => module.Lessons.Count));
        Assert.Equal([2, 0], detail.Modules.Select(module => module.WatchedLessons));
        Assert.Equal(2, detail.ModuleCount);
        Assert.Equal(TimeSpan.FromMinutes(30), detail.TotalDuration);
        Assert.Equal("Máscaras", detail.Thread.LessonTitle);
        Assert.Equal(["El nodo", "Intro"], detail.Recap.Select(lesson => lesson.Title));
    }

    /// <summary>
    /// Lessons loose in the course folder are grouped like any others, but they are not a module:
    /// a course with only those has none, and drawing «Módulo 1» over them would invent one.
    /// </summary>
    [Fact]
    public async Task Loose_lessons_are_grouped_without_becoming_a_module()
    {
        var id = new CourseId(Guid.NewGuid());
        var courses = new StubCourses { Courses = { Course(id, "Tutorial") } };
        var lessons = new StubLessons
        {
            ByCourse = { [id] = [Lesson(0, 1, "Uno", WatchStatus.NotStarted, module: null)] },
        };

        var detail = await new GetCourses(courses, lessons)
            .GetAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Single(detail.Modules);
        Assert.Null(detail.Modules[0].Title);
        Assert.Equal(0, detail.ModuleCount);
    }

    [Fact]
    public async Task A_course_that_is_not_there_reads_as_nothing()
    {
        var detail = await new GetCourses(new StubCourses(), new StubLessons())
            .GetAsync(new CourseId(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Null(detail);
    }

    [Fact]
    public void Reading_courses_needs_a_store_and_a_reader()
    {
        Assert.Throws<ArgumentNullException>(() => new GetCourses(null!, new StubLessons()));
        Assert.Throws<ArgumentNullException>(() => new GetCourses(new StubCourses(), null!));
    }

    private static Course Course(CourseId id, string title) => new(
        id,
        RootId,
        title,
        title,
        DateTimeOffset.UnixEpoch,
        LastOpenedAtUtc: null);

    private static CourseLessonProgress Lesson(
        int moduleNumber,
        int number,
        string title,
        WatchStatus status,
        TimeSpan? position = null,
        string? module = "Módulo") => new(
        new LessonId(Guid.NewGuid()),
        new MediaFileId(Guid.NewGuid()),
        moduleNumber,
        module,
        number,
        title,
        TimeSpan.FromMinutes(10),
        position ?? TimeSpan.Zero,
        status);

    private sealed class StubCourses : ICourseRepository
    {
        public List<Course> Courses { get; } = [];

        public Task<IReadOnlyList<Course>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Course>>(Courses);

        public Task<Course?> GetAsync(CourseId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Courses.FirstOrDefault(course => course.Id == id));

        public Task<CourseId> SaveAsync(
            Course course,
            IReadOnlyList<Lesson> lessons,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Lesson>> ListLessonsAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Lesson?> FindLessonByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveAsync(CourseId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task TouchAsync(
            CourseId id,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubLessons : ICourseLessonReader
    {
        public Dictionary<CourseId, IReadOnlyList<CourseLessonProgress>> ByCourse { get; } = [];

        public bool WasRead { get; private set; }

        public Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return Task.FromResult(ByCourse.TryGetValue(courseId, out var lessons) ? lessons : []);
        }

        public Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
            CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return Task.FromResult<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>>(ByCourse);
        }
    }
}
