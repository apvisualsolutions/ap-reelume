// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Windows.Playback;
using ApSolutions.LocalMedia.Windows.Shell;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Playback;

/// <summary>
/// The external escape hatch must be safe by construction. These tests never start a handler: they
/// pin the argument contract and the refusal path, which are the parts that could damage a file or
/// compose a command line.
/// </summary>
public sealed class ExternalPlaybackLauncherTests
{
    [Fact]
    public async Task A_file_that_is_not_there_is_refused_without_starting_anything()
    {
        var launcher = new ShellExternalPlaybackLauncher();
        var missing = Path.Combine(Path.GetTempPath(), $"apsolutions-{Guid.NewGuid():N}.mkv");

        var launched = await launcher.TryLaunchAsync(missing, TestContext.Current.CancellationToken);

        Assert.False(launched);
        Assert.False(File.Exists(missing));
    }

    [Fact]
    public async Task An_empty_path_is_rejected_before_any_shell_call()
    {
        var launcher = new ShellExternalPlaybackLauncher();

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => launcher.TryLaunchAsync("   ", TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => launcher.TryLaunchAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The launcher hands a path to whatever Windows has registered for that extension, so the
    /// extension is the whole decision. Every caller filters by the approved containers today; this
    /// is the audit's point that the check belongs where the shell call is made, not in the good
    /// behaviour of everyone who might one day make it.
    /// </summary>
    [Theory]
    [InlineData(".exe")]
    [InlineData(".ps1")]
    [InlineData(".lnk")]
    [InlineData(".txt")]
    [InlineData("")]
    public async Task A_container_the_library_would_not_catalogue_is_refused_before_the_shell(string extension)
    {
        var launcher = new ShellExternalPlaybackLauncher();
        var path = Path.Combine(Path.GetTempPath(), $"apsolutions-{Guid.NewGuid():N}{extension}");
        await File.WriteAllBytesAsync(path, [0], TestContext.Current.CancellationToken);

        try
        {
            var launched = await launcher.TryLaunchAsync(path, TestContext.Current.CancellationToken);

            Assert.False(launched);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The list the refusal above is made of. It is asserted here rather than restated in the
    /// launcher, because two lists would drift and the scanner's is the one that decides what the
    /// library holds.
    /// </summary>
    /// <remarks>
    /// The other half — an approved container reaching the shell — is deliberately not driven: this
    /// suite starts no handler, and a test that opened a real player on the machine running it would
    /// be worse than the coverage it bought.
    /// </remarks>
    [Fact]
    public void The_refusal_is_made_of_the_library_own_list_of_containers()
    {
        Assert.All(MediaFileExtensions.All, extension => Assert.True(MediaFileExtensions.IsApproved(extension)));
        Assert.False(MediaFileExtensions.IsApproved(".exe"));
        Assert.False(MediaFileExtensions.IsApproved(string.Empty));
    }

    [Fact]
    public async Task A_cancelled_request_never_reaches_the_shell()
    {
        var launcher = new ShellExternalPlaybackLauncher();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => launcher.TryLaunchAsync(@"C:\does-not-matter.mkv", cancellation.Token));
    }

    /// <summary>
    /// The stand-in an isolated run is built with instead, held to the same contract — every refusal
    /// above, plus the half the tests above deliberately cannot drive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is asserted here, beside the real one, because that is the claim: what the recorder writes
    /// down only says something about the real launcher while the two refuse the same things. A
    /// recorder that accepted a `.ps1` would make every probe reading its record meaningless.
    /// </para>
    /// <para>
    /// And it is one test rather than five on purpose. Merged Cobertura reports keep the better of
    /// the two readings per line rather than their union, so a branch split across suites reads as
    /// half-covered for ever; this file owns every branch of that class.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_recorder_refuses_everything_the_shell_launcher_refuses()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"apsolutions-handoff-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            var handoff = new RecordingSystemHandoff(folder);
            var launcher = new RecordingExternalPlaybackLauncher(handoff);

            // A recorder with nowhere to write is not a quieter recorder, it is a launcher that
            // silently hands nothing over while a probe reads an empty record and calls it a refusal.
            _ = Assert.Throws<ArgumentNullException>(() => new RecordingExternalPlaybackLauncher(null!));

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => launcher.TryLaunchAsync("   ", TestContext.Current.CancellationToken));
            _ = await Assert.ThrowsAsync<ArgumentNullException>(
                () => launcher.TryLaunchAsync(null!, TestContext.Current.CancellationToken));

            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => launcher.TryLaunchAsync(@"C:\does-not-matter.mkv", cancellation.Token));

            var script = Path.Combine(folder, "install.ps1");
            await File.WriteAllTextAsync(script, "whoami", TestContext.Current.CancellationToken);
            Assert.False(await launcher.TryLaunchAsync(script, TestContext.Current.CancellationToken));

            Assert.False(await launcher.TryLaunchAsync(
                Path.Combine(folder, "Gone.2014.mkv"),
                TestContext.Current.CancellationToken));

            // The half the real launcher cannot be asked for without opening a player on whoever is
            // running this: an approved container that is really there does get handed over.
            var film = Path.Combine(folder, "Arrival.2016.mkv");
            await File.WriteAllTextAsync(film, "a film", TestContext.Current.CancellationToken);
            Assert.True(await launcher.TryLaunchAsync(film, TestContext.Current.CancellationToken));
            Assert.Equal(
                [$"{RecordingSystemHandoff.PlayExternallyVerb} {film}"],
                await File.ReadAllLinesAsync(handoff.RecordPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
