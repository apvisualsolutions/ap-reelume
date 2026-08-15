// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// What Windows tells somebody about this application before they run it, and whether it tells them
/// in their own language (DES-001).
/// </summary>
/// <remarks>
/// <para>
/// The defect this suite exists for was not a missing declaration. The manifest declared
/// <c>es-ES</c> and <c>en-US</c> all along; its description was <b>one string with a slash inside
/// it</b> — "Biblioteca y reproductor de vídeo local / Local video library and player" — which
/// Windows shows exactly like that to both readers. A declared language localises nothing on its
/// own.
/// </para>
/// <para>
/// So the assertions are on what the sealed package carries, not on what the source says it
/// intends: the reference in the manifest, and the built resources dumped back out of the PRI.
/// </para>
/// </remarks>
public sealed class PackageDescriptionTests
{
    /// <summary>
    /// The description is a reference, and the name is not: "AP Reelume" is the product's name in
    /// both languages, and a brand that changes with the locale is a different product.
    /// </summary>
    [Fact]
    public void The_manifest_refers_to_a_resource_for_the_description_and_not_for_the_name()
    {
        var visual = PackageEvidence.Manifest()
            .Descendants()
            .Single(element => element.Name.LocalName == "VisualElements");

        var description = (string?)visual.Attribute("Description");
        Assert.Equal("ms-resource:AppDescription", description);

        var displayName = (string?)visual.Attribute("DisplayName");
        Assert.Equal("AP Reelume", displayName);
    }

    /// <summary>
    /// Every language the manifest declares is a language the built resources describe the product
    /// in, and each says something different — the whole point of the change.
    /// </summary>
    [Fact]
    public void The_sealed_package_describes_the_product_in_every_declared_language()
    {
        var declared = PackageEvidence.Manifest()
            .Descendants()
            .Where(element => element.Name.LocalName == "Resource")
            .Select(element => (string?)element.Attribute("Language"))
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(declared);

        var described = PackageEvidence.ReadReport("contents.json").GetProperty("describedIn");

        // The resource map is named after the package identity, because ms-resource:AppDescription
        // resolves against ms-resource://<identity>/Resources/AppDescription and nothing else.
        var identity = (string?)PackageEvidence.Manifest()
            .Descendants()
            .Single(element => element.Name.LocalName == "Identity")
            .Attribute("Name");
        Assert.Equal(identity, described.RequiredString("resourceMap"));

        var descriptions = described.GetProperty("descriptions")
            .EnumerateArray()
            .Select(entry => (Language: entry.RequiredString("language"), Text: entry.RequiredString("value")))
            .ToArray();

        Assert.Equal(declared, descriptions.Select(entry => entry.Language).Order(StringComparer.Ordinal));
        Assert.All(descriptions, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Text)));
        Assert.Equal(
            descriptions.Length,
            descriptions.Select(entry => entry.Text).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The resources travel inside the package. A reference whose resources stayed behind resolves
    /// to nothing, and nothing is what Windows would then show.
    /// </summary>
    [Fact]
    public void The_resources_the_description_points_at_travel_with_the_package()
    {
        var report = PackageEvidence.ReadReport("contents.json");
        var shipped = report.GetProperty("onlyInLayout").EnumerateArray().Select(entry => entry.GetString());
        Assert.Empty(shipped);

        var layout = Path.Combine(PackageEvidence.PackageRoot(), "layout", "resources.pri");
        Assert.True(
            File.Exists(layout),
            $"The payload carries no resources.pri, so ms-resource:AppDescription resolves to nothing. "
                + PackageEvidence.HowToProduce);
    }

    /// <summary>
    /// Both install channels say the same thing in the same language. The description is read from
    /// the READMEs by one script for exactly this reason: written out per channel, the two drift at
    /// the release nobody is watching.
    /// </summary>
    [Fact]
    public void The_package_and_the_winget_entry_describe_the_product_identically()
    {
        var descriptions = PackageEvidence.ReadReport("contents.json")
            .GetProperty("describedIn")
            .GetProperty("descriptions")
            .EnumerateArray()
            .ToDictionary(entry => entry.RequiredString("language"), entry => entry.RequiredString("value"));

        foreach (var (language, text) in descriptions)
        {
            var manifest = Directory
                .EnumerateFiles(PackageEvidence.PackageRoot(), $"*.locale.{language}.yaml", SearchOption.AllDirectories)
                .SingleOrDefault();
            if (manifest is null)
            {
                // winget publishes x64 only today, and only the locales the entry carries can be
                // compared. Saying so beats a silent pass.
                continue;
            }

            var shortDescription = File.ReadAllLines(manifest)
                .Single(line => line.StartsWith("ShortDescription: ", StringComparison.Ordinal))
                ["ShortDescription: ".Length..];
            Assert.Equal(text, shortDescription);
        }

        Assert.Contains("en-US", descriptions.Keys);
    }
}
