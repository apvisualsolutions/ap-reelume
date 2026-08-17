// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Windows.Updates;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Updates;

/// <summary>
/// The source a run that does not own this machine's profile is built with: its own handover folder,
/// and never the network.
/// </summary>
public sealed class HandoffUpdateSourceTests : IDisposable
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"update-source-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// A folder with no manifest is a source that has published nothing — an answer, and not a
    /// failure. Reporting it as unreachable would make a run with nothing to install look like one
    /// that could not find out.
    /// </summary>
    [Fact]
    public async Task A_handover_folder_with_no_manifest_offers_nothing()
    {
        var source = new HandoffUpdateSource(_root);

        Assert.Null(await source.GetLatestAsync("win-x64", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_manifest_becomes_the_release_the_policy_judges()
    {
        Write();
        var source = new HandoffUpdateSource(_root);

        var release = await source.GetLatestAsync("win-x64", TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        Assert.Equal("999.0.0", release!.Version);
        Assert.Equal("win-x64", release.Runtime);
        Assert.Equal("https://updates.handoff.invalid/apreelume-999.0.0.msix", release.Url);
        Assert.Equal(Hash, release.Sha256);
        Assert.Equal(2048, release.SizeInBytes);
        Assert.False(string.IsNullOrWhiteSpace(release.SummaryEs));
        Assert.False(string.IsNullOrWhiteSpace(release.SummaryEn));
    }

    /// <summary>
    /// And the release it produces is one the policy offers, which is the whole point of the source
    /// existing: a manifest that could never be offered would make every control below it unreachable
    /// for a reason nobody could see.
    /// </summary>
    [Fact]
    public async Task The_release_it_produces_is_one_the_policy_offers()
    {
        Write();
        var release = await new HandoffUpdateSource(_root)
            .GetLatestAsync("win-x64", TestContext.Current.CancellationToken);

        var decision = UpdatePolicy.Decide(release, "0.1.0", "win-x64");

        Assert.True(decision.IsOffered, decision.Reason);
    }

    /// <summary>
    /// A manifest naming another architecture is still refused, by the policy and with its own
    /// reason. The source answers what it was given rather than filtering it away, so the refusal
    /// stays something somebody can read.
    /// </summary>
    [Fact]
    public async Task A_manifest_for_another_architecture_is_refused_by_the_policy()
    {
        Write(runtime: "win-arm64");
        var release = await new HandoffUpdateSource(_root)
            .GetLatestAsync("win-x64", TestContext.Current.CancellationToken);

        var decision = UpdatePolicy.Decide(release, "0.1.0", "win-x64");

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.WrongRuntime, decision.Rejection);
    }

    /// <summary>
    /// A manifest that is not one is unreachable, not empty. Collapsing the two would make a broken
    /// handover indistinguishable from a run with nothing to install.
    /// </summary>
    [Theory]
    // Not JSON; nothing in it; an address that is not one; a field explicitly null; and a size that
    // is not a number. Each one reaches this by a different route, and every route has to end here
    // rather than in whatever the caller happened to be doing.
    [InlineData("{ not json at all")]
    [InlineData("{}")]
    [InlineData("{\"version\":\"999.0.0\",\"runtime\":\"win-x64\",\"url\":\"not an address\"}")]
    [InlineData("{\"version\":null,\"runtime\":\"win-x64\",\"url\":\"https://x.invalid/p.msix\"}")]
    [InlineData("""
        {"version":"999.0.0","runtime":"win-x64","url":"https://x.invalid/p.msix",
         "sha256":"abc","sizeInBytes":"not a number"}
        """)]
    public async Task A_manifest_that_cannot_be_read_says_so_rather_than_offering_nothing(string content)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            Path.Combine(_root, HandoffUpdateManifest.FileName),
            content,
            TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<UpdateSourceUnavailableException>(
            () => new HandoffUpdateSource(_root).GetLatestAsync("win-x64", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A manifest that is there and cannot be opened is unreachable too, for the same reason: the
    /// run has something to install and cannot find out what.
    /// </summary>
    [Fact]
    public void A_manifest_that_cannot_be_opened_is_unreachable_rather_than_a_crash()
    {
        Write();
        using var held = new FileStream(
            Path.Combine(_root, HandoffUpdateManifest.FileName),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        _ = Assert.Throws<UpdateSourceUnavailableException>(() => HandoffUpdateManifest.Read(_root));
    }

    [Fact]
    public async Task A_cancelled_request_answers_nothing_at_all()
    {
        Write();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new HandoffUpdateSource(_root).GetLatestAsync("win-x64", cancellation.Token));
    }

    [Fact]
    public async Task A_source_with_nowhere_to_read_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentException>(() => new HandoffUpdateSource("   "));
        _ = Assert.Throws<ArgumentNullException>(() => new HandoffUpdateSource(null!));
        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => new HandoffUpdateSource(_root).GetLatestAsync("  ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_manifest_read_from_nowhere_refuses_to_be_read()
    {
        _ = Assert.Throws<ArgumentException>(() => HandoffUpdateManifest.Read("   "));
        _ = Assert.Throws<ArgumentNullException>(() => HandoffUpdateManifest.Read(null!));
    }

    private void Write(string runtime = "win-x64")
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, HandoffUpdateManifest.FileName),
            string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                {
                  "version": "999.0.0",
                  "runtime": "{{runtime}}",
                  "url": "https://updates.handoff.invalid/apreelume-999.0.0.msix",
                  "sha256": "{{Hash}}",
                  "sizeInBytes": 2048,
                  "summaryEs": "Lo que cambia.",
                  "summaryEn": "What changed.",
                  "packageFile": "apreelume-999.0.0.msix"
                }
                """));
    }
}
