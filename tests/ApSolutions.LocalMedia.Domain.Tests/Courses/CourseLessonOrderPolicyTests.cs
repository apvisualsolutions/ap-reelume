// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Courses;

/// <summary>
/// The order a course is watched in (CRS-001, ADR-0006 decision 4).
/// </summary>
/// <remarks>
/// The shapes here are the ones measured over a real collection of 595 lessons and not shapes
/// invented for the test: <c>NN - title</c> and <c>NN-title</c> at 62.5 %, <c>NN. title</c> at
/// 17.8 %, <c>NN_title</c> at 0.5 %, and 19.2 % carrying their numbering somewhere else.
/// </remarks>
public sealed class CourseLessonOrderPolicyTests
{
    [Theory]
    [InlineData("01 - Introducción", 1, null, "Introducción")]
    [InlineData("02-Primeros pasos", 2, null, "Primeros pasos")]
    [InlineData("03. El nodo de ruido", 3, null, "El nodo de ruido")]
    [InlineData("04_Composición", 4, null, "Composición")]
    [InlineData("10 — El render", 10, null, "El render")]
    [InlineData("007 - Ceros a la izquierda", 7, null, "Ceros a la izquierda")]
    public void A_leading_number_names_the_lesson_and_leaves_the_title_alone(
        string name, int major, int? minor, string title)
    {
        Assert.Equal(new LessonOrdinal(major, minor), CourseLessonOrderPolicy.ReadOrdinal(name));
        Assert.Equal(title, CourseLessonOrderPolicy.ReadTitle(name));
    }

    /// <summary>
    /// The defect this exists to stop: today the film name cleaner turns <c>1.3 Título</c> into
    /// <c>1 3 Título</c>, which destroys both the order and the title in one move.
    /// </summary>
    [Theory]
    [InlineData("1.3 El canal alfa", 1, 3, "El canal alfa")]
    [InlineData("2.10 - Máscaras", 2, 10, "Máscaras")]
    [InlineData("12.1_Rotoscopia", 12, 1, "Rotoscopia")]
    public void A_hierarchical_number_survives_as_a_pair(string name, int major, int minor, string title)
    {
        Assert.Equal(new LessonOrdinal(major, minor), CourseLessonOrderPolicy.ReadOrdinal(name));
        Assert.Equal(title, CourseLessonOrderPolicy.ReadTitle(name));
    }

    /// <summary>
    /// Four digits are a year, not a lesson number. This is the measured false positive the film
    /// parser produces on the same collection — a title dated with a year came back as a film of
    /// that year, and the year was stripped out of the title.
    /// </summary>
    [Theory]
    [InlineData("2019 - Retrospectiva")]
    [InlineData("ES_014_02_07")]
    [InlineData("Bonus (3) material")]
    [InlineData("Introducción")]
    [InlineData("1")]
    [InlineData("42.")]
    [InlineData("5 -")]
    public void A_name_that_carries_no_leading_number_keeps_all_of_itself(string name)
    {
        Assert.Null(CourseLessonOrderPolicy.ReadOrdinal(name));
        Assert.Equal(name, CourseLessonOrderPolicy.ReadTitle(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_is_not_a_lesson_number(string? name)
    {
        Assert.Null(CourseLessonOrderPolicy.ReadOrdinal(name));
        Assert.Equal(string.Empty, CourseLessonOrderPolicy.ReadTitle(name));
    }

    /// <summary>
    /// Alphabetical puts 10 before 2, and a course watched in that order is a course watched wrong.
    /// </summary>
    [Fact]
    public void Ten_comes_after_two()
    {
        var ordered = CourseLessonOrderPolicy.Order(
            ["10 - Décima", "2 - Segunda", "1 - Primera"],
            name => name);

        Assert.Equal(["1 - Primera", "2 - Segunda", "10 - Décima"], ordered);
    }

    [Fact]
    public void A_lesson_comes_before_its_own_subdivisions()
    {
        var ordered = CourseLessonOrderPolicy.Order(
            ["1.2 Segunda parte", "1.1 Primera parte", "1 - El módulo", "2 - Siguiente"],
            name => name);

        Assert.Equal(["1 - El módulo", "1.1 Primera parte", "1.2 Segunda parte", "2 - Siguiente"], ordered);
    }

    /// <summary>
    /// The 19.2 % that carries its numbering elsewhere sorts last and alphabetically, which is what
    /// puts the zero-padded encoded schemes right without a pattern anybody has to maintain.
    /// </summary>
    [Fact]
    public void What_carries_no_number_sorts_last_and_alphabetically()
    {
        var ordered = CourseLessonOrderPolicy.Order(
            ["ES_014_02_07", "ES_014_01_03", "3 - Tercera", "Bonus"],
            name => name);

        Assert.Equal(["3 - Tercera", "Bonus", "ES_014_01_03", "ES_014_02_07"], ordered);
    }

    /// <summary>
    /// Two lessons with the same number are still ordered, and by something other than the order the
    /// file system happened to hand them over in.
    /// </summary>
    [Fact]
    public void A_repeated_number_still_has_one_answer()
    {
        var forwards = CourseLessonOrderPolicy.Order(["1 - Beta", "1 - alfa"], name => name);
        var backwards = CourseLessonOrderPolicy.Order(["1 - alfa", "1 - Beta"], name => name);

        Assert.Equal(["1 - alfa", "1 - Beta"], forwards);
        Assert.Equal(forwards, backwards);
    }

    /// <summary>
    /// Two names differing only in case: the ordinal comparison ties, the case-insensitive
    /// comparison ties, and the ordinal string comparison is what is left to decide.
    /// </summary>
    [Fact]
    public void Case_alone_is_still_decided()
    {
        var forwards = CourseLessonOrderPolicy.Order(["alfa", "Alfa"], name => name);
        var backwards = CourseLessonOrderPolicy.Order(["Alfa", "alfa"], name => name);

        Assert.Equal(forwards, backwards);
    }

    [Fact]
    public void A_name_the_projection_cannot_produce_is_read_as_empty()
    {
        var ordered = CourseLessonOrderPolicy.Order([1, 2], _ => null!);

        Assert.Equal(2, ordered.Count);
    }

    [Fact]
    public void Ordering_nothing_needs_something_to_order_and_something_to_read_it_with()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CourseLessonOrderPolicy.Order<string>(null!, name => name));
        Assert.Throws<ArgumentNullException>(() =>
            CourseLessonOrderPolicy.Order(["a"], null!));
    }
}
