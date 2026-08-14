// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The privacy statement's connection table and the network purpose registry are two views of one
/// promise. When a component gains a network purpose, the statement has to say so in both languages
/// before the change can ship — otherwise the code tells the truth and the promise does not.
/// </summary>
/// <remarks>
/// This exists because it already happened once: the updater added two GitHub hosts to the registry
/// and the statement kept describing the application from before the updater existed.
/// </remarks>
public sealed class NetworkPurposeDocumentationTests
{
    private static readonly Regex DeclaredHostPattern = new(
        @"new NetworkPurpose\(\s*""[^""]+"",\s*""(?<host>[^""]+)""",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    // The redirect hosts a purpose declares are part of the same promise: the statement has to name
    // them too, or the registry tells the whole truth and the promise does not.
    private static readonly Regex AdditionalHostsPattern = new(
        @"AdditionalHosts:\s*\[(?<hosts>[^\]]*)\]",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    // A destination handed to the browser is not a connection, and it is still a place a person ends
    // up because of this application, so the statement names it under its own heading.
    private static readonly Regex HandedOffHostPattern = new(
        @"new HandedOffDestination\(\s*""[^""]+"",\s*""(?<host>[^""]+)""",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex QuotedHostPattern = new(
        @"""(?<host>[^""]+)""",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex TableHostPattern = new(
        @"(?m)^\|\s*`(?<host>[a-z0-9.*-]+)`\s*\|",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    public static TheoryData<string> PrivacyStatements => new(
        "docs/privacy/PRIVACY.es.md",
        "docs/privacy/PRIVACY.en.md");

    [Theory]
    [MemberData(nameof(PrivacyStatements))]
    public void The_privacy_statement_names_every_host_the_registry_declares_and_no_other(string statement)
    {
        var declared = DeclaredHosts();
        var documented = TableHostPattern
            .Matches(File.ReadAllText(RepositoryLayout.PathFromRoot(statement)))
            .Select(match => match.Groups["host"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(documented);

        var undocumented = declared.Except(documented, StringComparer.OrdinalIgnoreCase).Order().ToArray();
        Assert.True(
            undocumented.Length == 0,
            $"{statement} does not mention: {string.Join(", ", undocumented)}. "
            + "The registry declares them, so the promise has to.");

        var invented = documented.Except(declared, StringComparer.OrdinalIgnoreCase).Order().ToArray();
        Assert.True(
            invented.Length == 0,
            $"{statement} names hosts the registry never declared: {string.Join(", ", invented)}.");
    }

    /// <summary>
    /// Every host the registry writes down, connected to or handed off.
    /// </summary>
    /// <remarks>
    /// The handed-off ones are a separate list in the registry because they are a separate promise —
    /// the application never connects to them — but the statement still has to name them, and it
    /// still may not name anything else. Reading both lists is what keeps a destination from
    /// appearing in one place and not the other.
    /// </remarks>
    private static HashSet<string> DeclaredHosts()
    {
        var source = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Infrastructure/Privacy/NetworkPurposeRegistry.cs"));
        var hosts = DeclaredHostPattern
            .Matches(source)
            .Select(match => match.Groups["host"].Value)
            .Concat(HandedOffHostPattern
                .Matches(source)
                .Select(match => match.Groups["host"].Value))
            .Concat(AdditionalHostsPattern
                .Matches(source)
                .SelectMany(block => QuotedHostPattern
                    .Matches(block.Groups["hosts"].Value)
                    .Select(match => match.Groups["host"].Value)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(hosts);
        return hosts;
    }
}
