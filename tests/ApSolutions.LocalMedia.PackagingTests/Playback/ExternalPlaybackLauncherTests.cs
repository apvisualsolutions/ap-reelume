// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
