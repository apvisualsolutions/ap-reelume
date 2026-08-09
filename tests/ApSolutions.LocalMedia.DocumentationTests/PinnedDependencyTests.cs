using System.Text.Json;
using System.Xml.Linq;

namespace ApSolutions.LocalMedia.DocumentationTests;

public sealed class PinnedDependencyTests
{
    [Fact]
    public void Dotnet_SDK_is_pinned_to_an_exact_stable_version()
    {
        var globalJsonPath = RepositoryLayout.PathFromRoot("global.json");
        Assert.True(File.Exists(globalJsonPath), "global.json is missing.");

        using var document = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
        var sdk = document.RootElement.GetProperty("sdk");
        Assert.Matches(@"^\d+\.\d+\.\d+$", sdk.GetProperty("version").GetString()!);
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void Package_versions_are_central_and_exact()
    {
        var propsPath = RepositoryLayout.PathFromRoot("Directory.Packages.props");
        Assert.True(File.Exists(propsPath), "Directory.Packages.props is missing.");

        var document = XDocument.Load(propsPath);
        var packageVersions = document.Descendants("PackageVersion").ToArray();
        Assert.NotEmpty(packageVersions);
        Assert.Contains(packageVersions, package => package.Attribute("Include")?.Value == "Avalonia" && package.Attribute("Version")?.Value.StartsWith("12.1.", StringComparison.Ordinal) == true);
        Assert.Contains(packageVersions, package => package.Attribute("Include")?.Value == "LibVLCSharp" && package.Attribute("Version")?.Value.StartsWith("3.", StringComparison.Ordinal) == true);

        foreach (var package in packageVersions)
        {
            var version = package.Attribute("Version")?.Value;
            Assert.Matches(@"^\d+\.\d+\.\d+(?:\.\d+)?$", version!);
        }

        var projectVersionAttributes = Directory.GetFiles(RepositoryLayout.Root, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference"))
            .Where(reference => reference.Attribute("Version") is not null)
            .ToArray();
        Assert.Empty(projectVersionAttributes);
    }

    [Fact]
    public void Every_project_has_a_dependency_lock_file()
    {
        var projectPaths = Directory.GetFiles(RepositoryLayout.Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(projectPaths);

        foreach (var projectPath in projectPaths)
        {
            var lockPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "packages.lock.json");
            Assert.True(File.Exists(lockPath), $"Missing lock file for {Path.GetRelativePath(RepositoryLayout.Root, projectPath)}");
        }
    }
}
