// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

public sealed partial class BilingualDocumentationPairTests
{
    /// <summary>
    /// Fewer published documents in either language than this, and the sweep below is reading an
    /// empty or truncated tree rather than the repository: a sweep over nothing agrees with
    /// everything. Sixteen are published today — the two READMEs and seven pairs under docs.
    /// </summary>
    internal const int LanguageSuffixedFloor = 12;

    /// <summary>
    /// The privacy statement is a promise, and a promise that says different things in two languages is
    /// worse than one language. The section counts are pinned so a change to one side cannot ship
    /// without the other.
    /// </summary>
    [Fact]
    public void Privacy_statement_has_synchronized_Spanish_and_English_files()
    {
        var spanishPath = RepositoryLayout.PathFromRoot("docs/privacy/PRIVACY.es.md");
        var englishPath = RepositoryLayout.PathFromRoot("docs/privacy/PRIVACY.en.md");

        Assert.True(File.Exists(spanishPath), "Missing Spanish privacy statement.");
        Assert.True(File.Exists(englishPath), "Missing English privacy statement.");
        Assert.Equal(TopLevelSectionCount(spanishPath), TopLevelSectionCount(englishPath));
        Assert.True(TopLevelSectionCount(spanishPath) >= 8, "The privacy statement lost sections.");
    }

    /// <summary>
    /// Every published document with a language suffix has its counterpart in the other language.
    /// </summary>
    /// <remarks>
    /// It reads what the repository publishes and not what the disk holds, so a document kept out of
    /// the repository on one machine neither passes nor fails it there while a clone says otherwise.
    /// </remarks>
    [Fact]
    public void Every_language_suffixed_document_has_its_counterpart()
    {
        Assert.Empty(MissingCounterparts(PublishedFiles.All));
    }

    /// <summary>The sweep above is reading a repository, not an empty tree.</summary>
    [Fact]
    public void The_counterpart_sweep_reads_the_published_documents()
    {
        var suffixed = PublishedFiles.All.Count(IsLanguageSuffixed);

        Assert.True(
            suffixed >= LanguageSuffixedFloor,
            $"only {suffixed} published documents carry a language suffix, fewer than the "
                + $"{LanguageSuffixedFloor} this repository publishes, so the sweep is not reading it.");
    }

    /// <summary>
    /// The sweep itself, against a list it did not find: one pair that is complete, one document
    /// whose counterpart is missing, and one whose counterpart exists only on the disk.
    /// </summary>
    [Fact]
    public void A_counterpart_that_is_not_published_is_a_missing_counterpart()
    {
        var root = Path.Combine(Path.GetTempPath(), "bilingual-pairs-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            string[] names = ["GUIDE.es.md", "GUIDE.en.md", "ALONE.es.md", "HIDDEN.en.md", "HIDDEN.es.md"];
            foreach (var name in names)
            {
                File.WriteAllText(Path.Combine(root, name), "# x\n");
            }

            var published = names.Take(4).Select(name => Path.Combine(root, name)).ToArray();

            Assert.Equal(
                ["ALONE.es.md", "HIDDEN.en.md"],
                MissingCounterparts(published).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] MissingCounterparts(IReadOnlyCollection<string> published)
    {
        var set = published.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return
        [
            .. published
                .Where(IsLanguageSuffixed)
                .Where(path => !set.Contains(Counterpart(path)))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static bool IsLanguageSuffixed(string path) =>
        path.EndsWith(".es.md", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".en.md", StringComparison.OrdinalIgnoreCase);

    private static string Counterpart(string path) =>
        path.EndsWith(".es.md", StringComparison.OrdinalIgnoreCase)
            ? path[..^6] + ".en.md"
            : path[..^6] + ".es.md";

    private static int TopLevelSectionCount(string path) =>
        File.ReadLines(path).Count(line => line.StartsWith("## ", StringComparison.Ordinal));
}
