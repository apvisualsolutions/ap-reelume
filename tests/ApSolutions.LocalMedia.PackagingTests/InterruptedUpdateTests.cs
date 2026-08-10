// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Application.Data;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Tests.Updates;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// What survives an update that goes wrong.
/// </summary>
/// <remarks>
/// The other suites ask whether the updater does the right thing. This one asks what is left behind
/// when it does not: after a download that dies on the wire, one that is cancelled, one that arrives
/// altered, and one that succeeds, the installation a person is currently using must be exactly as it
/// was — the running binary byte for byte, and a database that still passes its own integrity check.
/// <para>
/// That is the property an updater exists to preserve. An update that fails is an inconvenience; an
/// update that fails and takes the library with it is the reason people stop updating.
/// </para>
/// </remarks>
public sealed class InterruptedUpdateTests : IDisposable
{
    private const string AssetName = "APSolutions.LocalMedia_0.2.0_x64.msix";
    private const string AssetPath = "/download/" + AssetName;

    private static readonly byte[] Package = Encoding.UTF8.GetBytes(
        new string('u', 8192) + "-the-package-that-would-replace-this-one");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        "interrupted-updates",
        Guid.NewGuid().ToString("N"));

    private readonly string _installation;
    private readonly string _staging;
    private readonly string _activeBinary;
    private readonly string _databasePath;

    public InterruptedUpdateTests()
    {
        _installation = Path.Combine(_root, "installation");
        _staging = Path.Combine(_root, "staging");
        _activeBinary = Path.Combine(_installation, "ApSolutions.LocalMedia.Windows.exe");
        _databasePath = Path.Combine(_installation, "library.db");
        Directory.CreateDirectory(_installation);
        File.WriteAllBytes(_activeBinary, Encoding.UTF8.GetBytes("the binary that is currently running"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// The four outcomes, each asked the same two questions. Naming them one by one is what makes a
    /// failure say which of the four broke rather than that "the updater" did.
    /// </summary>
    [Theory]
    [InlineData("complete")]
    [InlineData("cancelled")]
    [InlineData("tampered")]
    [InlineData("interrupted")]
    public async Task The_running_installation_is_untouched_whatever_the_update_does(string outcome)
    {
        await CreateDatabaseAsync();
        var binaryBefore = await Sha256OfFileAsync(_activeBinary);
        var databaseBefore = await Sha256OfFileAsync(_databasePath);

        await RunAsync(outcome);

        Assert.Equal(binaryBefore, await Sha256OfFileAsync(_activeBinary));
        Assert.Equal(databaseBefore, await Sha256OfFileAsync(_databasePath));
        Assert.True(
            (await CheckIntegrityAsync()).IsValid,
            $"The database stopped passing its integrity check after a {outcome} update.");
        Assert.DoesNotContain(
            Directory.EnumerateFiles(_installation, "*", SearchOption.AllDirectories),
            file => Path.GetFileName(file).Contains("msix", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(file).EndsWith(".partial", StringComparison.Ordinal));
    }

    /// <summary>
    /// A connection that dies halfway leaves what arrived where a resume can find it, leaves nothing
    /// that could be mistaken for the finished package, and says how far it got.
    /// </summary>
    [Fact]
    public async Task A_download_that_dies_on_the_wire_keeps_what_arrived_and_finishes_nothing()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Truncated(Package));
        using var client = server.CreateClient();

        var interrupted = await Assert.ThrowsAsync<UpdateInterruptedException>(
            () => Download(client, Release(server), null));

        Assert.Equal(Package.Length / 2, interrupted.BytesReceived);
        Assert.Equal(Package.Length, interrupted.TotalBytes);
        Assert.DoesNotContain(StagedFiles(), file => Path.GetFileName(file) == AssetName);
        var partial = Assert.Single(StagedFiles());
        Assert.EndsWith(".partial", partial, StringComparison.Ordinal);
        Assert.Equal(Package.Length / 2, new FileInfo(partial).Length);
    }

    /// <summary>
    /// Resuming asks for the bytes that are missing rather than for the file again, and the result is
    /// the same file it would have been. A resume that produced something different would be worse
    /// than no resume at all, because the hash check is the only thing that would notice.
    /// </summary>
    [Fact]
    public async Task A_resumed_download_asks_only_for_what_is_missing_and_produces_the_same_package()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Truncated(Package));
        using var client = server.CreateClient();
        await Assert.ThrowsAsync<UpdateInterruptedException>(() => Download(client, Release(server), null));
        Resumable(server);

        var staged = await Download(client, Release(server), null);

        Assert.Equal(Package, await File.ReadAllBytesAsync(staged.Path, Cancellation));
        var resumed = Assert.Single(server.Requests, request => request.RangeStart is not null);
        Assert.Equal(Package.Length / 2, resumed.RangeStart!.Value);
    }

    /// <summary>
    /// A server that answers a range request with the whole file is entitled to. Appending that reply
    /// to what was already on disk would double the package and fail the hash for the wrong reason.
    /// </summary>
    [Fact]
    public async Task A_server_that_ignores_the_range_request_restarts_the_file_rather_than_doubling_it()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Truncated(Package));
        using var client = server.CreateClient();
        await Assert.ThrowsAsync<UpdateInterruptedException>(() => Download(client, Release(server), null));
        server.Map(AssetPath, _ => FakeResponse.Bytes(Package));

        var staged = await Download(client, Release(server), null);

        Assert.Equal(Package.Length, new FileInfo(staged.Path).Length);
        Assert.Equal(Package, await File.ReadAllBytesAsync(staged.Path, Cancellation));
    }

    /// <summary>
    /// A leftover longer than the package it claims to be is not a resumable download; it is a
    /// different file. It is thrown away rather than continued from a position that does not exist.
    /// </summary>
    [Fact]
    public async Task A_leftover_larger_than_the_declared_size_is_discarded_instead_of_resumed()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var partial = Path.Combine(_staging, "0.2.0", AssetName + ".partial");
        Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
        await File.WriteAllBytesAsync(partial, [.. Package, .. Package], Cancellation);

        var staged = await Download(client, Release(server), null);

        Assert.Equal(Package, await File.ReadAllBytesAsync(staged.Path, Cancellation));
        Assert.Null(Assert.Single(server.Requests).RangeStart);
    }

    /// <summary>
    /// A resumed download that completes into the wrong bytes is refused like any other, and the
    /// leftover goes with it: keeping it would make the next attempt resume from poisoned data.
    /// </summary>
    [Fact]
    public async Task A_resume_that_completes_into_the_wrong_bytes_is_refused_and_leaves_nothing_behind()
    {
        var altered = Package.ToArray();
        altered[^1] ^= 0xFF;
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Truncated(Package));
        using var client = server.CreateClient();
        await Assert.ThrowsAsync<UpdateInterruptedException>(() => Download(client, Release(server), null));
        server.Map(AssetPath, request => request.RangeStart is { } start
            ? FakeResponse.Partial(altered, start)
            : FakeResponse.Bytes(altered));

        await Assert.ThrowsAsync<UpdateVerificationException>(() => Download(client, Release(server), null));

        Assert.Empty(StagedFiles());
    }

    /// <summary>
    /// Progress is reported against the whole package, not against the part of it this attempt is
    /// fetching. A resumed download whose progress bar restarted at zero would be telling somebody
    /// their download went backwards.
    /// </summary>
    [Fact]
    public async Task Progress_on_a_resumed_download_counts_from_what_is_already_on_disk()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Truncated(Package));
        using var client = server.CreateClient();
        await Assert.ThrowsAsync<UpdateInterruptedException>(() => Download(client, Release(server), null));
        Resumable(server);
        var reported = new List<UpdateDownloadProgress>();

        _ = await Download(client, Release(server), new CollectingProgress(reported.Add));

        Assert.NotEmpty(reported);
        Assert.All(reported, progress => Assert.True(
            progress.BytesReceived >= Package.Length / 2,
            "A resumed download reported fewer bytes than were already on disk."));
        Assert.All(reported, progress => Assert.Equal(Package.Length, progress.TotalBytes));
    }

    /// <summary>
    /// The published package is around a hundred megabytes, so how long a download is allowed to take
    /// is not a detail. The client's timeout governs getting an answer, not receiving a body: a
    /// download slower than it must still finish, or nobody on a slow connection could ever update.
    /// </summary>
    /// <remarks>
    /// This is a property of asking for the response headers rather than the whole message. Reading
    /// the body eagerly would put the download under the same clock, and the failure would only ever
    /// appear on connections nobody develops on.
    /// </remarks>
    [Fact]
    public async Task A_download_slower_than_the_client_timeout_still_finishes()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Bytes(Package) with { SplitDelayMilliseconds = 1500 });
        using var client = server.CreateClient(TimeSpan.FromMilliseconds(600));

        var staged = await Download(client, Release(server), null);

        Assert.Equal(Package, await File.ReadAllBytesAsync(staged.Path, Cancellation));
    }

    /// <summary>
    /// The same clock does govern getting an answer at all. A source that never replies is not one to
    /// wait on forever.
    /// </summary>
    [Fact]
    public async Task A_source_that_never_answers_is_given_up_on()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.TooSlow());
        using var client = server.CreateClient(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAsync<UpdateSourceUnavailableException>(() =>
            Download(client, Release(server), null));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private async Task RunAsync(string outcome)
    {
        using var server = Server();
        using var client = server.CreateClient();
        if (outcome == "complete")
        {
            _ = await Download(client, Release(server), null);
            return;
        }

        if (outcome == "cancelled")
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Download(client, Release(server), null, cancellation.Token));
            return;
        }

        if (outcome == "tampered")
        {
            var altered = Package.ToArray();
            altered[0] ^= 0xFF;
            server.Map(AssetPath, _ => FakeResponse.Bytes(altered));
            await Assert.ThrowsAsync<UpdateVerificationException>(() => Download(client, Release(server), null));
            return;
        }

        server.Map(AssetPath, _ => FakeResponse.Truncated(Package));
        await Assert.ThrowsAsync<UpdateInterruptedException>(() => Download(client, Release(server), null));
    }

    private Task<StagedUpdate> Download(
        HttpClient client,
        UpdateRelease release,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken? cancellationToken = null) =>
        new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"])
            .DownloadAsync(release, progress, cancellationToken ?? Cancellation);

    private static FakeReleaseServer Server()
    {
        var server = new FakeReleaseServer();
        Resumable(server);
        return server;
    }

    private static void Resumable(FakeReleaseServer server) =>
        server.Map(AssetPath, request => request.RangeStart is { } start
            ? FakeResponse.Partial(Package, start)
            : FakeResponse.Bytes(Package));

    private static UpdateRelease Release(FakeReleaseServer server) => new(
        "0.2.0",
        "win-x64",
        $"{server.BaseAddress}download/{AssetName}",
        Convert.ToHexString(SHA256.HashData(Package)).ToLowerInvariant(),
        Package.Length,
        "Resumen bilingüe.",
        "Bilingual summary.");

    private IReadOnlyList<string> StagedFiles() =>
        Directory.Exists(_staging)
            ? [.. Directory.EnumerateFiles(_staging, "*", SearchOption.AllDirectories)]
            : [];

    private async Task CreateDatabaseAsync()
    {
        using var factory = new SqliteConnectionFactory(_databasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(Cancellation);
    }

    private async Task<DatabaseIntegrityResult> CheckIntegrityAsync()
    {
        using var factory = new SqliteConnectionFactory(_databasePath);
        return await new IntegrityChecker(factory).CheckAsync(Cancellation);
    }

    private static async Task<string> Sha256OfFileAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path, Cancellation);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class CollectingProgress(Action<UpdateDownloadProgress> report)
        : IProgress<UpdateDownloadProgress>
    {
        public void Report(UpdateDownloadProgress value) => report(value);
    }
}
