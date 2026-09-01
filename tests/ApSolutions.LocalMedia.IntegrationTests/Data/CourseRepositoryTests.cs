// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

/// <summary>
/// Courses and lessons against the real store (CRS-001, CRS-005, migration 0022).
/// </summary>
[Trait("Category", "Integration")]
public sealed class CourseRepositoryTests
{
    [Fact]
    public async Task A_course_comes_back_with_its_lessons_in_the_order_they_are_watched()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;
        var id = new CourseId(Guid.NewGuid());

        await repository.SaveAsync(
            Course(id, rootId, "Composición"),
            [
                Lesson("Módulo uno", new LessonOrdinal(1, null), "10 - Décima", new LessonOrdinal(10, null)),
                Lesson("Módulo uno", new LessonOrdinal(1, null), "2 - Segunda", new LessonOrdinal(2, null)),
                Lesson("Módulo uno", new LessonOrdinal(1, null), "ES_014_02", null),
                Lesson("Módulo dos", new LessonOrdinal(2, null), "1 - Primera", new LessonOrdinal(1, null)),
            ],
            TestContext.Current.CancellationToken);

        var lessons = await repository.ListLessonsAsync(id, TestContext.Current.CancellationToken);

        // Numbered by their number and not alphabetically, then what carries no number, last.
        Assert.Equal(
            ["2 - Segunda", "10 - Décima", "ES_014_02", "1 - Primera"],
            lessons.Select(lesson => lesson.Name));
        Assert.Equal(id, lessons[0].CourseId);
    }

    /// <summary>
    /// SQLite sorts NULL first, so an unnumbered lesson would open every course if the order did not
    /// say otherwise. This is the assertion that would have caught it.
    /// </summary>
    [Fact]
    public async Task What_carries_no_number_never_opens_a_course()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;
        var id = new CourseId(Guid.NewGuid());

        await repository.SaveAsync(
            Course(id, rootId, "Curso"),
            [
                Lesson(null, null, "Bonus", null),
                Lesson(null, null, "01 - Intro", new LessonOrdinal(1, null)),
            ],
            TestContext.Current.CancellationToken);

        var lessons = await repository.ListLessonsAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(["01 - Intro", "Bonus"], lessons.Select(lesson => lesson.Name));
    }

    [Fact]
    public async Task A_hierarchical_number_survives_the_round_trip()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;
        var id = new CourseId(Guid.NewGuid());

        await repository.SaveAsync(
            Course(id, rootId, "Curso"),
            [Lesson("Módulo", new LessonOrdinal(3, 2), "1.3 Alfa", new LessonOrdinal(1, 3))],
            TestContext.Current.CancellationToken);

        var lesson = Assert.Single(await repository.ListLessonsAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(new LessonOrdinal(1, 3), lesson.Ordinal);
        Assert.Equal(new LessonOrdinal(3, 2), lesson.ModuleOrdinal);
    }

    /// <summary>
    /// Marking a folder twice is the same course re-read: the second save keeps the identifier the
    /// first one settled on, takes the newer title, and replaces the lessons rather than doubling
    /// them — a lesson deleted from the disk has to be gone from the list.
    /// </summary>
    [Fact]
    public async Task Saving_the_same_folder_twice_keeps_one_course_and_replaces_its_lessons()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;

        var first = await repository.SaveAsync(
            Course(new CourseId(Guid.NewGuid()), rootId, "Composición"),
            [Lesson(null, null, "01 - Intro", new LessonOrdinal(1, null))],
            TestContext.Current.CancellationToken);

        var second = await repository.SaveAsync(
            Course(new CourseId(Guid.NewGuid()), rootId, "Composición renombrada", "Composición"),
            [Lesson(null, null, "01 - Otra", new LessonOrdinal(1, null))],
            TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        Assert.Single(await repository.ListAsync(TestContext.Current.CancellationToken));
        var stored = await repository.GetAsync(first, TestContext.Current.CancellationToken);
        Assert.Equal("Composición renombrada", stored!.Title);
        var lesson = Assert.Single(await repository.ListLessonsAsync(first, TestContext.Current.CancellationToken));
        Assert.Equal("01 - Otra", lesson.Name);
    }

    /// <summary>
    /// The same folder under two different roots is two courses. The uniqueness is per root, because
    /// two libraries may legitimately hold a folder of the same name.
    /// </summary>
    [Fact]
    public async Task The_same_folder_under_two_roots_is_two_courses()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var first = harness.RootId;
        var second = await harness.AddRootAsync(@"E:\Otra");

        await repository.SaveAsync(
            Course(new CourseId(Guid.NewGuid()), first, "Composición"),
            [],
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            Course(new CourseId(Guid.NewGuid()), second, "Composición"),
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, (await repository.ListAsync(TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Unmarking_a_course_takes_its_lessons_with_it()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;
        var id = new CourseId(Guid.NewGuid());
        await repository.SaveAsync(
            Course(id, rootId, "Curso"),
            [Lesson(null, null, "01 - Intro", new LessonOrdinal(1, null))],
            TestContext.Current.CancellationToken);

        await repository.RemoveAsync(id, TestContext.Current.CancellationToken);

        Assert.Empty(await repository.ListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await repository.ListLessonsAsync(id, TestContext.Current.CancellationToken));
        Assert.Null(await repository.GetAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Opening_a_course_is_remembered_and_starts_unremembered()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;
        var id = new CourseId(Guid.NewGuid());
        await repository.SaveAsync(Course(id, rootId, "Curso"), [], TestContext.Current.CancellationToken);

        Assert.Null((await repository.GetAsync(id, TestContext.Current.CancellationToken))!.LastOpenedAtUtc);

        var when = new DateTimeOffset(2026, 8, 30, 18, 30, 0, TimeSpan.Zero);
        await repository.TouchAsync(id, when, TestContext.Current.CancellationToken);

        Assert.Equal(when, (await repository.GetAsync(id, TestContext.Current.CancellationToken))!.LastOpenedAtUtc);
    }

    /// <summary>
    /// One nullable column carries both of ADR-0006's answers: a root holds courses exactly when it
    /// has a depth, and undeclaring is setting it back to nothing.
    /// </summary>
    [Fact]
    public async Task A_root_declares_its_course_depth_and_can_take_it_back()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;

        Assert.Null(await repository.GetCourseDepthAsync(rootId, TestContext.Current.CancellationToken));

        await repository.DeclareAsync(rootId, 2, TestContext.Current.CancellationToken);
        Assert.Equal(2, await repository.GetCourseDepthAsync(rootId, TestContext.Current.CancellationToken));

        await repository.DeclareAsync(rootId, null, TestContext.Current.CancellationToken);
        Assert.Null(await repository.GetCourseDepthAsync(rootId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task There_is_no_depth_zero_and_no_depth_for_a_root_that_is_not_there()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.DeclareAsync(new LibraryRootId(Guid.NewGuid()), 0, TestContext.Current.CancellationToken));
        Assert.Null(await repository.GetCourseDepthAsync(
            new LibraryRootId(Guid.NewGuid()),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_lesson_the_catalogue_knows_carries_its_file_identity()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;
        var mediaFileId = new MediaFileId(Guid.NewGuid());
        await new MediaFileRepository(harness.Factory).UpsertAsync(
            new MediaFile(
                mediaFileId,
                rootId,
                @"D:\Cursos\Curso\01 - Intro.mp4",
                1,
                DateTimeOffset.UnixEpoch,
                new TechnicalMetadata(null, "mp4", [], [], null, null)),
            TestContext.Current.CancellationToken);

        var id = new CourseId(Guid.NewGuid());
        await repository.SaveAsync(
            Course(id, rootId, "Curso"),
            [Lesson(null, null, "01 - Intro", new LessonOrdinal(1, null)) with { MediaFileId = mediaFileId }],
            TestContext.Current.CancellationToken);

        var lesson = Assert.Single(await repository.ListLessonsAsync(id, TestContext.Current.CancellationToken));
        Assert.Equal(mediaFileId, lesson.MediaFileId);
    }

    [Fact]
    public void A_repository_needs_a_connection_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new CourseRepository(null!));
    }

    [Fact]
    public async Task Saving_needs_a_course_and_a_list_of_lessons()
    {
        using var harness = await Harness.OpenAsync();
        var repository = harness.Repository;
        var rootId = harness.RootId;

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.SaveAsync(null!, [], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.SaveAsync(
                Course(new CourseId(Guid.NewGuid()), rootId, "Curso"),
                null!,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The folder and the title are two things, and the test that renames one has to be able to keep
    /// the other: the upsert keys on the folder, so tying them together would have hidden that.
    /// </summary>
    private static Course Course(
        CourseId id,
        LibraryRootId rootId,
        string title,
        string? relativePath = null) => new(
        id,
        rootId,
        relativePath ?? title,
        title,
        new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero),
        LastOpenedAtUtc: null);

    /// <summary>
    /// The lesson a file backs (CRS-004), which is how a playing session learns it is a lesson at
    /// all — and the query <c>ix_lessons_media_file</c> had been waiting for since migration 0022.
    /// </summary>
    [Fact]
    public async Task The_lesson_a_file_backs_is_found_by_that_file()
    {
        using var harness = await Harness.OpenAsync();
        var files = new MediaFileRepository(harness.Factory);
        var wanted = new MediaFileId(Guid.NewGuid());
        var other = new MediaFileId(Guid.NewGuid());
        foreach (var (id, name) in new[] { (wanted, "El nodo"), (other, "Máscaras") })
        {
            await files.UpsertAsync(
                new MediaFile(
                    id,
                    harness.RootId,
                    $@"D:\Cursos\Composición\{name}.mp4",
                    1,
                    DateTimeOffset.UnixEpoch,
                    new TechnicalMetadata(TimeSpan.FromMinutes(10), "mp4", [], [], null, null)),
                TestContext.Current.CancellationToken);
        }

        var courseId = new CourseId(Guid.NewGuid());
        await harness.Repository.SaveAsync(
            Course(courseId, harness.RootId, "Composición"),
            [
                Lesson("Módulo uno", new LessonOrdinal(1, null), "1 - El nodo", new LessonOrdinal(1, null))
                    with
                { MediaFileId = wanted },
                Lesson("Módulo uno", new LessonOrdinal(1, null), "2 - Máscaras", new LessonOrdinal(2, null))
                    with
                { MediaFileId = other },
                Lesson("Módulo uno", new LessonOrdinal(1, null), "3 - Sin archivo", new LessonOrdinal(3, null)),
            ],
            TestContext.Current.CancellationToken);

        var found = await harness.Repository.FindLessonByFileAsync(wanted, TestContext.Current.CancellationToken);

        Assert.Equal("1 - El nodo", found?.Name);
        // The course identifier is what the panel is built from, and the save assigns it: asking for
        // it back is the whole point of returning the lesson rather than a boolean.
        Assert.Equal(courseId, found?.CourseId);
        Assert.Equal(wanted, found?.MediaFileId);
    }

    /// <summary>
    /// A file that backs nothing is not a lesson, which is the answer that makes the player's column
    /// <b>absent</b> rather than empty for every film in the library.
    /// </summary>
    [Fact]
    public async Task A_file_that_backs_no_lesson_is_not_one()
    {
        using var harness = await Harness.OpenAsync();

        var found = await harness.Repository.FindLessonByFileAsync(
            new MediaFileId(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Null(found);
    }

    private static Lesson Lesson(string? module, LessonOrdinal? moduleOrdinal, string name, LessonOrdinal? ordinal) =>
        new(
            new LessonId(Guid.NewGuid()),
            CourseId: default,
            MediaFileId: null,
            module,
            moduleOrdinal,
            ordinal,
            name,
            name,
            $"{module ?? "."}/{name}.mp4");

    /// <summary>A migrated database with one library root, and the repository over it.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly DatabaseTestDirectory _directory = new();

        private Harness()
        {
            Factory = new SqliteConnectionFactory(_directory.DatabasePath);
            Repository = new CourseRepository(Factory);
        }

        public SqliteConnectionFactory Factory { get; }

        public CourseRepository Repository { get; }

        public LibraryRootId RootId { get; private set; }

        public static async Task<Harness> OpenAsync()
        {
            var harness = new Harness();
            using (var runner = new MigrationRunner(harness.Factory))
            {
                await runner.MigrateAsync(CancellationToken.None);
            }

            harness.RootId = await harness.AddRootAsync(@"D:\Cursos");
            return harness;
        }

        public async Task<LibraryRootId> AddRootAsync(string path)
        {
            var rootId = new LibraryRootId(Guid.NewGuid());
            await new LibraryRootRepository(Factory).AddAsync(
                new LibraryRoot(rootId, path, RootKind.Local, RootAvailability.Available, ScanPolicy.Manual),
                TestContext.Current.CancellationToken);
            return rootId;
        }

        public void Dispose() => _directory.Dispose();
    }
}
