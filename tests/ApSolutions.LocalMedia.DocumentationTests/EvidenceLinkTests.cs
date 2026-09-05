// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
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

    /// <summary>
    /// Nobody may cite a concession by number, because there is no numbered register of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On 2026-09-06 a set of working notes closed four parity candidates by citing «la cesión 11»,
    /// «la 12», «la 15» and «la 25». The section they pointed at —
    /// <c>docs/design/ELEMENTS.es.md</c> §«Las cesiones, con su razón» — is five unnumbered bullets,
    /// and no numbering of concessions exists anywhere in this tree.
    /// </para>
    /// <para>
    /// What makes it worth a gate rather than a correction is that the cited numbers do resolve to
    /// something: 4, 5 and 6 are points of the «Lo que sigue siendo distinto» section of
    /// <c>audit-prototype-fidelity-round-three.md</c>. Three local numberings had been concatenated
    /// into an imaginary global one, so the citation gets checked, finds something, and passes.
    /// </para>
    /// <para>
    /// The root cause is the word: «concession» is used in four senses here — those five bullets, a
    /// verdict of ADR-0007 («<b>Defect</b> unless a written concession»), any measured commitment in
    /// the changelog, and handover pointers. It sounds like a closed register and it is an
    /// adjective. This is born green with a real floor and turns red the day somebody reinvents the
    /// numbering inside the tree.
    /// </para>
    /// <para>
    /// Its blind side, stated rather than hidden: the notes that made the claim live outside the
    /// repository, under the user's profile. No gate here can see them. What this prevents is the
    /// invention reaching the tree.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_document_cites_a_concession_by_number()
    {
        // Both languages, because the English half of a bilingual document would carry the same
        // invention under its own word.
        var citation = new Regex(
            @"\b(cesi[oó]n|cesiones|concession|concessions)\s+(?:n[.º°]?\s*)?\d+",
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(5));

        // The one document allowed to write those citations, because writing them down is what it is
        // for: it archives the invention and explains why the numbers resolved to something. A gate
        // that could not quote the defect it exists to prevent would force the record to be vague.
        // This is a closed list: anything else that cites a concession by number is a new offence.
        string[] archives = ["docs/evidence/stable/audit-prototype-fidelity-round-four.md"];

        var offences = new List<string>();
        var read = 0;

        foreach (var document in Documents())
        {
            read++;
            var relativePath = Path.GetRelativePath(RepositoryLayout.Root, document).Replace('\\', '/');
            if (archives.Contains(relativePath, StringComparer.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(document);
            for (var index = 0; index < lines.Length; index++)
            {
                if (citation.IsMatch(lines[index]))
                {
                    offences.Add($"{relativePath}:{index + 1} → {lines[index].Trim()}");
                }
            }
        }

        // A sweep that reads nothing agrees with everything. The tree carried 289 Markdown files the
        // day this was written; a floor well below that catches a broken enumeration without
        // breaking on every new document.
        Assert.True(read >= 100, $"Only {read} documents were read; the sweep is measuring nothing.");
        Assert.True(
            offences.Count == 0,
            "There is no numbered register of concessions in this tree, so a citation by number "
                + "points at nothing — or worse, at a different local numbering that happens to "
                + "have that digit. Name the file and the reason instead: "
                + string.Join("; ", offences));
    }

    /// <summary>Every Markdown document of the tree, skipping build output and the design package.</summary>
    private static IEnumerable<string> Documents()
    {
        string[] roots = ["docs", "design", ".claude"];
        foreach (var relative in roots)
        {
            var directory = RepositoryLayout.PathFromRoot(relative);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
            {
                yield return path;
            }
        }

        foreach (var path in Directory.EnumerateFiles(RepositoryLayout.Root, "*.md", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }
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
