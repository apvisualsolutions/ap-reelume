// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Home;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The two things the course dialog's words do that markup alone cannot promise (CRS-001).
/// </summary>
/// <remarks>
/// Both were written against an unmeasured assumption and are measured here instead. The Avalonia
/// documentation the repository consults has no page on either: a lookup for <c>TextBlock</c>
/// answered "no results", and that is a fact rather than a dead end — it means the answer has to
/// come from running it.
/// </remarks>
public sealed class CourseDialogStringsTests
{
    /// <summary>
    /// The course shape is a tree, and a tree needs line breaks. It is written as <c>&amp;#10;</c>
    /// in the dictionary because AXAML is XML, and whether that survives into the string a
    /// <c>TextBlock</c> paints is exactly the sort of thing that looks right in the markup and
    /// arrives as one long line.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    public void The_course_shape_reaches_the_surface_as_six_lines(string language)
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        App.ApplyLanguage(application, CultureInfo.GetCultureInfo(language));

        Assert.True(
            application.TryGetResource("AddCourseShape", application.ActualThemeVariant, out var resource));
        var shape = Assert.IsType<string>(resource);

        var lines = shape.Split('\n');
        Assert.Equal(6, lines.Length);

        // A tree and not six sentences: the branch characters are the shape, and losing them would
        // leave a list that says nothing about what contains what.
        Assert.DoesNotContain("├", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("├─", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("│", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("└─", lines[4], StringComparison.Ordinal);
        Assert.EndsWith(".mp4", lines[5], StringComparison.Ordinal);
    }

    /// <summary>
    /// The neighbours' question is one sentence with a number in it, and the sentence follows the
    /// chosen language. <c>StringFormat</c> cannot do that here — its format has to be a literal —
    /// so the key travels as the converter's parameter and the count as its value.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("es-ES", "Hemos encontrado 2 carpetas más. ¿Son todas cursos?")]
    [InlineData("en-US", "We have found 2 more folders. Are they all courses?")]
    public void The_neighbours_question_carries_its_count_in_the_chosen_language(
        string language,
        string expected)
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        App.ApplyLanguage(application, CultureInfo.GetCultureInfo(language));

        var converted = new ResourceKeyConverter().Convert(
            2,
            typeof(string),
            "AddCourseNeighboursQuestionFormat",
            CultureInfo.InvariantCulture);

        Assert.Equal(expected, converted);
    }

    /// <summary>
    /// Without a key in the parameter the converter is what it always was: a key in, its words out.
    /// An empty parameter is not a key, which is what an unset <c>ConverterParameter</c> is.
    /// </summary>
    [AvaloniaFact]
    public void A_parameterless_conversion_still_resolves_the_value_as_the_key()
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));

        var converter = new ResourceKeyConverter();

        Assert.Equal(
            "Curso (carpeta de lecciones)",
            converter.Convert("AddAsCourseOption", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(
            "Curso (carpeta de lecciones)",
            converter.Convert("AddAsCourseOption", typeof(string), "   ", CultureInfo.InvariantCulture));
    }
}
