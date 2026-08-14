// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

public sealed class ArchitectureDependencyTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedProjectReferences =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["src/ApSolutions.LocalMedia.Domain/ApSolutions.LocalMedia.Domain.csproj"] = [],
            ["src/ApSolutions.LocalMedia.Application/ApSolutions.LocalMedia.Application.csproj"] =
                ["src/ApSolutions.LocalMedia.Domain/ApSolutions.LocalMedia.Domain.csproj"],
            ["src/ApSolutions.LocalMedia.Infrastructure/ApSolutions.LocalMedia.Infrastructure.csproj"] =
                [
                    "src/ApSolutions.LocalMedia.Application/ApSolutions.LocalMedia.Application.csproj",
                    "src/ApSolutions.LocalMedia.Domain/ApSolutions.LocalMedia.Domain.csproj",
                ],
            ["src/ApSolutions.LocalMedia.Presentation/ApSolutions.LocalMedia.Presentation.csproj"] =
                ["src/ApSolutions.LocalMedia.Application/ApSolutions.LocalMedia.Application.csproj"],
            ["src/ApSolutions.LocalMedia.Windows/ApSolutions.LocalMedia.Windows.csproj"] =
                [
                    "src/ApSolutions.LocalMedia.Application/ApSolutions.LocalMedia.Application.csproj",
                    "src/ApSolutions.LocalMedia.Domain/ApSolutions.LocalMedia.Domain.csproj",
                    "src/ApSolutions.LocalMedia.Infrastructure/ApSolutions.LocalMedia.Infrastructure.csproj",
                    "src/ApSolutions.LocalMedia.Presentation/ApSolutions.LocalMedia.Presentation.csproj",
                ],
        };

    [Fact]
    public void Project_graph_matches_the_approved_dependency_rule()
    {
        foreach (var (projectPath, expectedReferences) in ExpectedProjectReferences)
        {
            var absoluteProjectPath = RepositoryLayout.PathFromRoot(projectPath);
            Assert.True(File.Exists(absoluteProjectPath), $"Required project is missing: {projectPath}");

            var actualReferences = ProjectReferences(absoluteProjectPath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var expected = expectedReferences.Order(StringComparer.OrdinalIgnoreCase).ToArray();

            Assert.Equal(expected, actualReferences);
        }
    }

    [Fact]
    public void Domain_has_no_package_or_project_dependencies()
    {
        const string projectPath = "src/ApSolutions.LocalMedia.Domain/ApSolutions.LocalMedia.Domain.csproj";
        var absoluteProjectPath = RepositoryLayout.PathFromRoot(projectPath);
        Assert.True(File.Exists(absoluteProjectPath), $"Required project is missing: {projectPath}");

        var document = XDocument.Load(absoluteProjectPath);
        Assert.Empty(document.Descendants("ProjectReference"));
        Assert.Empty(document.Descendants("PackageReference"));
    }

    [Fact]
    public void Only_the_Windows_host_targets_Windows()
    {
        foreach (var projectPath in ExpectedProjectReferences.Keys)
        {
            var absoluteProjectPath = RepositoryLayout.PathFromRoot(projectPath);
            Assert.True(File.Exists(absoluteProjectPath), $"Required project is missing: {projectPath}");

            var targetFramework = XDocument.Load(absoluteProjectPath)
                .Descendants("TargetFramework")
                .Select(element => element.Value)
                .Single();
            var expected = projectPath.Contains(".Windows/", StringComparison.Ordinal)
                ? "net10.0-windows10.0.22621.0"
                : "net10.0";

            Assert.Equal(expected, targetFramework);
        }
    }

    private static IEnumerable<string> ProjectReferences(string absoluteProjectPath)
    {
        var projectDirectory = Path.GetDirectoryName(absoluteProjectPath)!;
        return XDocument.Load(absoluteProjectPath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => Path.GetFullPath(Path.Combine(projectDirectory, value!)))
            .Select(value => Path.GetRelativePath(RepositoryLayout.Root, value).Replace('\\', '/'));
    }
}
