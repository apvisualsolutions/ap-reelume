// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// A section heading is its own label, shouted — and it stays that way.
/// </summary>
/// <remarks>
/// The prototype writes these in small capitals, and AXAML has no <c>text-transform</c>: a converter
/// would have needed a markup extension of its own, because the strings are dynamic resources that
/// follow the language and there is no way to compose the two in markup.
/// <para>
/// So the heading is a second resource, and the cost of a second string is that the two could drift
/// apart — somebody edits the label and the heading keeps the old words in capitals, which is this
/// repository's characteristic defect at one string long. They do not get to: what is asserted is
/// that each heading <b>is</b> its label uppercased, in both languages, so an edit to one without
/// the other fails here.
/// </para>
/// </remarks>
public sealed class SectionHeadingTests
{
    private static readonly (string Label, string Heading)[] Pairs =
    [
        ("AudioOutputDeviceLabel", "AudioOutputDeviceHeading"),
        ("AudioOutputLayoutLabel", "AudioOutputLayoutHeading"),
    ];

    [AvaloniaFact]
    public void Every_heading_is_its_label_in_capitals_in_both_languages()
    {
        var application = Avalonia.Application.Current!;

        foreach (var name in new[] { "es-ES", "en-US" })
        {
            var culture = CultureInfo.GetCultureInfo(name);
            App.ApplyLanguage(application, culture);

            foreach (var (labelKey, headingKey) in Pairs)
            {
                var label = Resource(labelKey);
                var heading = Resource(headingKey);

                Assert.False(string.IsNullOrWhiteSpace(label), $"{labelKey} did not resolve in {name}.");
                Assert.False(string.IsNullOrWhiteSpace(heading), $"{headingKey} did not resolve in {name}.");
                Assert.Equal(label.ToUpper(culture), heading);
            }
        }

        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
    }

    private static string Resource(string key) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, null, out var value)
            && value is string text
                ? text
                : string.Empty;
}
