namespace ApSolutions.LocalMedia.DocumentationTests;

internal static class RepositoryLayout
{
    public static string Root { get; } = FindRoot();

    public static string PathFromRoot(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "FEATURES.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root containing docs/FEATURES.md was not found.");
    }
}
