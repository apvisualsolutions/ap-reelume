namespace ApSolutions.LocalMedia.DocumentationTests;

public sealed class BilingualDocumentationPairTests
{
    [Fact]
    public void Development_guide_has_synchronized_Spanish_and_English_files()
    {
        var spanishPath = RepositoryLayout.PathFromRoot("docs/development/README.es.md");
        var englishPath = RepositoryLayout.PathFromRoot("docs/development/README.en.md");

        Assert.True(File.Exists(spanishPath), "Missing Spanish development guide.");
        Assert.True(File.Exists(englishPath), "Missing English development guide.");
        Assert.Equal(TopLevelSectionCount(spanishPath), TopLevelSectionCount(englishPath));
    }

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

    [Fact]
    public void Every_language_suffixed_document_has_its_counterpart()
    {
        var docsRoot = RepositoryLayout.PathFromRoot("docs");
        var localizedFiles = Directory.GetFiles(docsRoot, "*.md", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".es.md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".en.md", StringComparison.OrdinalIgnoreCase));

        foreach (var localizedFile in localizedFiles)
        {
            var counterpart = localizedFile.EndsWith(".es.md", StringComparison.OrdinalIgnoreCase)
                ? localizedFile[..^6] + ".en.md"
                : localizedFile[..^6] + ".es.md";
            Assert.True(File.Exists(counterpart), $"Missing bilingual counterpart for {Path.GetRelativePath(RepositoryLayout.Root, localizedFile)}");
        }
    }

    private static int TopLevelSectionCount(string path) =>
        File.ReadLines(path).Count(line => line.StartsWith("## ", StringComparison.Ordinal));
}
