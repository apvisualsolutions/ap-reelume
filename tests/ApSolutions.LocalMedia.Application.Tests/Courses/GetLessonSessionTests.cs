// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Courses;

/// <summary>
/// Whether the file that is playing is a lesson (CRS-004), and the course around it.
/// </summary>
/// <remarks>
/// Every refusal here ends the same way — no session, so the player's column is <b>absent</b> — and
/// that is exactly why each one needs its own test: absence is indistinguishable from absence, so a
/// path that refused for the wrong reason would look identical to one that refused for the right
/// one. The three are reached by taking a different thing away each time.
/// </remarks>
public sealed class GetLessonSessionTests
{
    [Fact]
    public async Task A_file_that_backs_a_lesson_comes_back_with_its_whole_course()
    {
        var store = new StubStore();
        var session = await new GetLessonSession(store, new GetCourses(store, store))
            .FindAsync(store.FileOf(1), TestContext.Current.CancellationToken);

        Assert.NotNull(session);
        Assert.Equal("Compositing", session!.Course.Title);
        Assert.Equal(store.LessonOf(1), session.LessonId);
        // Flattened back into watching order, which is what the countdown walks and what the panel
        // was built from: the two must not disagree about what «next» means.
        Assert.Equal(["Intro", "El nodo", "Máscaras"], session.Lessons.Select(lesson => lesson.Title));
    }

    [Fact]
    public async Task A_file_that_backs_no_lesson_is_no_session()
    {
        var store = new StubStore();

        Assert.Null(await new GetLessonSession(store, new GetCourses(store, store))
            .FindAsync(new MediaFileId(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The course unmarked between the two reads — the one moment the lesson row and the course row
    /// can disagree. Absent beats half a panel.
    /// </summary>
    [Fact]
    public async Task A_lesson_whose_course_has_gone_is_no_session()
    {
        var store = new StubStore { CourseIsGone = true };

        Assert.Null(await new GetLessonSession(store, new GetCourses(store, store))
            .FindAsync(store.FileOf(0), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The lesson exists and the course exists, but the progress join dropped the row: there is no
    /// panel to draw for a course that does not contain the thing being played.
    /// </summary>
    [Fact]
    public async Task A_lesson_the_course_does_not_contain_is_no_session()
    {
        var store = new StubStore { DropFirstLessonFromProgress = true };

        Assert.Null(await new GetLessonSession(store, new GetCourses(store, store))
            .FindAsync(store.FileOf(0), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Both_dependencies_are_required()
    {
        var store = new StubStore();

        Assert.Throws<ArgumentNullException>(
            () => new GetLessonSession(null!, new GetCourses(store, store)));
        Assert.Throws<ArgumentNullException>(() => new GetLessonSession(store, null!));
    }

    /// <summary>One course of three lessons, with a switch for each way the read can come apart.</summary>
    private sealed class StubStore : ICourseRepository, ICourseLessonReader
    {
        private readonly MediaFileId[] _files =
            [.. Enumerable.Range(0, 3).Select(_ => new MediaFileId(Guid.NewGuid()))];

        private readonly LessonId[] _lessons =
            [.. Enumerable.Range(0, 3).Select(_ => new LessonId(Guid.NewGuid()))];

        private readonly string[] _titles = ["Intro", "El nodo", "Máscaras"];

        public CourseId CourseId { get; } = new(Guid.NewGuid());

        public bool CourseIsGone { get; init; }

        public bool DropFirstLessonFromProgress { get; init; }

        public MediaFileId FileOf(int index) => _files[index];

        public LessonId LessonOf(int index) => _lessons[index];

        public Task<Course?> GetAsync(CourseId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Course?>(CourseIsGone
                ? null
                : new Course(CourseId, default, "Compositing", "Compositing", default, null));

        public Task<Lesson?> FindLessonByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default)
        {
            var index = Array.IndexOf(_files, fileId);
            return Task.FromResult<Lesson?>(index < 0
                ? null
                : new Lesson(
                    _lessons[index],
                    CourseId,
                    fileId,
                    "Fundamentos",
                    new LessonOrdinal(1, null),
                    new LessonOrdinal(index + 1, null),
                    $"{index + 1:D2} {_titles[index]}.mkv",
                    _titles[index],
                    $@"01 Fundamentos\{index + 1:D2} {_titles[index]}.mkv"));
        }

        public Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default)
        {
            var rows = Enumerable.Range(0, 3)
                .Where(index => !DropFirstLessonFromProgress || index != 0)
                .Select(index => new CourseLessonProgress(
                    _lessons[index],
                    _files[index],
                    1,
                    "Fundamentos",
                    index + 1,
                    _titles[index],
                    TimeSpan.FromMinutes(10),
                    TimeSpan.Zero,
                    WatchStatus.NotStarted));
            return Task.FromResult<IReadOnlyList<CourseLessonProgress>>([.. rows]);
        }

        public Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Course>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CourseId> SaveAsync(
            Course course,
            IReadOnlyList<Lesson> lessons,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Lesson>> ListLessonsAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveAsync(CourseId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task TouchAsync(
            CourseId id,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
