// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Windows.Playback;
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
}
