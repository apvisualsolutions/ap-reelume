// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Courses;

/// <summary>
/// Marking a folder as a course (CRS-001).
/// </summary>
/// <remarks>
/// What these hold to, beyond the happy path: the depth is declared and never guessed, nothing goes
/// to the network, nothing on disk is written, and marking the same folder twice is the same course
/// re-read rather than a second one.
/// </remarks>
public sealed class MarkCoursesInRootTests
{
    private static readonly LibraryRootId RootId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private const string RootPath = @"D:\Cursos";

    [Fact]
    public async Task A_folder_of_numbered_videos_becomes_a_course_with_its_modules()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Módulo uno\02 - El nodo.mp4",
            @"D:\Cursos\Composición\01 - Módulo uno\01 - Intro.mp4",
            @"D:\Cursos\Composición\02 - Módulo dos\01 - Máscaras.mp4");

        var marked = Assert.Single(await world.ExecuteAsync(courseDepth: 1));

        Assert.Equal("Composición", marked.Title);
        Assert.Equal("Composición", marked.RelativePath);
        Assert.Equal(2, marked.ModuleCount);
        Assert.Equal(3, marked.LessonCount);

        var lessons = world.Courses.Saved[marked.Id];
        Assert.Equal(["Intro", "El nodo", "Máscaras"], lessons.Select(lesson => lesson.Title));
        Assert.Equal(["Módulo uno", "Módulo uno", "Módulo dos"], lessons.Select(lesson => lesson.Module));
    }

    /// <summary>
    /// The depth is the root's declaration. The same tree read at another depth is another answer,
    /// and this use case never picks one for the user.
    /// </summary>
    [Fact]
    public async Task The_declared_depth_decides_what_a_course_is_and_is_written_down()
    {
        var world = new World(@"D:\Cursos\3D\Composición\01 - Módulo\01 - Intro.mp4");

        var shallow = Assert.Single(await world.ExecuteAsync(courseDepth: 1));
        Assert.Equal("3D", shallow.Title);
        Assert.Equal(1, world.Declarations.Depth);

        var declared = Assert.Single(await world.ExecuteAsync(courseDepth: 2));
        Assert.Equal("Composición", declared.Title);
        Assert.Equal(2, world.Declarations.Depth);
    }

    [Fact]
    public async Task Marking_the_same_folder_twice_is_one_course_re_read()
    {
        var world = new World(@"D:\Cursos\Composición\01 - Intro.mp4");

        var first = Assert.Single(await world.ExecuteAsync(courseDepth: 1));
        var second = Assert.Single(await world.ExecuteAsync(courseDepth: 1));

        Assert.Equal(first.Id, second.Id);
        Assert.Single(world.Courses.Saved);
    }

    /// <summary>
    /// Of 1955 files measured in one real collection only 595 were video; the rest is the course's
    /// working material and has to be walked past without tripping.
    /// </summary>
    [Fact]
    public async Task Everything_that_is_not_an_approved_video_is_walked_past()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Composición\proyecto.zip",
            @"D:\Cursos\Composición\escenas\shot.blend",
            @"D:\Cursos\Composición\apuntes.pdf",
            @"D:\Cursos\Composición\secuencia\frame.0001.png");

        var marked = Assert.Single(await world.ExecuteAsync(courseDepth: 1));

        Assert.Equal(1, marked.LessonCount);
        Assert.Equal(0, marked.ModuleCount);
    }

    /// <summary>
    /// An entry the enumerator could not read is the scan's business to report, not a lesson.
    /// </summary>
    [Fact]
    public async Task A_file_the_enumerator_could_not_read_is_not_a_lesson()
    {
        var world = new World(@"D:\Cursos\Composición\01 - Intro.mp4");
        world.Enumerator.Failing.Add(@"D:\Cursos\Composición\02 - Roto.mp4");

        var marked = Assert.Single(await world.ExecuteAsync(courseDepth: 1));

        Assert.Equal(1, marked.LessonCount);
    }

    /// <summary>
    /// The lesson is anchored to LIB-009's identity rather than to its path, which is what makes
    /// progress survive a move. A file the catalogue has not seen yet still becomes a lesson — it is
    /// a lesson whose file is not catalogued, not an absence.
    /// </summary>
    [Fact]
    public async Task A_lesson_carries_the_identity_of_its_file_when_the_catalogue_has_one()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Composición\02 - Nodo.mp4");
        var known = world.MediaFiles.Add(@"D:\Cursos\Composición\01 - Intro.mp4");

        var marked = Assert.Single(await world.ExecuteAsync(courseDepth: 1));
        var lessons = world.Courses.Saved[marked.Id];

        Assert.Equal(known, lessons[0].MediaFileId);
        Assert.Null(lessons[1].MediaFileId);
    }

    [Fact]
    public async Task A_root_with_no_video_deep_enough_marks_nothing()
    {
        var world = new World(@"D:\Cursos\suelto.mp4");

        Assert.Empty(await world.ExecuteAsync(courseDepth: 1));
        Assert.Empty(world.Courses.Saved);

        // The declaration still stands: the user said this root holds courses, and it holding none
        // today is an answer rather than a reason to forget what they said.
        Assert.Equal(1, world.Declarations.Depth);
    }

    /// <summary>
    /// A path the enumerator hands back from outside the root would put a lesson of one library into
    /// a course of another, so it is dropped rather than trusted.
    /// </summary>
    [Fact]
    public async Task A_path_that_escapes_the_root_is_dropped()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Otra\Composición\01 - Intruso.mp4");

        var marked = Assert.Single(await world.ExecuteAsync(courseDepth: 1));

        Assert.Equal(1, marked.LessonCount);
    }

    [Fact]
    public async Task A_root_that_ends_in_a_separator_reads_the_same_tree()
    {
        var world = new World(@"D:\Cursos\Composición\01 - Intro.mp4") { RootPathOverride = @"D:\Cursos\" };

        var marked = Assert.Single(await world.ExecuteAsync(courseDepth: 1));

        Assert.Equal("Composición", marked.RelativePath);
    }

    [Fact]
    public async Task An_unknown_root_is_refused_before_anything_is_declared()
    {
        var world = new World(@"D:\Cursos\Composición\01 - Intro.mp4") { RootExists = false };

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ExecuteAsync(courseDepth: 1));
        Assert.Null(world.Declarations.Depth);
    }

    [Fact]
    public async Task There_is_no_depth_zero_and_no_empty_batch()
    {
        var world = new World(@"D:\Cursos\Composición\01 - Intro.mp4");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            world.UseCase.ExecuteAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => world.ExecuteAsync(courseDepth: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            world.UseCase.ExecuteAsync(
                new MarkCoursesInRootCommand(RootId, 1, BatchSize: 0),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Pointing at one folder declares the depth for the whole root, and at that depth there are
    /// usually neighbours nobody has said anything about. ADR-0006 amendment 1 has the application
    /// ask rather than claim them, so this pass marks the one that was named and hands the rest
    /// back to be counted into «Hemos encontrado {0} carpetas más».
    /// </summary>
    [Fact]
    public async Task Only_the_folder_that_was_pointed_at_is_marked_and_the_others_are_named()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4",
            @"D:\Cursos\Render\01 - Intro.mp4");

        var result = await world.ExecuteAsync(1, "Composición");

        Assert.Equal(["Composición"], result.Marked.Select(course => course.RelativePath));
        Assert.Equal(["Modelado", "Render"], result.Others.Order());
        Assert.Single(world.Courses.Saved);

        // The depth is the root's, so it is written down even though only one folder was claimed.
        Assert.Equal(1, world.Declarations.Depth);
    }

    /// <summary>
    /// "Yes, they are all courses" comes back as a pass with no filter at all: everything detected
    /// is marked and there is nothing left over to ask about a second time.
    /// </summary>
    [Fact]
    public async Task An_unfiltered_pass_marks_every_folder_at_the_depth_and_leaves_nothing_over()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");

        var result = await world.UseCase.ExecuteAsync(
            new MarkCoursesInRootCommand(RootId, 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Composición", "Modelado"], result.Marked.Select(course => course.RelativePath).Order());
        Assert.Empty(result.Others);
    }

    /// <summary>
    /// The filter is compared against what detection found rather than trusted as given: a folder
    /// nobody detected is not a course this pass can mark, whoever named it.
    /// </summary>
    [Fact]
    public async Task A_named_folder_detection_never_found_marks_nothing()
    {
        var world = new World(@"D:\Cursos\Composición\01 - Intro.mp4");

        var result = await world.ExecuteAsync(1, "Inventada");

        Assert.Empty(result.Marked);
        Assert.Equal(["Composición"], result.Others);
        Assert.Empty(world.Courses.Saved);
    }

    private sealed class World
    {
        public World(params string[] files)
        {
            Enumerator = new StubEnumerator(files);
            UseCase = new MarkCoursesInRoot(Roots, Courses, Declarations, Enumerator, MediaFiles, Clock);
        }

        public StubRoots Roots { get; } = new();

        public StubCourses Courses { get; } = new();

        public StubDeclarations Declarations { get; } = new();

        public StubEnumerator Enumerator { get; }

        public StubMediaFiles MediaFiles { get; } = new();

        public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero));

        public MarkCoursesInRoot UseCase { get; }

        public bool RootExists
        {
            get => Roots.Exists;
            init => Roots.Exists = value;
        }

        public string RootPathOverride
        {
            get => Roots.Path;
            init => Roots.Path = value;
        }

        public async Task<IReadOnlyList<MarkedCourse>> ExecuteAsync(int courseDepth) =>
            (await UseCase.ExecuteAsync(
                new MarkCoursesInRootCommand(RootId, courseDepth),
                TestContext.Current.CancellationToken)).Marked;

        public Task<MarkedCourses> ExecuteAsync(int courseDepth, params string[] onlyRelativePaths) =>
            UseCase.ExecuteAsync(
                new MarkCoursesInRootCommand(RootId, courseDepth, OnlyRelativePaths: onlyRelativePaths),
                TestContext.Current.CancellationToken);
    }

    private sealed class StubRoots : ILibraryRootRepository
    {
        public bool Exists { get; set; } = true;

        public string Path { get; set; } = RootPath;

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Exists
                ? new LibraryRoot(id, Path, RootKind.Local, RootAvailability.Available, ScanPolicy.Manual)
                : null);

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
