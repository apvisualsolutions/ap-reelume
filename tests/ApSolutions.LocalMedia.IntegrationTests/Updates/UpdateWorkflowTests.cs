using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Tests.Updates;
using ApSolutions.LocalMedia.Windows.Updates;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Updates;

/// <summary>
/// The whole path an update walks: a source is asked, a policy decides, bytes arrive, they are proved
/// to be the promised bytes, and only then does somebody get to say yes.
/// </summary>
/// <remarks>
/// Every case here runs against a real server over a real TLS connection, because the properties
/// worth defending — that a download can be resumed, that a truncated one is never mistaken for a
/// complete one, that a redirect cannot quietly drop to plain HTTP — do not exist above the socket.
/// <para>
/// The one thing no case is allowed to do is reach the launcher without a confirmation. That is the
/// claim `REL-003` actually makes, and it is asserted from both sides: the paths that must not launch
/// and the single path that may.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class UpdateWorkflowTests : IDisposable
{
    private const string InstalledVersion = "0.1.0";
    private const string Runtime = "win-x64";
    private const string AssetName = "APSolutions.LocalMedia_0.2.0_x64.msix";
    private const string LatestPath = "/repos/ap-solutions/ap-reelume/releases/latest";
    private const string AssetPath = "/download/" + AssetName;

    private static readonly byte[] Package = Encoding.UTF8.GetBytes(
        new string('p', 4096) + "-a-package-that-is-not-really-a-package");

    private readonly string _staging = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        "updates",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_staging))
        {
            Directory.Delete(_staging, recursive: true);
        }
    }

    [Fact]
    public async Task A_newer_release_is_offered_downloaded_verified_and_handed_over_only_once_confirmed()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.True(check.HasOffer, check.Decision?.Reason);
        Assert.Equal("0.2.0", check.Release!.Version);
        Assert.Equal("Corrige la reanudación en unidades desconectadas.", check.Release.SummaryEs);
        Assert.Equal("Fixes resume on disconnected drives.", check.Release.SummaryEn);
        Assert.Equal(Package.Length, check.Release.SizeInBytes);

        var reported = new List<UpdateDownloadProgress>();
        var confirm = Confirm(client, launcher);
        var staged = await confirm.StageAsync(
            check.Release,
            new CollectingProgress(reported.Add),
            Cancellation);

        Assert.True(File.Exists(staged.Path));
        Assert.Equal(Package, await File.ReadAllBytesAsync(staged.Path, Cancellation));
        Assert.Empty(launcher.Handed);
        Assert.NotEmpty(reported);
        Assert.All(reported, progress => Assert.Equal(Package.Length, progress.TotalBytes));

        var installation = await confirm.ExecuteAsync(
            staged,
            new UpdateConsent("0.2.0", DateTimeOffset.UnixEpoch),
            Cancellation);

        Assert.Equal(UpdateOutcome.HandedToWindows, installation.Outcome);
        Assert.Equal(staged.Path, Assert.Single(launcher.Handed));
    }

    /// <summary>
    /// The staged file never carries the name of the folder it came from into the application, and
    /// nothing about it lands outside the staging directory it was given.
    /// </summary>
    [Fact]
    public async Task The_download_stays_inside_the_staging_directory_it_was_given()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        var staged = await Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation);

        Assert.StartsWith(_staging, Path.GetFullPath(staged.Path), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AssetName, Path.GetFileName(staged.Path));
    }

    [Fact]
    public async Task An_absent_release_is_a_settled_answer_and_downloads_nothing()
    {
        using var server = new FakeReleaseServer()
            .Map(LatestPath, _ => FakeResponse.Status(HttpStatusCode.NotFound));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Answered, check.Status);
        Assert.False(check.HasOffer);
        Assert.Equal(UpdateRejection.NoReleaseAvailable, check.Decision!.Rejection);
        Assert.False(Directory.Exists(_staging));
    }

    /// <summary>
    /// A source that cannot be reached is not a source that said no. Collapsing the two would make an
    /// offline machine indistinguishable from an up-to-date one, which is the failure that lets a
    /// security fix sit unoffered for months.
    /// </summary>
    [Fact]
    public async Task A_source_that_cannot_be_reached_is_not_the_same_as_being_up_to_date()
    {
        using var server = Server();
        using var client = server.CreateClient();
        server.Dispose();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Unreachable, check.Status);
        Assert.Null(check.Decision);
        Assert.False(check.HasOffer);
    }

    [Fact]
    public async Task A_source_answering_html_is_unreachable_rather_than_a_crash()
    {
        // A captive portal answers 200 with a login page to every request. That is not a release
        // and not a reason to take the application down at startup: it is a source that could not
        // be reached, wearing a success code.
        using var server = new FakeReleaseServer()
            .Map(LatestPath, _ => FakeResponse.Json("<!DOCTYPE html><html><body>hotel wifi</body></html>"));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Unreachable, check.Status);
        Assert.False(check.HasOffer);
    }

    [Fact]
    public async Task A_server_error_is_unreachable_rather_than_an_answer()
    {
        using var server = new FakeReleaseServer()
            .Map(LatestPath, _ => FakeResponse.Status(HttpStatusCode.InternalServerError));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Unreachable, check.Status);
        Assert.False(check.HasOffer);
    }

    /// <summary>
    /// Nothing checks by itself. An automatic check with the setting off must not open a connection,
    /// which is the only version of that promise a test can actually observe.
    /// </summary>
    [Fact]
    public async Task An_automatic_check_contacts_nothing_while_the_setting_is_off()
    {
        using var server = Server();
        using var client = server.CreateClient();

        var check = await Check(client, automatic: false)
            .ExecuteAsync(UpdateCheckTrigger.Automatic, Cancellation);

        Assert.Equal(UpdateCheckStatus.NotAsked, check.Status);
        Assert.False(check.HasOffer);
        Assert.Empty(server.Requests);
    }

    /// <summary>Turning the setting on is what makes the automatic check reach anything.</summary>
    [Fact]
    public async Task An_automatic_check_runs_once_somebody_has_turned_it_on()
    {
        using var server = Server();
        using var client = server.CreateClient();

        var check = await Check(client, automatic: true)
            .ExecuteAsync(UpdateCheckTrigger.Automatic, Cancellation);

        Assert.True(check.HasOffer, check.Decision?.Reason);
        Assert.Single(server.Requests);
    }

    /// <summary>Asking is always allowed. The setting governs the checks nobody asked for.</summary>
    [Fact]
    public async Task A_requested_check_runs_even_with_automatic_checks_switched_off()
    {
        using var server = Server();
        using var client = server.CreateClient();

        var check = await Check(client, automatic: false)
            .ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.True(check.HasOffer, check.Decision?.Reason);
    }

    [Theory]
    [InlineData("v0.1.0", UpdateRejection.NotNewer)]
    [InlineData("v0.0.9", UpdateRejection.NotNewer)]
    public async Task A_release_that_is_not_newer_is_never_offered(string tag, UpdateRejection expected)
    {
        using var server = Server(Body(), tag);
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.False(check.HasOffer);
        Assert.Equal(expected, check.Decision!.Rejection);
    }

    /// <summary>
    /// A package for another architecture would install and then fail to start, and by then the
    /// working copy is gone. The source is asked for this runtime and answers for it or not at all.
    /// </summary>
    [Fact]
    public async Task A_release_that_carries_no_package_for_this_runtime_offers_nothing()
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(ReleaseJson(
            server.BaseAddress,
            "v0.2.0",
            Body(),
            assetName: "APSolutions.LocalMedia_0.2.0_arm64.msix")));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Answered, check.Status);
        Assert.False(check.HasOffer);
        Assert.Equal(UpdateRejection.NoReleaseAvailable, check.Decision!.Rejection);
    }

    /// <summary>
    /// A pre-release is not what this application ships. Offering one would replace a stable install
    /// with a candidate on the strength of a tag nobody promised anything about.
    /// </summary>
    [Theory]
    [InlineData("prerelease")]
    [InlineData("draft")]
    public async Task A_release_that_is_not_published_as_stable_is_never_offered(string flag)
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(ReleaseJson(
            server.BaseAddress,
            "v0.2.0",
            Body(),
            unstableFlag: flag)));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Answered, check.Status);
        Assert.False(check.HasOffer);
        Assert.Equal(UpdateRejection.NoReleaseAvailable, check.Decision!.Rejection);
    }

    /// <summary>
    /// Notes with no hash in them describe a package nobody can check. The offer stops at the policy,
    /// before a single byte is requested.
    /// </summary>
    [Fact]
    public async Task A_release_whose_notes_carry_no_hash_is_refused_before_anything_is_downloaded()
    {
        using var server = Server(Body(includeHash: false));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.False(check.HasOffer);
        Assert.Equal(UpdateRejection.UnusableHash, check.Decision!.Rejection);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task A_release_that_is_summarised_in_only_one_language_is_refused()
    {
        using var server = Server(Body(includeEnglish: false));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.False(check.HasOffer);
        Assert.Equal(UpdateRejection.IncompleteSummary, check.Decision!.Rejection);
    }

    /// <summary>
    /// The policy runs again before the download, not only when the offer was made. A caller that
    /// staged a release the policy refuses is a defect, and it fails loudly rather than downloading.
    /// </summary>
    [Fact]
    public async Task Staging_a_release_the_policy_refuses_downloads_nothing_and_says_which_rule_stopped_it()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var refused = new UpdateRelease(
            "0.2.0",
            Runtime,
            "http://127.0.0.1/insecure.msix",
            new string('a', 64),
            Package.Length,
            "Resumen",
            "Summary");

        var failure = await Assert.ThrowsAsync<UpdateRefusedException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(refused, null, Cancellation));

        Assert.Equal(UpdateRejection.InsecureDownload, failure.Rejection);
        Assert.Empty(server.Requests);
    }

    /// <summary>
    /// The download is what the notes promised or it is nothing. A package whose bytes hash to
    /// something else is deleted rather than kept for a person to be asked about.
    /// </summary>
    [Fact]
    public async Task A_package_whose_bytes_do_not_match_the_promised_hash_is_refused_and_deleted()
    {
        var tampered = Package.ToArray();
        tampered[10] ^= 0xFF;
        using var server = Server().Map(AssetPath, _ => FakeResponse.Bytes(tampered));
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        var failure = await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            Confirm(client, launcher).StageAsync(check.Release!, null, Cancellation));

        Assert.Equal(AssetName, failure.FileName);
        Assert.NotEqual(failure.ExpectedSha256, failure.ActualSha256);
        Assert.Empty(launcher.Handed);
        Assert.Empty(StagedFiles());
    }

    /// <summary>
    /// A body shorter than the size the notes declared is an interrupted download, and it must not be
    /// hashed and offered as if it had finished.
    /// </summary>
    [Fact]
    public async Task A_package_that_arrives_short_is_never_offered_as_complete()
    {
        using var server = Server().Map(AssetPath, _ => FakeResponse.Bytes(Package[..100]));
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        var failure = await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation));

        Assert.Equal(Package.Length, failure.ExpectedSize);
        Assert.Equal(100, failure.ActualSize);
        Assert.Empty(StagedFiles());
    }

    /// <summary>
    /// Cancelling leaves nothing anybody could mistake for a finished download, and hands nothing to
    /// Windows.
    /// </summary>
    [Fact]
    public async Task A_cancelled_download_finishes_nothing_and_hands_over_nothing()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Confirm(client, launcher).StageAsync(check.Release!, null, cancellation.Token));

        Assert.Empty(launcher.Handed);
        Assert.DoesNotContain(StagedFiles(), file => file.EndsWith(AssetName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Nothing is installed by arriving. Without a confirmation the verified package sits on disk and
    /// the launcher is never called at all.
    /// </summary>
    [Fact]
    public async Task A_verified_package_with_no_confirmation_is_never_handed_to_windows()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var confirm = Confirm(client, launcher);
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var staged = await confirm.StageAsync(check.Release!, null, Cancellation);

        var installation = await confirm.ExecuteAsync(staged, consent: null, Cancellation);

        Assert.Equal(UpdateOutcome.NotConfirmed, installation.Outcome);
        Assert.Empty(launcher.Handed);
        Assert.True(File.Exists(staged.Path), "Declining threw away the package instead of keeping it.");
    }

    /// <summary>
    /// A confirmation is for one version. Reusing yesterday's yes for today's package is how a
    /// confirmation dialogue becomes a formality.
    /// </summary>
    [Fact]
    public async Task A_confirmation_for_another_version_does_not_install_this_one()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var confirm = Confirm(client, launcher);
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var staged = await confirm.StageAsync(check.Release!, null, Cancellation);

        var installation = await confirm.ExecuteAsync(
            staged,
            new UpdateConsent("0.3.0", DateTimeOffset.UnixEpoch),
            Cancellation);

        Assert.Equal(UpdateOutcome.ConsentMismatch, installation.Outcome);
        Assert.Empty(launcher.Handed);
    }

    /// <summary>
    /// Verification happens twice: once when the bytes arrive and once immediately before they are
    /// handed over. Everything between those two moments is somebody else's disk.
    /// </summary>
    [Fact]
    public async Task A_package_altered_after_it_was_verified_is_not_handed_over()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var confirm = Confirm(client, launcher);
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var staged = await confirm.StageAsync(check.Release!, null, Cancellation);
        await File.WriteAllBytesAsync(staged.Path, [.. Package, 0x00], Cancellation);

        var installation = await confirm.ExecuteAsync(
            staged,
            new UpdateConsent("0.2.0", DateTimeOffset.UnixEpoch),
            Cancellation);

        Assert.Equal(UpdateOutcome.Tampered, installation.Outcome);
        Assert.Empty(launcher.Handed);
        Assert.False(File.Exists(staged.Path), "The altered package was left on disk.");
    }

    /// <summary>
    /// A package that vanished between the download and the confirmation is the same finding as one
    /// that was altered: what would be handed over is not what was verified.
    /// </summary>
    [Fact]
    public async Task A_package_that_disappeared_before_the_confirmation_is_not_handed_over()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var confirm = Confirm(client, launcher);
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var staged = await confirm.StageAsync(check.Release!, null, Cancellation);
        File.Delete(staged.Path);

        var installation = await confirm.ExecuteAsync(
            staged,
            new UpdateConsent("0.2.0", DateTimeOffset.UnixEpoch),
            Cancellation);

        Assert.Equal(UpdateOutcome.Tampered, installation.Outcome);
        Assert.Empty(launcher.Handed);
    }

    /// <summary>
    /// Windows can refuse. When it does, the application is still running and the verified package is
    /// still on disk, so the answer is "try again" rather than a half-replaced installation.
    /// </summary>
    [Fact]
    public async Task A_launch_windows_refuses_leaves_the_application_and_the_package_intact()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher { Accepts = false };
        var confirm = Confirm(client, launcher);
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var staged = await confirm.StageAsync(check.Release!, null, Cancellation);

        var installation = await confirm.ExecuteAsync(
            staged,
            new UpdateConsent("0.2.0", DateTimeOffset.UnixEpoch),
            Cancellation);

        Assert.Equal(UpdateOutcome.LaunchRefused, installation.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(installation.Reason));
        Assert.True(File.Exists(staged.Path), "A refused launch deleted the package it could not start.");
    }

    /// <summary>
    /// A redirect is another URL, so the rule that the download travels encrypted has to hold for the
    /// address that is actually fetched and not only for the one that was advertised.
    /// </summary>
    [Fact]
    public async Task A_redirect_that_leaves_https_is_refused()
    {
        using var server = Server()
            .Map(AssetPath, _ => FakeResponse.Redirect("http://127.0.0.1:1/plain.msix"));
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        var failure = await Assert.ThrowsAsync<UpdateRefusedException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation));

        Assert.Equal(UpdateRejection.InsecureDownload, failure.Rejection);
        Assert.Empty(StagedFiles());
    }

    /// <summary>
    /// GitHub answers an asset with a redirect to its own storage, so a downloader that refused every
    /// redirect would never download anything at all.
    /// </summary>
    [Fact]
    public async Task A_redirect_that_stays_on_https_is_followed()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Redirect("/storage/package.msix"));
        server.Map("/storage/package.msix", _ => FakeResponse.Bytes(Package));
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        var staged = await Confirm(client, new RecordingLauncher())
            .StageAsync(check.Release!, null, Cancellation);

        Assert.Equal(Package, await File.ReadAllBytesAsync(staged.Path, Cancellation));
        Assert.Contains(server.Requests, request => request.Path == "/storage/package.msix");
    }

    /// <summary>
    /// A redirect loop has to end somewhere. It is reported as a source that could not deliver the
    /// package rather than as a package that failed a rule: nothing was ever wrong with the release,
    /// and telling somebody their update is unusable would send them looking in the wrong place.
    /// </summary>
    [Fact]
    public async Task A_redirect_that_never_arrives_anywhere_is_given_up_on()
    {
        using var server = Server();
        server.Map(AssetPath, _ => FakeResponse.Redirect(AssetPath));
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        await Assert.ThrowsAsync<UpdateSourceUnavailableException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation));
        Assert.Empty(StagedFiles());
    }

    /// <summary>
    /// The launcher hands the package to Windows and does not pretend to have installed anything. A
    /// path Windows will not accept comes back as a refusal, which is what makes it recoverable.
    /// </summary>
    [Fact]
    public async Task The_windows_launcher_refuses_a_package_that_is_not_there_instead_of_throwing()
    {
        var launcher = new WindowsUpdateLauncher(_ => true);
        var missing = new StagedUpdate(
            Valid(),
            Path.Combine(_staging, "0.2.0", "not-written.msix"));

        Assert.False(await launcher.LaunchAsync(missing, Cancellation));
    }

    /// <summary>
    /// What the launcher hands over is the verified file itself, opened the way Windows opens a
    /// package. Nothing about the running installation is touched by asking.
    /// </summary>
    [Fact]
    public async Task The_windows_launcher_hands_windows_the_verified_file_and_nothing_else()
    {
        var handed = new List<string>();
        var launcher = new WindowsUpdateLauncher(path =>
        {
            handed.Add(path);
            return true;
        });
        var directory = Path.Combine(_staging, "0.2.0");
        Directory.CreateDirectory(directory);
        var packagePath = Path.Combine(directory, AssetName);
        await File.WriteAllBytesAsync(packagePath, Package, Cancellation);

        Assert.True(await launcher.LaunchAsync(new StagedUpdate(Valid(), packagePath), Cancellation));
        Assert.Equal(packagePath, Assert.Single(handed));
    }

    /// <summary>
    /// A source that answers too late has not answered. It is the same finding as one that never
    /// answered at all, and must not be reported as "you are up to date".
    /// </summary>
    [Fact]
    public async Task A_source_that_answers_too_late_is_unreachable_rather_than_an_answer()
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.TooSlow());
        using var client = server.CreateClient(TimeSpan.FromMilliseconds(250));

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Unreachable, check.Status);
        Assert.False(check.HasOffer);
    }

    /// <summary>
    /// A release with no tag has no version, and one with no assets has no package. Neither is a
    /// release this application can be replaced by, and neither is an error either.
    /// </summary>
    [Theory]
    [InlineData("{\"draft\":false,\"prerelease\":false,\"body\":\"\",\"assets\":[]}")]
    [InlineData("{\"tag_name\":\"\",\"draft\":false,\"prerelease\":false,\"body\":\"\",\"assets\":[]}")]
    [InlineData("{\"tag_name\":\"v0.2.0\",\"draft\":false,\"prerelease\":false,\"body\":\"\"}")]
    [InlineData("{\"tag_name\":\"v0.2.0\",\"draft\":false,\"prerelease\":false,\"body\":\"\",\"assets\":{}}")]
    public async Task A_release_that_describes_no_package_offers_nothing_without_failing(string payload)
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(payload));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.Equal(UpdateCheckStatus.Answered, check.Status);
        Assert.Equal(UpdateRejection.NoReleaseAvailable, check.Decision!.Rejection);
    }

    /// <summary>
    /// Notes that publish somebody else's checksum publish none of this package's. Matching on the
    /// file name rather than on the presence of a hash is what stops a release from being verified
    /// against the wrong line.
    /// </summary>
    [Fact]
    public async Task A_checksum_published_for_another_file_is_not_this_package_s()
    {
        using var server = Server(
            "## Español\n\nCambios.\n\n## English\n\nChanges.\n\n## SHA256SUMS\n\n"
            + new string('b', 64) + "  ApReelume-0.2.0-win-x64.zip\n");
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.False(check.HasOffer);
        Assert.Equal(UpdateRejection.UnusableHash, check.Decision!.Rejection);
    }

    /// <summary>
    /// The package is fetched from a source that has to still be there. When it is not, the download
    /// says so instead of producing a file.
    /// </summary>
    [Fact]
    public async Task A_package_whose_source_disappears_before_the_download_is_reported_as_unreachable()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        server.Dispose();

        await Assert.ThrowsAsync<UpdateSourceUnavailableException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation));
    }

    [Fact]
    public async Task A_package_the_server_will_not_serve_is_reported_as_unreachable()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        server.Map(AssetPath, _ => FakeResponse.Status(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<UpdateSourceUnavailableException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation));
        Assert.Empty(StagedFiles());
    }

    [Fact]
    public async Task A_package_that_arrives_too_slowly_is_reported_as_unreachable()
    {
        using var server = Server();
        using var client = server.CreateClient(TimeSpan.FromMilliseconds(250));
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        server.Map(AssetPath, _ => FakeResponse.TooSlow());

        await Assert.ThrowsAsync<UpdateSourceUnavailableException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation));
    }

    /// <summary>
    /// A leftover something else is holding open — a scanner, a backup agent, another copy of this
    /// application — stops the download. Discarding it is housekeeping that is allowed to fail, and
    /// what must not happen is a package appearing under the verified name anyway.
    /// </summary>
    [Fact]
    public async Task A_leftover_another_process_holds_open_stops_the_download_rather_than_being_overwritten()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var partial = Path.Combine(_staging, "0.2.0", AssetName + ".partial");
        Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
        await File.WriteAllBytesAsync(partial, [.. Package, .. Package], Cancellation);
        using var held = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.Read);

        await Assert.ThrowsAsync<IOException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(check.Release!, null, Cancellation));

        Assert.DoesNotContain(StagedFiles(), file => Path.GetFileName(file) == AssetName);
    }

    /// <summary>
    /// A staged path that is not a file is not a package. It is refused like any other thing that is
    /// not what was verified, and the attempt to tidy it up does not turn the refusal into a crash.
    /// </summary>
    [Fact]
    public async Task A_staged_path_that_is_not_a_file_at_all_is_refused_without_crashing()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var occupied = Path.Combine(_staging, "0.2.0", AssetName);
        Directory.CreateDirectory(occupied);

        var installation = await Confirm(client, launcher).ExecuteAsync(
            new StagedUpdate(check.Release!, occupied),
            new UpdateConsent("0.2.0", DateTimeOffset.UnixEpoch),
            Cancellation);

        Assert.Equal(UpdateOutcome.Tampered, installation.Outcome);
        Assert.Empty(launcher.Handed);
    }

    /// <summary>
    /// The same housekeeping, on the other side of the confirmation: a package that changed and
    /// cannot be removed is still refused rather than handed over.
    /// </summary>
    [Fact]
    public async Task An_altered_package_that_cannot_be_deleted_is_still_not_handed_over()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var launcher = new RecordingLauncher();
        var confirm = Confirm(client, launcher);
        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);
        var staged = await confirm.StageAsync(check.Release!, null, Cancellation);
        await File.WriteAllBytesAsync(staged.Path, [.. Package, 0x00], Cancellation);
        using var held = new FileStream(staged.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var installation = await confirm.ExecuteAsync(
            staged,
            new UpdateConsent("0.2.0", DateTimeOffset.UnixEpoch),
            Cancellation);

        Assert.Equal(UpdateOutcome.Tampered, installation.Outcome);
        Assert.Empty(launcher.Handed);
    }

    /// <summary>
    /// Every collaborator is required. A use case built without one would fail at the moment it was
    /// needed, which for an updater is the moment somebody confirmed something.
    /// </summary>
    [Fact]
    public void Neither_use_case_can_be_built_without_what_it_needs()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var source = new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey);
        var installed = new InstalledRelease(InstalledVersion, Runtime);
        var downloader = new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"]);
        var launcher = new RecordingLauncher();
        var settings = new FixedUpdateSettings(false);

        Assert.Throws<ArgumentNullException>(() => new CheckForUpdates(null!, installed, settings));
        Assert.Throws<ArgumentNullException>(() => new CheckForUpdates(source, null!, settings));
        Assert.Throws<ArgumentNullException>(() => new CheckForUpdates(source, installed, null!));
        Assert.Throws<ArgumentNullException>(() => new ConfirmUpdate(null!, launcher, installed));
        Assert.Throws<ArgumentNullException>(() => new ConfirmUpdate(downloader, null!, installed));
        Assert.Throws<ArgumentNullException>(() => new ConfirmUpdate(downloader, launcher, null!));
        Assert.Throws<ArgumentNullException>(() => new VerifiedUpdateDownloader(null!, _staging));
        Assert.Throws<ArgumentException>(() => new VerifiedUpdateDownloader(client, "  "));
        Assert.Throws<ArgumentNullException>(() => new GitHubReleaseUpdateProvider(null!, "o", "r"));
        Assert.Throws<ArgumentException>(() => new GitHubReleaseUpdateProvider(client, " ", "r"));
        Assert.Throws<ArgumentException>(() => new GitHubReleaseUpdateProvider(client, "o", " "));
        Assert.Throws<ArgumentNullException>(() => new WindowsUpdateLauncher(null!));
    }

    /// <summary>
    /// Nothing may be asked of the updater without something to ask about, and a runtime nobody
    /// named is not a runtime.
    /// </summary>
    [Fact]
    public async Task Nothing_can_be_staged_or_asked_for_without_something_to_ask_about()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var source = new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey);

        await Assert.ThrowsAsync<ArgumentException>(() => source.GetLatestAsync("  ", Cancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Confirm(client, new RecordingLauncher()).StageAsync(null!, null, Cancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Confirm(client, new RecordingLauncher()).ExecuteAsync(null!, null, Cancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"]).DownloadAsync(null!, null, Cancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new WindowsUpdateLauncher(_ => true).LaunchAsync(null!, Cancellation));
    }

    /// <summary>
    /// A handover Windows throws at is a handover that did not happen. It comes back as a refusal so
    /// the application stays usable and the package stays where it is.
    /// </summary>
    [Fact]
    public async Task A_handover_windows_throws_at_comes_back_as_a_refusal()
    {
        var directory = Path.Combine(_staging, "0.2.0");
        Directory.CreateDirectory(directory);
        var packagePath = Path.Combine(directory, AssetName);
        await File.WriteAllBytesAsync(packagePath, Package, Cancellation);
        var staged = new StagedUpdate(Valid(), packagePath);

        Assert.False(await new WindowsUpdateLauncher(_ => throw new System.ComponentModel.Win32Exception(2))
            .LaunchAsync(staged, Cancellation));
        Assert.False(await new WindowsUpdateLauncher(_ => throw new InvalidOperationException())
            .LaunchAsync(staged, Cancellation));
        Assert.True(File.Exists(packagePath), "A refused handover deleted the package.");
    }

    /// <summary>A handover nobody waited for is not attempted at all.</summary>
    [Fact]
    public async Task A_cancelled_handover_is_never_attempted()
    {
        var handed = 0;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WindowsUpdateLauncher(_ =>
            {
                handed++;
                return true;
            }).LaunchAsync(new StagedUpdate(Valid(), "unused"), cancellation.Token));

        Assert.Equal(0, handed);
    }

    /// <summary>
    /// The provider translates and never repairs. Every field a release can be missing, or can carry
    /// as the wrong kind of thing, arrives as an absence — and the policy is what refuses it by name.
    /// </summary>
    [Fact]
    public async Task Every_field_a_release_can_get_wrong_arrives_as_an_absence_rather_than_a_guess()
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(
            $$"""
            {
              "tag_name": {{Quote("v0.2.0")}},
              "assets": [
                { "size": 10 },
                { "name": "", "size": 10 },
                { "name": {{Quote(AssetName)}}, "size": "not a number" }
              ]
            }
            """));
        using var client = server.CreateClient();

        var release = await new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey)
            .GetLatestAsync(Runtime, Cancellation);

        Assert.NotNull(release);
        Assert.Equal("0.2.0", release.Version);
        Assert.Null(release.Url);
        Assert.Null(release.Sha256);
        Assert.Null(release.SummaryEs);
        Assert.Equal(0, release.SizeInBytes);
        Assert.Equal(
            UpdateRejection.InsecureDownload,
            UpdatePolicy.Decide(release, InstalledVersion, Runtime).Rejection);
    }

    /// <summary>
    /// A tag that is not a string is not a tag. It is read as absent rather than coerced into
    /// something the policy would then trust.
    /// </summary>
    [Fact]
    public async Task A_release_whose_fields_are_the_wrong_kind_of_thing_describes_nothing()
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(
            "{\"tag_name\": 20, \"draft\": false, \"assets\": [{\"name\": \"x_x64.msix\", \"size\": 1}]}"));
        using var client = server.CreateClient();

        Assert.Null(await new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey)
            .GetLatestAsync(Runtime, Cancellation));
    }

    /// <summary>
    /// A size that is a number but not a whole number of bytes, and a heading with nothing under it,
    /// are both absences. Reading them as anything else would let the policy check a package against
    /// a size nobody declared, or offer a summary that is a blank line.
    /// </summary>
    [Fact]
    public async Task A_fractional_size_and_an_empty_heading_are_absences_too()
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(
            $$"""
            {
              "tag_name": {{Quote("v0.2.0")}},
              "draft": false,
              "prerelease": false,
              "body": {{Quote("## Español\n\n   \n\n## English\n\nChanges.\n")}},
              "assets": [{ "name": {{Quote(AssetName)}}, "size": 1.5 }]
            }
            """));
        using var client = server.CreateClient();

        var release = await new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey)
            .GetLatestAsync(Runtime, Cancellation);

        Assert.NotNull(release);
        Assert.Equal(0, release.SizeInBytes);
        Assert.Null(release.SummaryEs);
        Assert.Equal("Changes.", release.SummaryEn);
    }

    /// <summary>
    /// Release notes are a changelog entry, and a changelog entry is written with `###` subheadings.
    /// Only a heading of the same level ends a section: treating a subheading as the next section
    /// would cut the summary at "Added", and a version that opens with one would arrive with no
    /// summary at all — and be refused for carrying none, on every machine, silently.
    /// </summary>
    [Theory]
    [InlineData("## Español\n\n### Añadido\n\n- Algo nuevo.\n\n## English\n\n### Added\n\n- Something new.\n")]
    [InlineData("## Español\n\nResumen.\n\n### Añadido\n\n- Algo.\n\n## English\n\nSummary.\n\n### Added\n\n- Thing.\n")]
    // A bare `##` is a heading of the same level with no name, so it ends the section it follows and
    // names none: a summary must not be able to leak into the one after it through a typo.
    [InlineData("## Español\n\n### Añadido\n\n- Algo nuevo.\n\n##\n\n## English\n\n### Added\n\n- Something new.\n")]
    public async Task Subheadings_inside_a_summary_belong_to_it_rather_than_ending_it(string body)
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(ReleaseJson(
            server.BaseAddress,
            "v0.2.0",
            body + TestReleaseSigning.SignedChecksumSections(
                string.Create(CultureInfo.InvariantCulture, $"{Sha256(Package)}  {AssetName}\n")))));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.True(check.HasOffer, check.Decision?.Reason);
        Assert.Contains("Añadido", check.Release!.SummaryEs, StringComparison.Ordinal);
        Assert.Contains("Added", check.Release.SummaryEn, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256SUMS", check.Release.SummaryEn, StringComparison.Ordinal);
    }

    /// <summary>
    /// The architecture is the last segment of the runtime identifier. A runtime that has no
    /// segments is used whole rather than producing an empty suffix that would match everything.
    /// </summary>
    [Theory]
    [InlineData("win", "APSolutions.LocalMedia_0.2.0_win.msix")]
    [InlineData("win-", "APSolutions.LocalMedia_0.2.0_win-.msix")]
    public async Task An_unusual_runtime_identifier_still_names_one_architecture(string runtime, string asset)
    {
        using var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(ReleaseJson(
            server.BaseAddress,
            "v0.2.0",
            Body(),
            assetName: asset)));
        using var client = server.CreateClient();

        var release = await new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey)
            .GetLatestAsync(runtime, Cancellation);

        Assert.Equal(runtime, release!.Runtime);
        Assert.EndsWith(asset, release.Url!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The downloader does not depend on anybody having run the policy first. A release with no
    /// version and no hash still lands in the staging directory under a name of its own, and is still
    /// refused rather than produced.
    /// </summary>
    [Fact]
    public async Task A_release_that_names_neither_a_version_nor_a_hash_is_still_refused_by_the_download()
    {
        using var server = Server();
        server.Map("/download/", _ => FakeResponse.Bytes(Package));
        using var client = server.CreateClient();
        var nameless = new UpdateRelease(
            null,
            Runtime,
            $"{server.BaseAddress}download/",
            null,
            Package.Length,
            null,
            null);

        var failure = await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"]).DownloadAsync(nameless, null, Cancellation));

        Assert.Equal("apreelume-update.msix", failure.FileName);
        Assert.Equal(string.Empty, failure.ExpectedSha256);
        Assert.Empty(StagedFiles());
    }

    /// <summary>An address that is not an address at all is refused before anything is opened.</summary>
    [Fact]
    public async Task An_address_that_cannot_be_parsed_is_refused()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var unparseable = Valid() with { Url = "not an address" };

        var failure = await Assert.ThrowsAsync<UpdateRefusedException>(() =>
            new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"]).DownloadAsync(unparseable, null, Cancellation));

        Assert.Equal(UpdateRejection.InsecureDownload, failure.Rejection);
        Assert.Empty(server.Requests);
    }

    /// <summary>
    /// An empty leftover is nothing to resume from. Asking a server to continue from byte zero is
    /// asking it for the file, so the request is made without a range at all.
    /// </summary>
    [Fact]
    public async Task An_empty_leftover_is_started_again_rather_than_resumed_from_nowhere()
    {
        using var server = Server();
        using var client = server.CreateClient();
        var partial = Path.Combine(_staging, "0.2.0", AssetName + ".partial");
        Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
        await File.WriteAllBytesAsync(partial, [], Cancellation);
        var release = Valid() with { Url = $"{server.BaseAddress}download/{AssetName}" };

        var staged = await new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"])
            .DownloadAsync(release, null, Cancellation);

        Assert.Equal(Package, await File.ReadAllBytesAsync(staged.Path, Cancellation));
        Assert.Null(Assert.Single(server.Requests).RangeStart);
    }

    /// <summary>
    /// SEC-004: the address the bytes actually come from has to be one the allowlist covers, on
    /// every hop. Here the first hop is allowed and the redirect leaves the list — to the same
    /// server under another name, which is exactly what a compromised source would look like from
    /// the socket's side.
    /// </summary>
    [Fact]
    public async Task A_redirect_that_leaves_the_allowed_hosts_is_refused()
    {
        using var server = Server();
        var elsewhere = new Uri(server.BaseAddress.ToString().Replace(
            "127.0.0.1",
            "localhost",
            StringComparison.Ordinal));
        _ = server.Map(AssetPath, _ => FakeResponse.Redirect($"{elsewhere}download-elsewhere/{AssetName}"));
        using var client = server.CreateClient();
        var release = Valid() with { Url = $"{server.BaseAddress}download/{AssetName}" };

        var refusal = await Assert.ThrowsAsync<UpdateRefusedException>(() =>
            new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"])
                .DownloadAsync(release, null, Cancellation));

        Assert.Equal(UpdateRejection.UndeclaredHost, refusal.Rejection);
        Assert.Empty(StagedFiles());
    }

    /// <summary>
    /// SEC-005: a server that keeps sending past the declared size is cut off as soon as the excess
    /// is seen, not after it has been written whole and hashed. The exception reports how far the
    /// write got, which is nowhere near what the server had in store.
    /// </summary>
    [Fact]
    public async Task A_package_larger_than_the_release_declared_is_cut_off_at_the_excess()
    {
        var oversized = new byte[300_000];
        Random.Shared.NextBytes(oversized);
        using var server = Server();
        _ = server.Map(AssetPath, _ => FakeResponse.Bytes(oversized));
        using var client = server.CreateClient();
        var release = Valid() with { Url = $"{server.BaseAddress}download/{AssetName}" };

        var verification = await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"])
                .DownloadAsync(release, null, Cancellation));

        Assert.True(
            verification.ActualSize < oversized.Length,
            $"The write ran to {verification.ActualSize} bytes; the excess was only seen at the end.");
        Assert.Empty(StagedFiles());
    }

    /// <summary>
    /// SEC-005: release metadata has a ceiling. A source that answers megabytes to "what is the
    /// latest release?" is not answering the question, whatever its bytes parse as.
    /// </summary>
    [Fact]
    public async Task Metadata_larger_than_a_release_description_reads_as_unreachable()
    {
        using var server = Server(body: new string('x', 1_200_000));
        using var client = server.CreateClient();

        await Assert.ThrowsAsync<UpdateSourceUnavailableException>(() =>
            new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey)
                .GetLatestAsync(Runtime, Cancellation));
    }

    /// <summary>
    /// SEC-003: a hash that travels unsigned next to the package it vouches for proves only that
    /// both came from the same answer. The refusal names the signature, not the hash — the hash is
    /// there, and saying it is not would send whoever publishes releases hunting the wrong absence.
    /// </summary>
    [Fact]
    public async Task A_release_whose_checksums_travel_unsigned_is_refused_by_that_name()
    {
        using var server = Server(body: Body(includeSignature: false));
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.False(check.HasOffer);
        Assert.Equal(UpdateRejection.UnsignedChecksums, check.Decision!.Rejection);
        Assert.False(Directory.Exists(_staging));
    }

    /// <summary>
    /// A signature is only worth what its key is: one from a key this binary does not embed, or
    /// one made over different bytes, leaves the checksums exactly as unsigned as no signature.
    /// </summary>
    [Fact]
    public async Task A_signature_from_another_key_or_over_other_bytes_proves_nothing()
    {
        var sums = $"{Sha256(Package)}  {AssetName}\n";
        var foreign = Body(includeSignature: false)
            + "\n## Firma / Signature\n\n```\n" + TestReleaseSigning.ForeignSignatureFor(sums) + "```\n";
        var overOtherBytes = Body(includeSignature: false)
            + "\n## Firma / Signature\n\n```\n" + TestReleaseSigning.SignatureFor("something else\n") + "```\n";

        foreach (var body in new[] { foreign, overOtherBytes })
        {
            using var server = Server(body: body);
            using var client = server.CreateClient();

            var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

            Assert.False(check.HasOffer);
            Assert.Equal(UpdateRejection.UnsignedChecksums, check.Decision!.Rejection);
        }
    }

    /// <summary>
    /// An unsigned checksum line elsewhere in the notes must not shadow the signed block. Notes
    /// rearranged that way stop verifying and the release is refused — never offered under the
    /// attacker's hash.
    /// </summary>
    [Fact]
    public async Task An_unsigned_line_cannot_shadow_the_signed_checksums()
    {
        var shadow = "```\n" + new string('0', 64) + $"  {AssetName}\n```\n\n";
        using var server = Server(body: shadow + Body());
        using var client = server.CreateClient();

        var check = await Check(client).ExecuteAsync(UpdateCheckTrigger.Requested, Cancellation);

        Assert.False(check.HasOffer, "A shadowed checksum block was still offered.");
        Assert.Equal(UpdateRejection.UnsignedChecksums, check.Decision!.Rejection);
    }

    /// <summary>A binary built without the embedded key trusts nothing, rather than everything.</summary>
    [Fact]
    public async Task A_provider_without_the_embedded_key_treats_every_release_as_unsigned()
    {
        using var server = Server();
        using var client = server.CreateClient();

        var release = await new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume")
            .GetLatestAsync(Runtime, Cancellation);

        Assert.NotNull(release);
        Assert.False(release.Sha256Signed);
        Assert.Equal(
            UpdateRejection.UnsignedChecksums,
            UpdatePolicy.Decide(release, InstalledVersion, Runtime).Rejection);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static CheckForUpdates Check(HttpClient client, bool automatic = false) => new(
        Provider(client),
        new InstalledRelease(InstalledVersion, Runtime),
        new FixedUpdateSettings(automatic));

    private ConfirmUpdate Confirm(HttpClient client, IUpdateLauncher launcher) => new(
        new VerifiedUpdateDownloader(client, _staging, ["127.0.0.1"]),
        launcher,
        new InstalledRelease(InstalledVersion, Runtime));

    private IReadOnlyList<string> StagedFiles() =>
        Directory.Exists(_staging)
            ? [.. Directory.EnumerateFiles(_staging, "*", SearchOption.AllDirectories)]
            : [];

    /// <summary>The provider under test, holding the run's ephemeral release-signing key.</summary>
    private static GitHubReleaseUpdateProvider Provider(HttpClient client) =>
        new(client, "ap-solutions", "ap-reelume", TestReleaseSigning.PublicKey);

    private static UpdateRelease Valid() => new(
        "0.2.0",
        Runtime,
        "https://127.0.0.1/x.msix",
        Sha256(Package),
        Package.Length,
        "Resumen",
        "Summary")
    {
        Sha256Signed = true,
    };

    private static FakeReleaseServer Server(string? body = null, string tag = "v0.2.0")
    {
        var server = new FakeReleaseServer();
        server.Map(LatestPath, _ => FakeResponse.Json(ReleaseJson(server.BaseAddress, tag, body ?? Body())));
        server.Map(AssetPath, request => request.RangeStart is { } start
            ? FakeResponse.Partial(Package, start)
            : FakeResponse.Bytes(Package));
        return server;
    }

    /// <summary>
    /// Release notes in the shape `docs/release/RELEASING` describes: both languages, and the same
    /// `SHA256SUMS` lines that accompany the files.
    /// </summary>
    private static string Body(bool includeHash = true, bool includeEnglish = true, bool includeSignature = true)
    {
        var notes = new StringBuilder();
        notes.Append("## Español\n\nCorrige la reanudación en unidades desconectadas.\n\n");
        if (includeEnglish)
        {
            notes.Append("## English\n\nFixes resume on disconnected drives.\n\n");
        }

        if (includeHash && includeSignature)
        {
            // The shape a signed publication carries: the checksum block and the detached
            // signature the release tooling appends over its exact bytes (SEC-003).
            notes.Append(TestReleaseSigning.SignedChecksumSections(
                string.Create(CultureInfo.InvariantCulture, $"{Sha256(Package)}  {AssetName}\n")));
        }
        else if (includeHash)
        {
            notes.Append(CultureInfo.InvariantCulture, $"## SHA256SUMS\n\n```\n{Sha256(Package)}  {AssetName}\n```\n");
        }

        return notes.ToString();
    }

    private static string ReleaseJson(
        Uri baseAddress,
        string tag,
        string body,
        string assetName = AssetName,
        string? unstableFlag = null) =>
        $$"""
        {
          "tag_name": {{Quote(tag)}},
          "draft": {{(unstableFlag == "draft" ? "true" : "false")}},
          "prerelease": {{(unstableFlag == "prerelease" ? "true" : "false")}},
          "body": {{Quote(body)}},
          "assets": [
            {
              "name": {{Quote(assetName)}},
              "size": {{Package.Length.ToString(CultureInfo.InvariantCulture)}},
              "browser_download_url": {{Quote($"{baseAddress}download/{assetName}")}}
            }
          ]
        }
        """;

    private static string Quote(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Reads what the view model would read, without a view model.</summary>
    private sealed class CollectingProgress(Action<UpdateDownloadProgress> report)
        : IProgress<UpdateDownloadProgress>
    {
        public void Report(UpdateDownloadProgress value) => report(value);
    }

    private sealed class FixedUpdateSettings(bool automatic) : IUpdateSettings
    {
        public bool AutomaticCheckEnabled { get; private set; } = automatic;

        public void SetAutomaticCheckEnabled(bool enabled) => AutomaticCheckEnabled = enabled;
    }

    /// <summary>Stands where Windows stands, and remembers everything it was asked to take.</summary>
    private sealed class RecordingLauncher : IUpdateLauncher
    {
        public List<string> Handed { get; } = [];

        public bool Accepts { get; init; } = true;

        public Task<bool> LaunchAsync(StagedUpdate staged, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(staged);
            if (!Accepts)
            {
                return Task.FromResult(false);
            }

            Handed.Add(staged.Path);
            return Task.FromResult(true);
        }
    }
}
