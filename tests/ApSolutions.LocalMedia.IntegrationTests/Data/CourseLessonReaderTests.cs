// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

/// <summary>
/// The join that answers a course card (CRS-002, CRS-003), against the real store.
/// </summary>
/// <remarks>
/// The point of measuring this rather than reasoning about it is the key: the reader finds progress
/// by composing <c>'title:' || course_id || '/episode:' || lesson_id</c> in SQL, and PLY-008 writes
/// that same text from <see cref="ContentKey"/> in C#. Two places compose one string, so a test has
/// to write through one and read through the other.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class CourseLessonReaderTests
{
    [Fact]
    public async Task Progress_written_by_the_continuity_store_is_found_by_the_reader()
    {
        using var world = await World.OpenAsync();
        var lesson = world.Lessons[0];

        await world.WatchState.SaveAsync(
            new WatchState
            {
                Content = CourseProgressKey.For(world.CourseId, lesson.Id),
                Position = TimeSpan.FromMinutes(4),
                ObservedDuration = TimeSpan.FromMinutes(10),
                SourceMediaFileId = lesson.MediaFileId!.Value,
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = DateTimeOffset.UnixEpoch,
                UpdatedUtc = DateTimeOffset.UnixEpoch,
            },
            TestContext.Current.CancellationToken);

        var read = await world.Reader.ReadAsync(world.CourseId, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(4), read[0].Position);
        Assert.Equal(WatchStatus.InProgress, read[0].Status);
        Assert.Equal(WatchStatus.NotStarted, read[1].Status);
        Assert.Equal(TimeSpan.Zero, read[1].Position);
    }

    /// <summary>
    /// The length is the catalogue's and is joined, not copied: a lesson whose file the scan has not
    /// probed has no length yet, and that is a state rather than a failure.
    /// </summary>
    [Fact]
    public async Task The_length_comes_from_the_media_file_and_is_absent_when_it_has_none()
    {
        using var world = await World.OpenAsync();

        var read = await world.Reader.ReadAsync(world.CourseId, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(10), read[0].Duration);
        Assert.Equal(TimeSpan.Zero, read[2].Duration);
    }

    /// <summary>
    /// The number counts through the course and not through the module, because the prototype writes
    /// «L06» beside a lesson in module 2 and a per-module number would name two lessons the same.
    /// </summary>
    [Fact]
    public async Task Lessons_come_back_in_watching_order_numbered_through_the_whole_course()
    {
        using var world = await World.OpenAsync();

        var read = await world.Reader.ReadAsync(world.CourseId, TestContext.Current.CancellationToken);

        Assert.Equal(["Intro", "El nodo", "Máscaras"], read.Select(lesson => lesson.Title));
        Assert.Equal([1, 2, 3], read.Select(lesson => lesson.Number));
        Assert.Equal([1, 1, 2], read.Select(lesson => lesson.ModuleNumber));
        Assert.Equal(["Módulo uno", "Módulo uno", "Módulo dos"], read.Select(lesson => lesson.Module));
    }

    [Fact]
    public async Task Every_course_reads_in_one_pass_and_each_numbers_from_one()
    {
        using var world = await World.OpenAsync();
        var second = await world.AddCourseAsync("Otro", [("Módulo", 1, "Única")]);

        var all = await world.Reader.ReadAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
        Assert.Equal([1, 2, 3], all[world.CourseId].Select(lesson => lesson.Number));
        Assert.Equal([1], all[second].Select(lesson => lesson.Number));
    }

    [Fact]
    public async Task A_course_with_no_lessons_is_not_in_the_pass_at_all()
    {
        using var world = await World.OpenAsync();
        var empty = await world.AddCourseAsync("Vacío", []);

        var all = await world.Reader.ReadAllAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(empty, all.Keys);
        Assert.Empty(await world.Reader.ReadAsync(empty, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A flat course — lessons with no section at all — is the only shape where the reader's two
    /// module columns arrive null, and it is not a corner case: five of the twelve real courses
    /// measured on 2026-08-30 are flat. Without this the loose arm of both `IsDBNull` reads was
    /// never taken, which is exactly the two branches that held the file at 88.
    /// </summary>
    [Fact]
    public async Task A_course_with_no_sections_reads_its_lessons_as_loose()
    {
        using var world = await World.OpenAsync();
        var flat = await world.AddLooseCourseAsync("Plano", ["Una", "Dos"]);

        var lessons = await world.Reader.ReadAsync(flat, TestContext.Current.CancellationToken);

        Assert.Equal(2, lessons.Count);
        Assert.All(lessons, lesson => Assert.Null(lesson.Module));
        // 0 is the reader's own LooseModuleNumber: a lesson with no section still needs a module
        // number to sort by, and zero puts it before every real section rather than after.
        Assert.All(lessons, lesson => Assert.Equal(0, lesson.ModuleNumber));
        Assert.Equal([1, 2], lessons.Select(lesson => lesson.Number));
    }

    [Fact]
    public void A_reader_needs_a_connection_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new CourseLessonReader(null!));
    }

    /// <summary>A migrated database with one root and one three-lesson course over two modules.</summary>
    private sealed class World : IDisposable
    {
        private readonly DatabaseTestDirectory _directory = new();

        private World()
        {
            Factory = new SqliteConnectionFactory(_directory.DatabasePath);
            Reader = new CourseLessonReader(Factory);
            Courses = new CourseRepository(Factory);
            WatchState = new WatchStateRepository(Factory);
        }

        public SqliteConnectionFactory Factory { get; }

        public CourseLessonReader Reader { get; }

        public CourseRepository Courses { get; }

        public WatchStateRepository WatchState { get; }

        public LibraryRootId RootId { get; private set; }

        public CourseId CourseId { get; private set; }

        public IReadOnlyList<Lesson> Lessons { get; private set; } = [];

        public static async Task<World> OpenAsync()
        {
            var world = new World();
            using (var runner = new MigrationRunner(world.Factory))
            {
                await runner.MigrateAsync(CancellationToken.None);
            }

            world.RootId = new LibraryRootId(Guid.NewGuid());
            await new LibraryRootRepository(world.Factory).AddAsync(
                new LibraryRoot(world.RootId, @"D:\Cursos", RootKind.Local, RootAvailability.Available, ScanPolicy.Manual),
                TestContext.Current.CancellationToken);

            world.CourseId = await world.AddCourseAsync(
                "Composición",
                [("Módulo uno", 1, "Intro"), ("Módulo uno", 1, "El nodo"), ("Módulo dos", 2, "Máscaras")],
                probedLessons: 2);
            world.Lessons = await world.Courses.ListLessonsAsync(
                world.CourseId,
                TestContext.Current.CancellationToken);
            return world;
        }

        /// <summary>
        /// Writes a course whose first <paramref name="probedLessons"/> lessons have a catalogued
        /// media file with a length, and whose rest have none.
        /// </summary>
        public async Task<CourseId> AddCourseAsync(
            string title,
            (string Module, int ModuleNumber, string Title)[] lessons,
            int probedLessons = int.MaxValue)
        {
            var id = new CourseId(Guid.NewGuid());
            var files = new MediaFileRepository(Factory);
            var rows = new List<Lesson>();
            for (var index = 0; index < lessons.Length; index++)
            {
                var (module, moduleNumber, lessonTitle) = lessons[index];
                MediaFileId? mediaFileId = null;
                if (index < probedLessons)
                {
                    mediaFileId = new MediaFileId(Guid.NewGuid());
                    await files.UpsertAsync(
                        new MediaFile(
                            mediaFileId.Value,
                            RootId,
                            $@"D:\Cursos\{title}\{module}\{index} - {lessonTitle}.mp4",
                            1,
                            DateTimeOffset.UnixEpoch,
                            new TechnicalMetadata(TimeSpan.FromMinutes(10), "mp4", [], [], null, null)),
                        TestContext.Current.CancellationToken);
                }

                rows.Add(new Lesson(
                    new LessonId(Guid.NewGuid()),
                    CourseId: default,
                    mediaFileId,
                    module,
                    new LessonOrdinal(moduleNumber, null),
                    new LessonOrdinal(index + 1, null),
                    $"{index + 1} - {lessonTitle}",
                    lessonTitle,
                    $"{title}/{module}/{index + 1} - {lessonTitle}.mp4"));
            }

            await Courses.SaveAsync(
                new Course(id, RootId, title, title, DateTimeOffset.UnixEpoch, null),
                rows,
                TestContext.Current.CancellationToken);
            return id;
        }

        /// <summary>A course whose lessons hang straight off it, with no section folder.</summary>
        public async Task<CourseId> AddLooseCourseAsync(string title, string[] lessons)
        {
            var id = new CourseId(Guid.NewGuid());
            var rows = new List<Lesson>();
            for (var index = 0; index < lessons.Length; index++)
            {
                rows.Add(new Lesson(
                    new LessonId(Guid.NewGuid()),
                    CourseId: default,
                    MediaFileId: null,
                    Module: null,
                    ModuleOrdinal: null,
                    new LessonOrdinal(index + 1, null),
                    $"{index + 1} - {lessons[index]}",
                    lessons[index],
                    $"{title}/{index + 1} - {lessons[index]}.mp4"));
            }

            await Courses.SaveAsync(
                new Course(id, RootId, title, title, DateTimeOffset.UnixEpoch, null),
                rows,
                TestContext.Current.CancellationToken);
            return id;
        }

        public void Dispose() => _directory.Dispose();
    }
}
