// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// A section heading is its own label, shouted — it stays that way, and it stays on the panel.
/// </summary>
/// <remarks>
/// The prototype writes these in small capitals, and AXAML has no <c>text-transform</c>: a converter
/// would have needed a markup extension of its own, because the strings are dynamic resources that
/// follow the language and there is no way to compose the two in markup.
/// <para>
/// So the heading is a second resource, and the cost of a second string is that the two could drift
/// apart — somebody edits the label and the heading keeps the old words in capitals, which is this
/// repository's characteristic defect at one string long.
/// </para>
/// <para>
/// <b>Measured on the control, not on the resource file.</b> Until 2026-09-02 this read both strings
/// out of the dictionaries and never built the view, so deleting both heading blocks from
/// <c>AudioOutputView.axaml</c> left it green — the same defect this batch had just fixed in
/// <c>ButtonShapeTests</c>, one file later. What is asserted now is what the panel paints: the
/// headings that carry the class, their count, and that each one <b>is</b> its label uppercased in
/// both languages.
/// </para>
/// <para>
/// Its limitation, written here rather than assumed: the census covers the headings of this panel.
/// A <c>section-overline</c> added to another view is not seen from here.
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
    public void Every_heading_the_panel_paints_is_its_label_in_capitals_in_both_languages()
    {
        var application = Avalonia.Application.Current!;

        try
        {
            var view = new AudioOutputView();
            var window = new Window { Width = 420, Height = 600, Content = view };
            window.Show();

            foreach (var name in new[] { "es-ES", "en-US" })
            {
                var culture = CultureInfo.GetCultureInfo(name);
                App.ApplyLanguage(application, culture);
                Dispatcher.UIThread.RunJobs();

                var painted = view.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(block => block.Classes.Contains("section-overline"))
                    .Select(block => block.Text ?? string.Empty)
                    .ToArray();

                // The census, and the direction that matters: a heading removed from the markup
                // fails here, and one added without its pair in this table fails here too.
                Assert.Equal(Pairs.Length, painted.Length);

                foreach (var (labelKey, headingKey) in Pairs)
                {
                    var label = Resource(labelKey);
                    var heading = Resource(headingKey);

                    Assert.False(string.IsNullOrWhiteSpace(label), $"{labelKey} did not resolve in {name}.");
                    Assert.False(string.IsNullOrWhiteSpace(heading), $"{headingKey} did not resolve in {name}.");

                    // The two strings agree with each other...
                    Assert.Equal(label.ToUpper(culture), heading);

                    // ...and the panel is actually painting that one.
                    Assert.Contains(heading, painted);
                }
            }
        }
        finally
        {
            // Restored even on a red, or whatever runs next inherits an application in English.
            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
        }
    }

    private static string Resource(string key) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, null, out var value)
            && value is string text
                ? text
                : string.Empty;
}
