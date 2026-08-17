// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Windows.Updates;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Updates;

/// <summary>
/// The download a run that does not own this machine's profile is built with: the product's own
/// verified downloader, over a transport that never leaves the handover folder.
/// </summary>
/// <remarks>
/// What is asserted here is that the replacement is only the transport. The hash, the declared size
/// and the staging under <c>.partial</c> belong to <c>VerifiedUpdateDownloader</c>, so a package that
/// is not what the manifest promised has to be refused exactly as it would be for a person.
/// </remarks>
public sealed class HandoffUpdateDownloadTests : IDisposable
{
    private const string PackageFile = "apreelume-999.0.0.msix";
    private static readonly Uri Address = new($"https://updates.handoff.invalid/{PackageFile}");
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("a package, and nothing else");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"update-download-{Guid.NewGuid():N}");

    private string Handoff => Path.Combine(_root, "handoff");

    private string Staging => Path.Combine(_root, "updates");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task The_package_arrives_and_is_staged_under_its_real_name()
    {
        Seed();

        var staged = await new HandoffUpdateDownloader(Handoff, Staging)
            .DownloadAsync(Release(), progress: null, TestContext.Current.CancellationToken);

        Assert.EndsWith(PackageFile, staged.Path, StringComparison.Ordinal);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(staged.Path, TestContext.Current.CancellationToken));

        // Nothing is left half-arrived: the partial name is what the staging folder holds until the
        // hash and the size agree, and it is gone once they do.
        Assert.Empty(Directory.GetFiles(Staging, "*.partial", SearchOption.AllDirectories));
    }

    /// <summary>
    /// A package that is not what the manifest promised is refused, by the product's own check. This
    /// is the assertion that says the transport is all that was replaced.
    /// </summary>
    [Fact]
    public async Task A_package_that_is_not_what_was_promised_is_refused_and_deleted()
    {
        Seed();
        await File.WriteAllBytesAsync(
            Path.Combine(Handoff, PackageFile),
            Encoding.UTF8.GetBytes("a package, and something else"),
            TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<UpdateVerificationException>(
            () => new HandoffUpdateDownloader(Handoff, Staging)
                .DownloadAsync(Release(), progress: null, TestContext.Current.CancellationToken));

        Assert.Empty(Directory.Exists(Staging)
            ? Directory.GetFiles(Staging, "*", SearchOption.AllDirectories)
            : []);
    }

    /// <summary>
    /// Asking for a release the handover folder no longer describes is unreachable rather than a
    /// crash: the run had something to install and now cannot find out what.
    /// </summary>
    [Fact]
    public async Task A_download_with_no_manifest_left_says_it_cannot_be_reached()
    {
        Directory.CreateDirectory(Handoff);

        _ = await Assert.ThrowsAsync<UpdateSourceUnavailableException>(
            () => new HandoffUpdateDownloader(Handoff, Staging)
                .DownloadAsync(Release(), progress: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_downloader_with_nowhere_to_read_or_write_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentException>(() => new HandoffUpdateDownloader("  ", Staging));
        _ = Assert.Throws<ArgumentNullException>(() => new HandoffUpdateDownloader(null!, Staging));
        _ = Assert.Throws<ArgumentException>(() => new HandoffUpdateDownloader(Handoff, "  "));
        _ = Assert.Throws<ArgumentNullException>(() => new HandoffUpdateDownloader(Handoff, null!));
    }

    /// <summary>
    /// The transport answers the address the manifest declares, and nothing else — a transport that
    /// served whatever an address asked for would be a way to read this machine through a manifest.
    /// </summary>
    [Fact]
    public async Task The_transport_answers_only_what_this_run_was_offered()
    {
        Seed();
        using var client = new HttpClient(new HandoffUpdateTransport(Handoff));

        using var offered = await client.GetAsync(Address, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, offered.StatusCode);
        Assert.Equal(
            Payload,
            await offered.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        using var elsewhere = await client.GetAsync(
            new Uri("https://updates.handoff.invalid/something-else.msix"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
    }

    [Fact]
    public async Task A_transport_with_no_manifest_or_no_package_answers_nothing_found()
    {
        Directory.CreateDirectory(Handoff);
        using var client = new HttpClient(new HandoffUpdateTransport(Handoff));

        using var withoutManifest = await client.GetAsync(Address, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, withoutManifest.StatusCode);

        // Described but not there: the manifest promises a file the folder does not hold.
        WriteManifest();
        using var withoutPackage = await client.GetAsync(Address, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, withoutPackage.StatusCode);
    }

    [Fact]
    public async Task A_cancelled_request_is_never_answered()
    {
        Seed();
        using var client = new HttpClient(new HandoffUpdateTransport(Handoff));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync(Address, cancellation.Token));
    }

    [Fact]
    public void A_transport_with_nowhere_to_read_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentException>(() => new HandoffUpdateTransport("  "));
        _ = Assert.Throws<ArgumentNullException>(() => new HandoffUpdateTransport(null!));
    }

    private static UpdateRelease Release() => new(
        "999.0.0",
        "win-x64",
        Address.AbsoluteUri,
        Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant(),
        Payload.Length,
        "Lo que cambia.",
        "What changed.")
    {
        Sha256Signed = true,
    };

    private void Seed()
    {
        WriteManifest();
        File.WriteAllBytes(Path.Combine(Handoff, PackageFile), Payload);
    }

    private void WriteManifest()
    {
        Directory.CreateDirectory(Handoff);
        File.WriteAllText(
            Path.Combine(Handoff, HandoffUpdateManifest.FileName),
            string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                {
                  "version": "999.0.0",
                  "runtime": "win-x64",
                  "url": "{{Address.AbsoluteUri}}",
                  "sha256": "{{Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant()}}",
                  "sizeInBytes": {{Payload.Length}},
                  "summaryEs": "Lo que cambia.",
                  "summaryEn": "What changed.",
                  "packageFile": "{{PackageFile}}"
                }
                """));
    }
}
