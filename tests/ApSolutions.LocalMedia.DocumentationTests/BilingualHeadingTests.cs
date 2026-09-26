// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// Every public document exists in both languages and says the same amount in each.
/// </summary>
/// <remarks>
/// Counting headings is a blunt check, and that is the point: it cannot verify a translation, but it
/// does catch the failure that actually happens — a section added to one language and forgotten in
/// the other, which turns a bilingual promise into a Spanish promise with an English summary.
/// </remarks>
public sealed partial class BilingualHeadingTests
{
    /// <summary>
    /// Fewer published Spanish documents than this and the accent sweep is reading an empty or
    /// truncated tree. Eight are published today.
    /// </summary>
    internal const int SpanishDocumentFloor = 6;

    private static readonly Regex Heading =
        new(@"(?m)^(?<level>#{1,6})\s+\S", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>The documents the release promises to publish in both languages.</summary>
    private static readonly string[] PublicStems =
    [
        "README",
        "docs/user-guide/README",
        "docs/troubleshooting/README",
        "docs/privacy/PRIVACY",
        "docs/release/SMARTSCREEN",
        "docs/release/THIRD-PARTY-NOTICES",
        "docs/release/licenses/README",
        "docs/CHANGELOG",
    ];

    public static TheoryData<string> PublicDocuments() => [.. PublicStems];

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
    /// Every published document the release promises in both languages is on the list above, so a
    /// new pair cannot ship without its structure being compared.
    /// </summary>
    [Fact]
    public void Every_published_pair_is_on_the_list()
    {
        var listed = PublicStems.ToHashSet(StringComparer.Ordinal);
        var unlisted = PublishedFiles.All
            .Where(path => path.EndsWith(".es.md", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/')[..^6])
            .Where(stem => !listed.Contains(stem))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unlisted.Length == 0,
            $"These published documents are bilingual and their structure is compared by nobody: {string.Join(", ", unlisted)}.");
    }

    /// <summary>
    /// The Spanish text keeps its diacritics. A document that lost them was written by something that
    /// could not read it back.
    /// </summary>
    [Fact]
    public void The_spanish_documents_keep_their_accents()
    {
        var spanish = PublishedFiles.All
            .Where(path => path.EndsWith(".es.md", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(
            spanish.Length >= SpanishDocumentFloor,
            $"only {spanish.Length} published Spanish documents were read, so the sweep is not reading the repository.");
        Assert.Empty(Flattened(spanish));
    }

    /// <summary>The accent sweep, against a document written without a single diacritic.</summary>
    [Fact]
    public void A_spanish_document_without_diacritics_is_reported()
    {
        var root = Path.Combine(Path.GetTempPath(), "accents-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            var kept = Path.Combine(root, "KEPT.es.md");
            var flattened = Path.Combine(root, "FLAT.es.md");
            File.WriteAllText(kept, "# Guía\n\nLa aplicación reproduce vídeos.\n");
            File.WriteAllText(flattened, "# Guia\n\nLa aplicacion reproduce videos.\n");

            Assert.Equal([flattened], Flattened([kept, flattened]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] Flattened(IEnumerable<string> files) =>
    [
        .. files.Where(file => !File.ReadAllText(file)
            .Any(character => "áéíóúñÁÉÍÓÚÑ¿¡".Contains(character, StringComparison.Ordinal))),
    ];

    private static string[] HeadingShape(string path) =>
        File.Exists(path)
            ? [.. Heading.Matches(File.ReadAllText(path)).Select(match => match.Groups["level"].Value)]
            : [];
}
