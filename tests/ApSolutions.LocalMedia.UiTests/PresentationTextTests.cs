// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests;

/// <summary>
/// A localised string, with a fallback for when there is nothing to localise against.
/// </summary>
/// <remarks>
/// It exists because two areas wanted it and the one that had it was named after its first caller —
/// the same defect measured twice on 2026-09-03, in a style class and in a scope row. Both arms
/// matter: a view model built in a test has no application behind it, so a missing key has to answer
/// rather than throw.
/// </remarks>
public sealed class PresentationTextTests
{
    [AvaloniaFact]
    public void A_key_the_dictionaries_carry_answers_with_the_language()
    {
        var application = Avalonia.Application.Current!;
        try
        {
            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));

            var spanish = PresentationText.Resource("CoverChooseAction", "fallback");
            Assert.NotEqual("fallback", spanish);

            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("en-US"));
            var english = PresentationText.Resource("CoverChooseAction", "fallback");
            Assert.NotEqual("fallback", english);

            // Two languages and not one: a lookup that resolved a single merged dictionary would
            // answer the same string twice and satisfy any check written against one of them.
            Assert.NotEqual(spanish, english);
        }
        finally
        {
            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
        }
    }

    /// <summary>With no application at all, the fallback is the answer.</summary>
    /// <remarks>
    /// The arm every view model built in a test takes. It is unreachable through the running
    /// application — the harness has already built one — which is why the seam exists.
    /// </remarks>
    [Fact]
    public void With_no_application_the_fallback_is_the_answer()
    {
        Assert.Equal("nothing", PresentationText.Resource(null, "CoverChooseAction", "nothing"));
    }

    /// <summary>A key nothing carries answers with the fallback rather than throwing.</summary>
    [AvaloniaFact]
    public void A_key_nothing_carries_answers_with_the_fallback()
    {
        Assert.Equal(
            "nothing here",
            PresentationText.Resource("NoSuchKeyAnywhereInThisTree", "nothing here"));
    }

    /// <summary>
    /// A key whose value is not a string answers with the fallback too.
    /// </summary>
    /// <remarks>
    /// The dictionaries hold brushes, doubles and geometries under keys of their own, and a caller
    /// asking for the wrong one has to get a word rather than a cast that throws in front of
    /// somebody.
    /// </remarks>
    [AvaloniaFact]
    public void A_key_that_is_not_a_string_answers_with_the_fallback()
    {
        Assert.Equal(
            "not a brush",
            PresentationText.Resource("AccentBrush", "not a brush"));
    }
}
