// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

namespace ApSolutions.LocalMedia.ArchitectureTests;

public sealed class StableInternalIdentityTests
{
    [Fact]
    public void Persistent_identifiers_are_stable_and_brand_independent()
    {
        const string propsPath = "Directory.Build.props";
        var absolutePropsPath = RepositoryLayout.PathFromRoot(propsPath);
        Assert.True(File.Exists(absolutePropsPath), $"Required build configuration is missing: {propsPath}");

        var properties = XDocument.Load(absolutePropsPath)
            .Descendants()
            .Where(element => !element.HasElements)
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);

        Assert.Equal("APSolutions.LocalMedia", properties["LocalMediaPackageIdentity"]);
        Assert.Equal("apsolutions-localmedia", properties["LocalMediaUriScheme"]);
        Assert.DoesNotContain("Reelume", properties["LocalMediaPackageIdentity"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reelume", properties["LocalMediaUriScheme"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_assembly_names_use_the_ApSolutions_LocalMedia_root()
    {
        var sourceRoot = RepositoryLayout.PathFromRoot("src");
        Assert.True(Directory.Exists(sourceRoot), "The production source directory is missing: src");

        var projects = Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories);
        Assert.Equal(5, projects.Length);
        foreach (var project in projects)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(project);
            Assert.StartsWith("ApSolutions.LocalMedia.", assemblyName, StringComparison.Ordinal);
            Assert.DoesNotContain("Reelume", assemblyName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
