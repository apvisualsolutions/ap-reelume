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
/// Declaring a course from the folder somebody pointed at (CRS-001, ADR-0006 amendment 1).
/// </summary>
/// <remarks>
/// What these hold to is the amendment's claim: the derived depth is the same number that used to be
/// typed, and nothing beyond the named folder is marked until a person has said yes to it.
/// </remarks>
public sealed class DeclareCourseFolderTests
{
    /// <summary>
    /// The shape of the second measured root — <c>root / category / course</c> — inside a root the
    /// catalogue already holds. Depth 2 is derived from the gesture, and the two neighbours at that
    /// same depth are named rather than claimed.
    /// </summary>
    [Fact]
    public async Task Pointing_at_a_course_inside_a_catalogued_root_derives_the_depth_and_marks_only_it()
    {
        var world = new World(
            @"D:\Cursos\3D\Composición\01 - Intro.mp4",
            @"D:\Cursos\3D\Modelado\01 - Intro.mp4",
            @"D:\Cursos\2D\Ilustración\01 - Intro.mp4");
        world.Roots.Add(@"D:\Cursos");

        var declared = await world.ExecuteAsync(@"D:\Cursos\3D\Composición");

        Assert.Equal(2, declared.CourseDepth);
        Assert.Equal(["3D/Composición"], declared.Marked.Select(course => course.RelativePath));
        Assert.Equal(["2D/Ilustración", "3D/Modelado"], declared.Others.Order());

        // No root was added: one inside another is refused, so the catalogued one is the only answer.
        Assert.Single(world.Roots.All);
    }

    /// <summary>
    /// With nothing catalogued the parent becomes the root at depth 1, which is what puts the
    /// siblings the amendment offers to mark next at the same level as the pointed-at folder.
    /// </summary>
    [Fact]
    public async Task With_no_root_holding_it_the_parent_is_added_as_a_root_that_is_not_scanned()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");

        var declared = await world.ExecuteAsync(@"D:\Cursos\Composición", RootKind.Usb);

        Assert.Equal(1, declared.CourseDepth);
        Assert.Equal(["Composición"], declared.Marked.Select(course => course.RelativePath));
        Assert.Equal(["Modelado"], declared.Others);

        var added = Assert.Single(world.Roots.All);
        Assert.Equal(@"D:\Cursos", added.Path);
        Assert.Equal(RootKind.Usb, added.Kind);

        // The dialog's help promises the rest of the drive is not scanned, and a policy that scans
        // on startup would quietly break that promise.
        Assert.Equal(ScanPolicy.Manual, added.ScanPolicy);
    }

    /// <summary>
    /// "Yes, they are all courses" is a second pass naming the neighbours. It re-reads the root
    /// rather than trusting the first pass's answer, which is what makes the answer true when it is
    /// acted on instead of true when it was computed.
    /// </summary>
    [Fact]
    public async Task Saying_yes_to_the_neighbours_marks_them_and_leaves_nothing_over()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4",
            @"D:\Cursos\Render\01 - Intro.mp4");

        var first = await world.ExecuteAsync(@"D:\Cursos\Composición");
        Assert.Equal(["Modelado", "Render"], first.Others.Order());

        var second = await world.ExecuteAsync(@"D:\Cursos\Composición", RootKind.Local, [.. first.Others]);

        Assert.Equal(
            ["Composición", "Modelado", "Render"],
            second.Marked.Select(course => course.RelativePath).Order());
        Assert.Empty(second.Others);
        Assert.Equal(3, world.Courses.Saved.Count);

        // The second pass found the root the first one added rather than adding another.
        Assert.Single(world.Roots.All);
    }

    /// <summary>
    /// An empty answer to the question is not the same as no question: nothing extra is marked, and
    /// the neighbours are still named so the dialog can say so.
    /// </summary>
    [Fact]
    public async Task An_empty_answer_marks_nothing_extra()
    {
        var world = new World(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");

        var declared = await world.ExecuteAsync(@"D:\Cursos\Composición", RootKind.Local, []);

        Assert.Single(declared.Marked);
        Assert.Equal(["Modelado"], declared.Others);
    }

    /// <summary>
    /// The three paths no course can be declared from. The dialog answers each on screen: letting
    /// one out of here reaches the dispatcher, and on Windows that ends the process.
    /// </summary>
    [Fact]
    public async Task A_path_no_course_can_be_declared_from_is_refused()
    {
        var world = new World(@"D:\Cursos\Composición\01 - Intro.mp4");
        world.Roots.Add(@"D:\Cursos");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            world.UseCase.ExecuteAsync(null!, TestContext.Current.CancellationToken));

        // The root itself, a folder straight on a drive, and the dialog's own starting state.
        await Assert.ThrowsAsync<ArgumentException>(() => world.ExecuteAsync(@"D:\Cursos"));
        await Assert.ThrowsAsync<ArgumentException>(() => world.ExecuteAsync(@"E:\Composición"));
        await Assert.ThrowsAsync<ArgumentException>(() => world.ExecuteAsync(string.Empty));
    }

    [Fact]
    public void Nothing_is_constructed_without_what_it_needs()
    {
        var world = new CourseWorld();
        var add = new AddLibraryRoot(world.Roots, new VerbatimPathNormalizer());

        Assert.Throws<ArgumentNullException>(() =>
            new DeclareCourseFolder(null!, add, world.MarkCourses));
        Assert.Throws<ArgumentNullException>(() =>
            new DeclareCourseFolder(world.Roots, null!, world.MarkCourses));
        Assert.Throws<ArgumentNullException>(() =>
            new DeclareCourseFolder(world.Roots, add, null!));
    }

    /// <summary>The chain the composition wires, with the ports stood in for.</summary>
    private sealed class World(params string[] files)
    {
        private readonly CourseWorld _world = new(files);

        public CatalogueOfRoots Roots => _world.Roots;

        public StubCourses Courses => _world.Courses;

        public DeclareCourseFolder UseCase => _world.Declare;

        public Task<DeclaredCourseFolder> ExecuteAsync(
            string path,
            RootKind kind = RootKind.Local,
            IReadOnlyCollection<string>? alsoMark = null) =>
            UseCase.ExecuteAsync(
                new DeclareCourseFolderCommand(path, kind, alsoMark),
                TestContext.Current.CancellationToken);
    }
}
