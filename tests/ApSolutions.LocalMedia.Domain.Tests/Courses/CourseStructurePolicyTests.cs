// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Courses;

/// <summary>
/// Which folders are courses, at the depth the root declares (CRS-001, ADR-0006 decision 3).
/// </summary>
/// <remarks>
/// The two shapes exercised here are the two real roots the ADR measured: one is
/// <c>root / category / course / [section] / lesson</c> and the other is
/// <c>root / course / section / lesson</c>. Any fixed depth would have been right about one and
/// wrong about the other, which is the whole reason the depth is declared.
/// </remarks>
public sealed class CourseStructurePolicyTests
{
    [Fact]
    public void At_depth_one_the_first_folder_is_the_course()
    {
        var courses = CourseStructurePolicy.Detect(
        [
            "Curso de composición/01 - Módulo uno/01 - Intro.mp4",
            "Curso de composición/01 - Módulo uno/02 - El nodo.mp4",
            "Curso de composición/02 - Módulo dos/01 - Máscaras.mp4",
        ],
            courseDepth: 1);

        var course = Assert.Single(courses);
        Assert.Equal("Curso de composición", course.RelativePath);
        Assert.Equal("Curso de composición", course.Name);
        Assert.Equal("Curso de composición", course.Title);
        Assert.Null(course.Ordinal);
        Assert.Equal(["01 - Módulo uno", "02 - Módulo dos"], course.Sections.Select(section => section.Name));
        Assert.Equal(["Módulo uno", "Módulo dos"], course.Sections.Select(section => section.Title));
        Assert.Equal([new LessonOrdinal(1, null), new LessonOrdinal(2, null)],
            course.Sections.Select(section => section.Ordinal));
        Assert.Equal(["Intro", "El nodo"], course.Sections[0].Lessons.Select(lesson => lesson.Title));
    }

    /// <summary>
    /// The same tree read at the wrong depth answers a different question, which is why the depth is
    /// the root's declaration and never the program's guess.
    /// </summary>
    [Fact]
    public void At_depth_two_the_category_is_not_the_course()
    {
        const string path = "3D/Curso de composición/01 - Módulo uno/01 - Intro.mp4";

        var shallow = Assert.Single(CourseStructurePolicy.Detect([path], courseDepth: 1));
        var declared = Assert.Single(CourseStructurePolicy.Detect([path], courseDepth: 2));

        Assert.Equal("3D", shallow.Title);
        Assert.Equal("Curso de composición", declared.Title);
        Assert.Equal("3D/Curso de composición", declared.RelativePath);
        Assert.Equal(["Módulo uno"], declared.Sections.Select(section => section.Title));
    }

    /// <summary>
    /// A course with no modules is a course with no modules, not a course with one module nobody
    /// named: the section is <see langword="null"/> and the lessons hang directly off it.
    /// </summary>
    [Fact]
    public void Lessons_loose_in_the_course_folder_have_no_section()
    {
        var course = Assert.Single(CourseStructurePolicy.Detect(
            ["Tutorial corto/01 - Uno.mkv", "Tutorial corto/02 - Dos.mkv"],
            courseDepth: 1));

        var section = Assert.Single(course.Sections);
        Assert.Null(section.Name);
        Assert.Null(section.Title);
        Assert.Null(section.Ordinal);
        Assert.Equal(["Uno", "Dos"], section.Lessons.Select(lesson => lesson.Title));
    }

    [Fact]
    public void A_course_with_both_shapes_opens_with_the_loose_lessons()
    {
        var course = Assert.Single(CourseStructurePolicy.Detect(
            ["Curso/00 - Bienvenida.mp4", "Curso/01 - Módulo/01 - Empezamos.mp4"],
            courseDepth: 1));

        Assert.Equal([null, "Módulo"], course.Sections.Select(section => section.Title));
    }

    /// <summary>
    /// Anything deeper than a section flattens against it: a video four levels below the root inside
    /// a technical folder is one of the four failure modes the guessing rule had.
    /// </summary>
    [Fact]
    public void Anything_below_a_section_flattens_against_it()
    {
        var course = Assert.Single(CourseStructurePolicy.Detect(
        [
            "Curso/01 - Módulo/data/media/01 - Lección.mp4",
            "Curso/01 - Módulo/02 - Lección.mp4",
        ],
            courseDepth: 1));

        var section = Assert.Single(course.Sections);
        Assert.Equal("01 - Módulo", section.Name);
        Assert.Equal("Módulo", section.Title);
        Assert.Equal(["Lección", "Lección"], section.Lessons.Select(lesson => lesson.Title));
        Assert.Equal(
            ["Curso/01 - Módulo/data/media/01 - Lección.mp4", "Curso/01 - Módulo/02 - Lección.mp4"],
            section.Lessons.Select(lesson => lesson.RelativePath));
    }

