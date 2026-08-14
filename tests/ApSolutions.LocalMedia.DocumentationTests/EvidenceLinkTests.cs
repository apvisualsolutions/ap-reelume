// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// Evidence that cannot be opened is not evidence. These checks follow every link the scope record
/// and the manifest make, and refuse the ones that lead nowhere.
/// </summary>
public sealed class EvidenceLinkTests
{
    [Fact]
    public void Every_evidence_document_the_matrix_links_exists()
    {
        var missing = FeatureMatrix.Rows
            .SelectMany(row => row.EvidenceLinks.Select(link => (row.Id, link)))
            .Where(entry => !File.Exists(RepositoryLayout.PathFromRoot(entry.link)))
            .Select(entry => $"{entry.Id} → {entry.link}")
            .ToArray();

        Assert.True(missing.Length == 0, $"Broken evidence links: {string.Join("; ", missing)}.");
    }

    [Fact]
    public void Every_evidence_document_the_manifest_links_exists()
    {
        var missing = new List<string>();
        foreach (var feature in FeatureMatrix.ManifestFeatures())
        {
            var id = feature.GetProperty("id").GetString()!;
            foreach (var link in feature.GetProperty("evidence").EnumerateArray().Select(entry => entry.GetString()!))
            {
                if (!File.Exists(RepositoryLayout.PathFromRoot(link)))
                {
                    missing.Add($"{id} → {link}");
                }
            }
        }

        Assert.True(missing.Count == 0, $"Broken manifest links: {string.Join("; ", missing)}.");
    }

    /// <summary>
    /// The two records have to point at the same documents. A manifest that links evidence the matrix
    /// does not is a second, quieter scope record.
    /// </summary>
    [Fact]
    public void The_manifest_and_the_matrix_link_the_same_evidence()
    {
        var matrix = FeatureMatrix.Mvp.ToDictionary(
            row => row.Id,
            row => row.EvidenceLinks.Order(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);

        var drift = new List<string>();
        foreach (var feature in FeatureMatrix.ManifestFeatures())
        {
            var id = feature.GetProperty("id").GetString()!;
            var fromManifest = feature.GetProperty("evidence")
                .EnumerateArray()
                .Select(entry => entry.GetString()!)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!matrix[id].SequenceEqual(fromManifest, StringComparer.Ordinal))
            {
                drift.Add($"{id}: matrix has {matrix[id].Length}, manifest has {fromManifest.Length}");
            }
        }

        Assert.True(drift.Count == 0, string.Join("; ", drift));
    }

    /// <summary>
    /// Relative links inside the documentation resolve. `verify-docs.ps1` checks this too; having it
    /// in the suite means a broken link fails the build a developer runs rather than only the gate.
    /// </summary>
    [Fact]
    public void Every_relative_link_in_the_documentation_resolves()
    {
        var docsRoot = RepositoryLayout.PathFromRoot("docs");
        var broken = new List<string>();

        foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var directory = Path.GetDirectoryName(file)!;
            foreach (var match in FeatureMatrix.LinkPattern.Matches(File.ReadAllText(file)))
            {
                var target = ((System.Text.RegularExpressions.Match)match).Groups["target"].Value.Trim();
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith('#'))
                {
                    continue;
                }

                var relative = Uri.UnescapeDataString(target.Split('#', 2)[0]);
                if (relative.Length == 0)
                {
                    continue;
                }

                if (!Path.Exists(Path.GetFullPath(Path.Combine(directory, relative))))
                {
                    broken.Add($"{Path.GetRelativePath(RepositoryLayout.Root, file)} → {target}");
                }
            }
        }

        Assert.True(broken.Count == 0, $"Broken links: {string.Join("; ", broken)}.");
    }

    /// <summary>
    /// The release-readiness report is what the Product Owner reads. It has to exist, name every
    /// unsettled commitment, and be reachable from the matrix rather than filed somewhere.
    /// </summary>
    [Fact]
    public void The_readiness_report_names_every_commitment_that_is_not_settled()
    {
        var path = RepositoryLayout.PathFromRoot("docs/evidence/mvp/release-readiness.md");
        Assert.True(File.Exists(path), "docs/evidence/mvp/release-readiness.md is missing.");
        var report = File.ReadAllText(path);

        var unmentioned = FeatureMatrix.Mvp
            .Where(row => !FeatureMatrix.SettledStatuses.Contains(row.Status))
            .Select(row => row.Id)
            .Where(id => !report.Contains(id, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            unmentioned.Length == 0,
            $"The readiness report does not mention: {string.Join(", ", unmentioned)}.");
    }

    /// <summary>
    /// The artifact the manifest describes is the artifact the release publishes, identified by the
    /// hash that was published with it.
    /// </summary>
    [Fact]
    public void The_manifest_identifies_each_artifact_by_its_published_hash()
    {
        foreach (var artifact in FeatureMatrix.Manifest().GetProperty("artifacts").EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(artifact.GetProperty("name").GetString()));
            Assert.Matches("^[0-9a-f]{64}$", artifact.GetProperty("sha256").GetString()!);
        }
    }

    /// <summary>
    /// The manifest describes the release it is filed with.
    /// </summary>
    /// <remarks>
    /// The hashes are not compared against a freshly built package on purpose: an MSIX records the
    /// moment it was sealed, so every build produces a different archive and the manifest would be
    /// permanently stale by construction. What must not drift is the version — a manifest naming
    /// another release's files is describing another release.
    /// </remarks>
    [Fact]
    public void The_manifest_names_the_artifacts_this_version_publishes()
    {
        var version = DeclaredVersion();
        var names = FeatureMatrix.Manifest()
            .GetProperty("artifacts")
            .EnumerateArray()
            .Select(artifact => artifact.GetProperty("name").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Ordinal order puts the package before the archive: 'P' sorts before 'p'.
        Assert.Equal(
            [$"APSolutions.LocalMedia_{version}_x64.msix", $"ApReelume-{version}-win-x64.zip"],
            names);
        Assert.Equal(version, FeatureMatrix.Manifest().GetProperty("version").GetString());
    }

    private static string DeclaredVersion()
    {
        var properties = System.Xml.Linq.XDocument.Load(RepositoryLayout.PathFromRoot("Directory.Build.props"));
        return properties
            .Descendants()
            .First(element => element.Name.LocalName == "Version")
            .Value
            .Trim();
    }
}
