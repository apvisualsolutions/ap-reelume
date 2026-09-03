// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation;

/// <summary>
/// A localised string read from the running application's dictionaries, with a fallback for when
/// there are none.
/// </summary>
/// <remarks>
/// It exists because two areas need it and the one that had it was called <c>CourseText</c>. Calling
/// that from the metadata editor would have been a name describing its first caller rather than its
/// shape — which is the defect this repository measured twice on 2026-09-03, in a style class and in
/// a scope row, and the reason a second caller writes a duplicate instead.
/// <para>
/// The fallback is English and not an exception: a view model built in a test has no application
/// behind it, and a screen that threw rather than showing a word would make every one of those a
/// crash.
/// </para>
/// </remarks>
public static class PresentationText
{
    /// <summary>The string for this key, or the fallback when nothing resolves it.</summary>
    public static string Resource(string key, string fallback) =>
        Resource(Avalonia.Application.Current, key, fallback);

    /// <summary>The same, against a given application rather than the running one.</summary>
    /// <remarks>
    /// <b>Public so that «there is no application» can be measured</b>, which is the arm that
    /// matters most: it is what every view model built in a test takes, and the one where throwing
    /// instead of answering would turn each of those into a crash. Reached through the running
    /// application it is unreachable by definition — the harness has already built one — so a seam
    /// is the difference between covering that arm and exempting it.
    /// <para>
    /// The variant is null on purpose: these live in language dictionaries rather than theme ones,
    /// and asking for the running theme variant resolves none of them.
    /// </para>
    /// </remarks>
    public static string Resource(Avalonia.Application? application, string key, string fallback) =>
        application is not null
            && application.TryGetResource(key, null, out var value)
            && value is string text
                ? text
                : fallback;
}
