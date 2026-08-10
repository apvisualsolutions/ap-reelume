// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// Every public document exists in both languages and says the same amount in each.
/// </summary>
/// <remarks>
/// Counting headings is a blunt check, and that is the point: it cannot verify a translation, but it
/// does catch the failure that actually happens — a section added to one language and forgotten in
/// the other, which turns a bilingual promise into a Spanish promise with an English summary.
/// </remarks>
public sealed class BilingualHeadingTests
{
    private static readonly Regex Heading =
        new(@"(?m)^(?<level>#{1,6})\s+\S", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>The documents the release promises to publish in both languages.</summary>
    public static TheoryData<string> PublicDocuments() =>
    [
        "README",
        "docs/roadmap/README",
        "docs/user-guide/README",
        "docs/troubleshooting/README",
        "docs/development/README",
        "docs/privacy/PRIVACY",
        "docs/release/RELEASING",
        "docs/release/SMARTSCREEN",
        "docs/release/THIRD-PARTY-NOTICES",
        "docs/release/licenses/README",
        "docs/legal/LEGAL",
        "docs/CHANGELOG",
    ];

    [Theory]
    [MemberData(nameof(PublicDocuments))]
    public void The_document_exists_in_both_languages(string stem)
    {
        foreach (var language in new[] { "es", "en" })
        {
            var path = RepositoryLayout.PathFromRoot($"{stem}.{language}.md");
            Assert.True(File.Exists(path), $"{stem}.{language}.md is missing.");
        }
    }

    [Theory]
    [MemberData(nameof(PublicDocuments))]
    public void The_two_languages_carry_the_same_structure(string stem)
    {
        var spanish = HeadingShape(RepositoryLayout.PathFromRoot($"{stem}.es.md"));
        var english = HeadingShape(RepositoryLayout.PathFromRoot($"{stem}.en.md"));

        Assert.True(spanish.Length > 0, $"{stem}.es.md has no headings.");
        Assert.Equal(spanish, english);
    }

    /// <summary>
    /// Every evidence document carries both languages inside one file, so the two halves are edited
    /// together instead of drifting into two documents.
    /// </summary>
    /// <remarks>
    /// The repository has two conventions and both are accepted, because both keep the languages in
    /// one file: the earlier reports pair the languages inside each heading with a slash, and from
    /// T39B onwards the Spanish text is one section and its translation another. What is rejected is
    /// a report written in one language, which is the failure this exists to catch.
    /// </remarks>
    [Fact]
    public void Every_mvp_evidence_document_carries_both_languages_in_one_file()
    {
        var evidenceRoot = RepositoryLayout.PathFromRoot("docs/evidence/mvp");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(evidenceRoot, "T*.md"))
        {
            var text = File.ReadAllText(file);
            var sectioned = text.Contains("\n## Español", StringComparison.Ordinal)
                && text.IndexOf("\n## English", StringComparison.Ordinal)
                    > text.IndexOf("\n## Español", StringComparison.Ordinal);
            var pairedHeadings = HeadingLines(text).Count(line => line.Contains(" / ", StringComparison.Ordinal));
            if (!sectioned && pairedHeadings < 2)
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"These evidence documents are not bilingual: {string.Join(", ", offenders)}.");
    }

    /// <summary>
    /// The Spanish text keeps its diacritics. A document that lost them was written by something that
    /// could not read it back.
    /// </summary>
    [Fact]
    public void The_spanish_documents_keep_their_accents()
    {
        var flattened = new List<string>();
        foreach (var file in Directory.EnumerateFiles(
            RepositoryLayout.PathFromRoot("docs"), "*.es.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (!text.Any(character => "áéíóúñÁÉÍÓÚÑ¿¡".Contains(character, StringComparison.Ordinal)))
            {
                flattened.Add(Path.GetRelativePath(RepositoryLayout.Root, file));
            }
        }

        Assert.True(flattened.Count == 0, $"Spanish documents with no diacritics at all: {string.Join(", ", flattened)}.");
    }

    private static IEnumerable<string> HeadingLines(string text) =>
        text.Split('\n').Where(line => line.StartsWith('#'));

    private static string[] HeadingShape(string path) =>
        File.Exists(path)
            ? [.. Heading.Matches(File.ReadAllText(path)).Select(match => match.Groups["level"].Value)]
            : [];
}
