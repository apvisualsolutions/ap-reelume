// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Tests.Updates;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The release notes as the updater sees them.
/// </summary>
/// <remarks>
/// This does not check a format. It hands the generated notes to the real provider inside the real
/// release payload GitHub would return, and asks the real policy whether it would offer the result.
/// A format check would pass on notes that parse and describe nothing; this fails unless a person
/// running the published artifact would actually be offered this version.
/// <para>
/// It matters because the failure it guards against is silent. Notes missing a summary or a hash
/// produce a release the application refuses, and the symptom on every machine is the reassuring
/// one: "you already have the most recent version".
/// </para>
/// </remarks>
public sealed class ReleaseNotesTests
{
    private const string NotesFileName = "release-notes.md";

    [Fact]
    public async Task The_published_notes_are_ones_the_updater_would_offer()
    {
        var notes = Notes();
        var version = PackageEvidence.DeclaredVersion();
        var asset = $"APSolutions.LocalMedia_{version}_x64.msix";
        using var server = new FakeReleaseServer();
        server.Map("/repos/o/r/releases/latest", _ => FakeResponse.Json(ReleaseJson(notes, version, asset)));
        using var client = server.CreateClient();

        var release = await new GitHubReleaseUpdateProvider(client, "o", "r", SigningKey())
            .GetLatestAsync("win-x64", TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        var decision = UpdatePolicy.Decide(release, "0.0.1", "win-x64");
        Assert.True(decision.IsOffered, decision.Reason);
        Assert.Equal(PublishedHash(asset), release.Sha256);
        Assert.False(string.IsNullOrWhiteSpace(release.SummaryEs));
        Assert.False(string.IsNullOrWhiteSpace(release.SummaryEn));
    }

    /// <summary>
    /// The two summaries say different things. Generating both from one changelog, or pasting the
    /// same text twice, would satisfy every rule and leave one of the two languages unwritten.
    /// </summary>
    [Fact]
    public async Task The_two_summaries_are_actually_two()
    {
        var version = PackageEvidence.DeclaredVersion();
        using var server = new FakeReleaseServer();
        server.Map("/repos/o/r/releases/latest", _ => FakeResponse.Json(ReleaseJson(
            Notes(),
            version,
            $"APSolutions.LocalMedia_{version}_x64.msix")));
        using var client = server.CreateClient();

        var release = await new GitHubReleaseUpdateProvider(client, "o", "r", SigningKey())
            .GetLatestAsync("win-x64", TestContext.Current.CancellationToken);

        Assert.NotEqual(release!.SummaryEs, release.SummaryEn);
    }

    /// <summary>
    /// The hashes in the notes are the ones that accompany the files. A copy that drifted would be
    /// verified against the wrong value, and every download would be refused as tampered.
    /// </summary>
    [Fact]
    public void Every_published_hash_appears_in_the_notes_exactly_as_it_was_computed()
    {
        var notes = Notes();
        var sums = File.ReadAllLines(Path.Combine(PackageEvidence.PackageRoot(), "SHA256SUMS.txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        Assert.NotEmpty(sums);
        foreach (var line in sums)
        {
            Assert.Contains(line.Trim(), notes, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A release for another architecture is not offered, whatever the notes say. The notes describe
    /// every published file; the provider picks the one this installation could actually run.
    /// </summary>
    [Fact]
    public async Task The_notes_describe_every_file_and_the_updater_still_picks_only_its_own()
    {
        var version = PackageEvidence.DeclaredVersion();
        using var server = new FakeReleaseServer();
        server.Map("/repos/o/r/releases/latest", _ => FakeResponse.Json(ReleaseJson(
            Notes(),
            version,
            $"APSolutions.LocalMedia_{version}_x64.msix")));
        using var client = server.CreateClient();

        var arm = await new GitHubReleaseUpdateProvider(client, "o", "r", SigningKey())
            .GetLatestAsync("win-arm64", TestContext.Current.CancellationToken);

        Assert.Null(arm);
    }

    private static string Notes() => NotesAndKey().Notes;

    private static string SigningKey() => NotesAndKey().PublicKey;

    /// <summary>
    /// The generated notes, with the public key their signature answers to. Notes signed by the
    /// release pipeline are verified against the key the binary embeds; a local build whose key
    /// never left its owner gets the run's ephemeral signature instead, so what this suite proves
    /// — that these notes would be offered — holds either way, and the real signature's presence
    /// at publishing time is a prepare-release blocker.
    /// </summary>
    private static (string Notes, string PublicKey) NotesAndKey()
    {
        var path = Path.Combine(PackageEvidence.PackageRoot(), NotesFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{NotesFileName} is not in {PackageEvidence.PackageRoot()}, so nothing can be verified about "
                + $"what a release would tell the updater. {PackageEvidence.HowToProduce}",
                path);
        }

        var notes = File.ReadAllText(path);
        if (notes.Contains("untrusted comment:", StringComparison.Ordinal))
        {
            return (notes, UpdateSigningKey.PublicKey);
        }

        var sums = File.ReadAllLines(Path.Combine(PackageEvidence.PackageRoot(), "SHA256SUMS.txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim());
        var canonical = string.Join('\n', sums) + "\n";
        return (
            notes + "\n## Firma / Signature\n\n```\n"
                + TestReleaseSigning.SignatureFor(canonical).TrimEnd('\n') + "\n```\n",
            TestReleaseSigning.PublicKey);
    }

    private static string PublishedHash(string asset) => File
        .ReadAllLines(Path.Combine(PackageEvidence.PackageRoot(), "SHA256SUMS.txt"))
        .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Where(parts => parts.Length == 2 && parts[1].Equals(asset, StringComparison.OrdinalIgnoreCase))
        .Select(parts => parts[0].ToLowerInvariant())
        .Single();

    /// <summary>The payload GitHub returns for a published release, carrying these exact notes.</summary>
    private static string ReleaseJson(string notes, string version, string asset) =>
        $$"""
        {
          "tag_name": {{JsonSerializer.Serialize("v" + version)}},
          "draft": false,
          "prerelease": false,
          "body": {{JsonSerializer.Serialize(notes)}},
          "assets": [
            {
              "name": {{JsonSerializer.Serialize(asset)}},
              "size": 104857600,
              "browser_download_url": {{JsonSerializer.Serialize("https://example.invalid/" + asset)}}
            }
          ]
        }
        """;
}
