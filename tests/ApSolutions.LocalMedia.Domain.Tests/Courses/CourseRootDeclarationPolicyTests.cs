// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Courses;

/// <summary>
/// Reading a root and a depth off one pointed-at folder (CRS-001, ADR-0006 amendment 1).
/// </summary>
/// <remarks>
/// The amendment's whole claim is that the derived number is the same number that used to be typed,
/// so these hold it to that: the two root shapes the ADR measured come out as depth 1 and depth 2
/// from the gesture alone, without a person counting folders in their head.
/// </remarks>
public sealed class CourseRootDeclarationPolicyTests
{
    /// <summary>
    /// The shape of the second measured root: <c>root / category / course</c>. Pointing at the
    /// course inside a root the catalogue already holds derives 2, which is the number the ADR
    /// measured returning all 12 courses with their sections.
    /// </summary>
    [Fact]
    public void A_course_inside_a_catalogued_root_derives_its_depth_from_the_gesture()
    {
        var declaration = CourseRootDeclarationPolicy.Derive(
            @"D:\Cursos\3D\Composición",
            [@"D:\Peliculas", @"D:\Cursos"]);

        Assert.NotNull(declaration);
        Assert.Equal(@"D:\Cursos", declaration.RootPath);
        Assert.Equal(2, declaration.CourseDepth);
        Assert.Equal("3D/Composición", declaration.RelativePath);
        Assert.True(declaration.IsExistingRoot);
    }

    [Fact]
    public void A_course_directly_under_a_catalogued_root_derives_depth_one()
    {
        var declaration = CourseRootDeclarationPolicy.Derive(@"D:\Cursos\Composición", [@"D:\Cursos"]);

        Assert.NotNull(declaration);
        Assert.Equal(@"D:\Cursos", declaration.RootPath);
        Assert.Equal(1, declaration.CourseDepth);
        Assert.Equal("Composición", declaration.RelativePath);
        Assert.True(declaration.IsExistingRoot);
    }

    /// <summary>
    /// With nothing catalogued the parent becomes the root, which is what puts the siblings the
    /// amendment offers to mark next at the same level as the folder that was pointed at.
    /// </summary>
    [Fact]
    public void With_no_root_holding_it_the_parent_becomes_the_root_at_depth_one()
    {
        var declaration = CourseRootDeclarationPolicy.Derive(@"D:\Cursos\3D\Composición", []);

        Assert.NotNull(declaration);
        Assert.Equal(@"D:\Cursos\3D", declaration.RootPath);
        Assert.Equal(1, declaration.CourseDepth);
        Assert.Equal("Composición", declaration.RelativePath);
        Assert.False(declaration.IsExistingRoot);
    }

    /// <summary>
    /// A root is not a course inside itself. Depth 0 is not a depth, and taking it as one would make
    /// every child of that root a course without anybody having said so.
    /// </summary>
    [Fact]
    public void Pointing_at_a_catalogued_root_itself_declares_nothing()
    {
        Assert.Null(CourseRootDeclarationPolicy.Derive(@"D:\Cursos", [@"D:\Cursos"]));
        Assert.Null(CourseRootDeclarationPolicy.Derive(@"d:\cursos\", [@"D:\Cursos"]));
    }

    /// <summary>
    /// A folder sitting straight on a drive would make the whole volume a root, and the dialog's own
    /// help promises the opposite in as many words: the rest of the drive is not scanned.
    /// </summary>
    [Fact]
    public void A_folder_on_a_bare_drive_declares_nothing()
    {
        Assert.Null(CourseRootDeclarationPolicy.Derive(@"D:\Composición", []));
        Assert.Null(CourseRootDeclarationPolicy.Derive(@"D:\", []));
        Assert.Null(CourseRootDeclarationPolicy.Derive("Composición", []));
        Assert.Null(CourseRootDeclarationPolicy.Derive("D", []));
    }

    /// <summary>The dialog's own starting state, which reaches here every time somebody opens it.</summary>
    [Fact]
    public void An_empty_path_declares_nothing()
    {
        Assert.Null(CourseRootDeclarationPolicy.Derive(string.Empty, [@"D:\Cursos"]));
        Assert.Null(CourseRootDeclarationPolicy.Derive("   ", [@"D:\Cursos"]));
        Assert.Null(CourseRootDeclarationPolicy.Derive(null!, [@"D:\Cursos"]));
        Assert.Throws<ArgumentNullException>(() => CourseRootDeclarationPolicy.Derive(@"D:\Cursos\A", null!));
    }

    /// <summary>
    /// Windows takes both separators and a person can paste either. The relative path comes back
    /// with forward slashes whichever went in, because that is the shape
    /// <see cref="CourseStructurePolicy"/> hands back and the two are compared to each other.
    /// </summary>
    [Fact]
    public void Either_separator_and_a_trailing_one_read_the_same()
    {
        var declaration = CourseRootDeclarationPolicy.Derive("D:/Cursos/3D/Composición/", ["D:/Cursos"]);

        Assert.NotNull(declaration);
        Assert.Equal("D:/Cursos", declaration.RootPath);
        Assert.Equal(2, declaration.CourseDepth);
        Assert.Equal("3D/Composición", declaration.RelativePath);
    }

    /// <summary>
    /// A root that merely starts with the same letters is not a root that holds it:
    /// <c>D:\Cursos2</c> is not the parent of anything under <c>D:\Cursos</c>, and matching on the
    /// prefix alone would put a lesson of one library into a course of another.
    /// </summary>
    [Fact]
    public void A_root_that_only_shares_a_prefix_does_not_hold_the_folder()
    {
        var declaration = CourseRootDeclarationPolicy.Derive(@"D:\Cursos\Composición", [@"D:\Cursos2"]);

        Assert.NotNull(declaration);
        Assert.Equal(@"D:\Cursos", declaration.RootPath);
        Assert.False(declaration.IsExistingRoot);
    }
}