    /// <summary>
    /// A resource folder with no video in it is not a section, and that comes free from being fed
    /// video paths: of 1955 files measured in one collection only 595 were video.
    /// </summary>
    [Fact]
    public void A_folder_with_no_video_is_not_a_section()
    {
        var course = Assert.Single(CourseStructurePolicy.Detect(
            ["Curso/01 - Módulo/01 - Lección.mp4"],
            courseDepth: 1));

        Assert.Equal(["Módulo"], course.Sections.Select(section => section.Title));
    }

    /// <summary>
    /// A video above the declared depth belongs to no course. Claiming it for the nearest folder is
    /// the guess this policy refuses.
    /// </summary>
    [Theory]
    [InlineData("suelto.mp4", 1)]
    [InlineData("Curso/suelto.mp4", 2)]
    [InlineData("", 1)]
    public void A_video_shallower_than_the_depth_belongs_to_no_course(string path, int depth)
    {
        Assert.Empty(CourseStructurePolicy.Detect([path], depth));
    }

    [Fact]
    public void Separators_of_either_kind_read_the_same_tree()
    {
        var slashes = CourseStructurePolicy.Detect(["Curso/Módulo/Lección.mp4"], courseDepth: 1);
        var backslashes = CourseStructurePolicy.Detect([@"Curso\Módulo\Lección.mp4"], courseDepth: 1);

        Assert.Equal(slashes[0].RelativePath, backslashes[0].RelativePath);
        Assert.Equal(slashes[0].Sections[0].Name, backslashes[0].Sections[0].Name);
        Assert.Equal(
            slashes[0].Sections[0].Lessons[0].RelativePath,
            backslashes[0].Sections[0].Lessons[0].RelativePath);
    }

    [Fact]
    public void Two_spellings_of_one_folder_are_one_course()
    {
        var course = Assert.Single(CourseStructurePolicy.Detect(
            ["Curso/01 - Uno.mp4", "CURSO/02 - Dos.mp4"],
            courseDepth: 1));

        Assert.Equal("Curso", course.Title);
        Assert.Equal(2, course.Sections[0].Lessons.Count);
    }

    [Fact]
    public void Two_spellings_of_one_section_are_one_section()
    {
        var course = Assert.Single(CourseStructurePolicy.Detect(
            ["Curso/Módulo/01 - Uno.mp4", "Curso/MÓDULO/02 - Dos.mp4"],
            courseDepth: 1));

        var section = Assert.Single(course.Sections);
        Assert.Equal(2, section.Lessons.Count);
    }

    [Fact]
    public void The_courses_themselves_come_back_in_order()
    {
        var courses = CourseStructurePolicy.Detect(
            ["10 - Décimo/a.mp4", "2 - Segundo/a.mp4", "Sin número/a.mp4"],
            courseDepth: 1);

        Assert.Equal(["Segundo", "Décimo", "Sin número"], courses.Select(course => course.Title));
        Assert.Equal([new LessonOrdinal(2, null), new LessonOrdinal(10, null), null],
            courses.Select(course => course.Ordinal));
    }

    [Fact]
    public void A_lesson_keeps_its_own_name_beside_the_title_it_was_read_into()
    {
        var course = Assert.Single(CourseStructurePolicy.Detect(
            ["Curso/1.3 El canal alfa.mp4"],
            courseDepth: 1));

        var lesson = Assert.Single(course.Sections[0].Lessons);
        Assert.Equal("1.3 El canal alfa", lesson.Name);
        Assert.Equal("El canal alfa", lesson.Title);
        Assert.Equal(new LessonOrdinal(1, 3), lesson.Ordinal);
    }

    [Fact]
    public void A_null_path_is_not_a_lesson()
    {
        Assert.Empty(CourseStructurePolicy.Detect([null!], courseDepth: 1));
    }

    [Fact]
    public void There_is_no_depth_zero_and_no_paths_to_read_from_nothing()
    {
        Assert.Throws<ArgumentNullException>(() => CourseStructurePolicy.Detect(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CourseStructurePolicy.Detect([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CourseStructurePolicy.Detect([], -1));
    }
}
