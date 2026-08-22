// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// No view writes a word. Every word on screen comes from the two dictionaries, and what is left
/// written into the markup is symbols.
/// </summary>
/// <remarks>
/// <para>
/// This rule existed twice, once inside <c>BackupViewTests</c> and once inside
/// <c>LifecycleSettingsTests</c>, each watching its own file and each written as "no literal at all".
/// Two things were wrong with that. It covered <b>two views of fifty</b>, and it was stricter than the
/// tree: the same <c>⚠</c> those two refused is literal in every other view that carries it, and
/// <c>○ ◐ ●</c>, <c>→</c>, <c>✓</c> and <c>!</c> are literal by decision. Both copies fired on a glyph
/// this batch added, one after the other, which is what a rule kept in two places does.
/// </para>
/// <para>
/// So it is stated once, over every view, as what it actually protects: <b>a literal is allowed only
/// if it holds no letter at all</b>. A word cannot pass as a symbol — one letter anywhere in the value
/// fails it — and the two per-view copies are gone, their key-existence halves left where they were.
/// </para>
/// <para>
/// Measured on 2026-08-22 before writing this: <b>zero</b> literals with a letter across every
/// <c>.axaml</c> under <c>src/</c>, so the rule is not being introduced to a tree that breaks it.
/// </para>
/// </remarks>
public sealed class ViewLiteralTests
{
    /// <summary>The attributes that put text on the screen.</summary>
    private static readonly string[] PaintingAttributes = ["Text", "Content", "ToolTip.Tip", "Header"];

    [Fact]
    public void No_view_writes_a_word_that_a_dictionary_should_have_written()
    {
        var views = Directory
            .EnumerateFiles(RepositoryLayout.PathFromRoot("src"), "*.axaml", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // The corpus is asserted first: a scan that found no views would report no words either.
        Assert.True(views.Length >= 40, $"the scan found {views.Length} views, so it is looking in the wrong place.");

        var painted = views
            .SelectMany(path => XDocument.Load(path).Descendants()
                .SelectMany(element => element.Attributes())
                .Where(attribute => PaintingAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                .Where(attribute => !attribute.Value.TrimStart().StartsWith('{'))
                .Select(attribute => (Path: Path.GetFileName(path), attribute.Value)))
            .ToArray();

        var words = painted.Where(entry => entry.Value.Any(char.IsLetter)).ToArray();
        Assert.True(
            words.Length == 0,
            "these views write words the dictionaries should have written: "
                + string.Join(", ", words.Select(entry => $"{entry.Path}='{entry.Value}'")));

        // And symbols survive, because "no words" is also what a tree painting nothing would report.
        Assert.NotEmpty(painted);
    }
}
