// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// Where the updater looks, checked against where the releases actually are.
/// </summary>
/// <remarks>
/// The owner and the repository are the entire address, and a wrong one has no symptom anybody would
/// notice. GitHub answers 404, the absence of a release is a settled answer rather than a failure,
/// and the application tells everybody they are up to date — forever, quietly, and with every test
/// still green, because every other test supplies its own server.
/// <para>
/// The changelog is what this compares against: it publishes the release address a person follows to
/// download the thing by hand. Two records that can disagree are one record and one rumour.
/// </para>
/// </remarks>
public sealed class UpdateSourceTests
{
    private static readonly Regex ReleasePattern = new(
        @"https://github\.com/(?<owner>[A-Za-z0-9._-]+)/(?<name>[A-Za-z0-9._-]+)/releases/",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    // The comparison of the declared address against the published one lived here, reading the
    // constants out of the composition root's text with a pattern. It moved to
    // CompositionDescriptorTests (AccessibilityTests), which resolves the update source the
    // application actually builds and asserts the address on the object (ARQ-006).

    /// <summary>
    /// Both changelogs point at the same place. They are maintained as a pair, and a release address
    /// that differed between them would mean one of the two languages sends people somewhere else.
    /// </summary>
    [Fact]
    public void Both_changelogs_publish_the_same_release_address()
    {
        Assert.Equal(PublishedSource("docs/CHANGELOG.es.md"), PublishedSource("docs/CHANGELOG.en.md"));
    }

    private static (string Owner, string Name) PublishedSource(string changelog)
    {
        var path = Path.Combine(RepositoryLayout.Root, changelog);
        Assert.True(File.Exists(path), $"{changelog} is missing.");
        var match = ReleasePattern.Match(File.ReadAllText(path));
        Assert.True(match.Success, $"{changelog} publishes no release address for anybody to follow.");
        return (match.Groups["owner"].Value, match.Groups["name"].Value);
    }
}
