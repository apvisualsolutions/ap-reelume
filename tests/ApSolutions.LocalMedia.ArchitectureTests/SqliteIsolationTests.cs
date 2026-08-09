using Xunit;

namespace ApSolutions.LocalMedia.ArchitectureTests;

public sealed class SqliteIsolationTests
{
    [Fact]
    public void Core_and_presentation_contracts_do_not_expose_SQLite_types()
    {
        var repositoryRoot = GetRepositoryRoot();
        var coreRoots = new[]
        {
            System.IO.Path.Combine(repositoryRoot, "src", "ApSolutions.LocalMedia.Domain"),
            System.IO.Path.Combine(repositoryRoot, "src", "ApSolutions.LocalMedia.Application"),
            System.IO.Path.Combine(repositoryRoot, "src", "ApSolutions.LocalMedia.Presentation"),
        };

        foreach (var root in coreRoots)
        {
            var project = Assert.Single(Directory.EnumerateFiles(root, "*.csproj"));
            Assert.DoesNotContain("Microsoft.Data.Sqlite", File.ReadAllText(project), StringComparison.Ordinal);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories),
                file => File.ReadAllText(file).Contains("Microsoft.Data.Sqlite", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Required_data_contracts_exist_under_stable_internal_namespaces()
    {
        var repositoryRoot = GetRepositoryRoot();
        var applicationRoot = System.IO.Path.Combine(
            repositoryRoot,
            "src",
            "ApSolutions.LocalMedia.Application");
        var expected = new[]
        {
            System.IO.Path.Combine(applicationRoot, "Data", "IUnitOfWork.cs"),
            System.IO.Path.Combine(applicationRoot, "Data", "IMigrationRunner.cs"),
            System.IO.Path.Combine(applicationRoot, "Data", "IDatabaseIntegrityChecker.cs"),
            System.IO.Path.Combine(applicationRoot, "Storage", "IAppDataPaths.cs"),
        };

        Assert.All(expected, path => Assert.True(File.Exists(path), $"Missing data contract: {path}"));
        Assert.All(
            expected,
            path =>
            {
                var source = File.ReadAllText(path);
                Assert.Contains("ApSolutions.LocalMedia", source, StringComparison.Ordinal);
                Assert.DoesNotContain("Reelume", source, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("SqliteConnection", source, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Windows_app_data_path_uses_only_the_stable_internal_identity()
    {
        var repositoryRoot = GetRepositoryRoot();
        var path = System.IO.Path.Combine(
            repositoryRoot,
            "src",
            "ApSolutions.LocalMedia.Windows",
            "AppDataPaths.cs");
        Assert.True(File.Exists(path), $"Missing Windows app-data adapter: {path}");
        var source = File.ReadAllText(path);
        Assert.Contains("APSolutions", source, StringComparison.Ordinal);
        Assert.Contains("LocalMedia", source, StringComparison.Ordinal);
        Assert.Contains("library.db", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Reelume", source, StringComparison.OrdinalIgnoreCase);
    }

    // The assertion that the composition selects the migration runner's constructor explicitly
    // lived here as a match on the source text — satisfiable by a comment or a coincidence of
    // characters. It moved to CompositionDescriptorTests (AccessibilityTests), which asserts the
    // registered descriptor's factory instead (ARQ-006).

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
