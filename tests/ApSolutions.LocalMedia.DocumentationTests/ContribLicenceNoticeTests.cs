// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Text.Json;
using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// Every third-party library linked inside the LibVLC plugins has its licence text in the artifact,
/// and the list of them is the list the build fetched.
/// </summary>
/// <remarks>
/// Until 2026-09-27 the notices said the engine was LGPL-2.1 and named three LGPL-3.0 libraries, while
/// its plugins linked sixty-nine third-party libraries, forty of them under licences — BSD, MIT, ISC,
/// IJG, the FreeType License — that ask for their copyright notice to travel with the binary. None
/// did. The inventory was read out of the engine's own source archive, each licence from the library's
/// own text and each «ships» from the published binaries, and <c>eng/libvlc/contrib-licences.json</c>
/// is what it produced; this test holds the notice to it.
/// </remarks>
public sealed class ContribLicenceNoticeTests
{
    private const string InventoryPath = "eng/libvlc/contrib-licences.json";
    private const string NoticePath = "docs/release/licenses/NOTICE-LibVLC-contribs.txt";

    private static readonly string[] ShipsValues = ["yes", "headers-only", "no"];

    private static readonly Regex SectionHeading = new(
        @"^={100}\n(?<heading>.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    [Fact]
    public void Every_library_that_ships_has_its_own_section_in_the_notice()
    {
        var headings = Headings();
        var missing = Sources()
            .Where(source => source.Ships != "no")
            .Select(source => source.Heading)
            .Where(heading => !headings.Contains(heading))
            .ToArray();

        Assert.True(missing.Length == 0, $"{NoticePath} has no section for: {string.Join("; ", missing)}.");
    }

    [Fact]
    public void The_notice_has_no_section_the_inventory_does_not_ship()
    {
        // The other direction, so the notice cannot keep a library the engine stopped carrying — or
        // carry the text of one that never shipped, which says something false about the binary.
        var shipping = Sources().Where(source => source.Ships != "no").Select(source => source.Heading).ToHashSet(StringComparer.Ordinal);
        var extra = Headings().Where(heading => !shipping.Contains(heading)).ToArray();

        Assert.True(extra.Length == 0, $"{NoticePath} has sections nothing in the inventory ships: {string.Join("; ", extra)}.");
    }

    [Fact]
    public void Every_entry_says_whether_it_ships_and_why_when_it_is_not_plainly_linked()
    {
        var sources = Sources();

        // Anti-blindness floor: a file that failed to parse would pass every check below with nothing.
        Assert.True(sources.Count >= 70, $"the inventory lists only {sources.Count} sources.");
        Assert.Equal(sources.Count, sources.Select(source => source.Tarball).Distinct(StringComparer.Ordinal).Count());

        foreach (var source in sources)
        {
            Assert.Contains(source.Ships, ShipsValues);
            Assert.False(string.IsNullOrWhiteSpace(source.Licence), $"{source.Tarball} names no licence.");
            if (source.Ships != "yes")
            {
                Assert.False(string.IsNullOrWhiteSpace(source.Reason), $"{source.Tarball} is «{source.Ships}» without saying why.");
            }

            if (source.Ships != "no")
            {
                Assert.True(
                    source.Text?.StartsWith(source.Tarball + ":", StringComparison.Ordinal) == true,
                    $"{source.Tarball} does not say which file of its own archive its text came from.");
            }
        }
    }

    [Fact]
    public void The_notice_travels_where_the_licences_travel_and_the_indexes_name_it()
    {
        Assert.True(File.Exists(RepositoryLayout.PathFromRoot(NoticePath)), $"{NoticePath} is missing.");
        foreach (var index in new[] { "docs/release/licenses/README.en.md", "docs/release/licenses/README.es.md" })
        {
            Assert.Contains(
                "NOTICE-LibVLC-contribs.txt",
                File.ReadAllText(RepositoryLayout.PathFromRoot(index)),
                StringComparison.Ordinal);
        }
    }

    private static HashSet<string> Headings()
    {
        var notice = File.ReadAllText(RepositoryLayout.PathFromRoot(NoticePath)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var headings = SectionHeading.Matches(notice).Select(match => match.Groups["heading"].Value).ToList();

        // A heading twice would let one section stand in for two libraries.
        Assert.Equal(headings.Count, headings.Distinct(StringComparer.Ordinal).Count());
        return headings.ToHashSet(StringComparer.Ordinal);
    }

    private static List<Source> Sources()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.PathFromRoot(InventoryPath)));
        return document.RootElement.GetProperty("sources").EnumerateArray()
            .Select(element => new Source(
                element.GetProperty("tarball").GetString()!,
                element.GetProperty("name").GetString()!,
                element.GetProperty("version").GetString()!,
                element.GetProperty("licence").GetString()!,
                element.GetProperty("ships").GetString()!,
                element.TryGetProperty("reason", out var reason) ? reason.GetString() : null,
                element.TryGetProperty("text", out var text) ? text.GetString() : null))
            .ToList();
    }

    private sealed record Source(string Tarball, string Name, string Version, string Licence, string Ships, string? Reason, string? Text)
    {
        public string Heading => $"{Name} {Version} - {Licence}";
    }
}
