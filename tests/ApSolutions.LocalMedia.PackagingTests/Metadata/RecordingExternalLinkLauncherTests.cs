// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Windows.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Metadata;

/// <summary>
/// The exit a run that does not own this machine's profile is built with: the address is written
/// down, under that run's own root, and no browser opens anywhere.
/// </summary>
/// <remarks>
/// What makes this worth having is not only that a harness can press the button. Until it existed,
/// nothing checked that the address the button would open is the address the stored key composes —
/// every assertion stopped at the view model, because the layer past it went to a real browser.
/// </remarks>
public sealed class RecordingExternalLinkLauncherTests : IDisposable
{
    private const string WatchAddress = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"handoff-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task An_accepted_address_is_written_whole_and_alone()
    {
        var launcher = new RecordingExternalLinkLauncher(_root);

        Assert.True(await launcher.TryLaunchAsync(WatchAddress, TestContext.Current.CancellationToken));

        Assert.Equal(
            [WatchAddress],
            await File.ReadAllLinesAsync(launcher.RecordPath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The folder is made when it is needed. A run's root exists long before anything asks to leave
    /// it, and a first press that failed because a folder was missing would report "the offer could
    /// not be honoured" for a reason that has nothing to do with the offer.
    /// </summary>
    [Fact]
    public async Task The_folder_does_not_have_to_exist_beforehand()
    {
        var launcher = new RecordingExternalLinkLauncher(Path.Combine(_root, "never", "created"));

        Assert.True(await launcher.TryLaunchAsync(WatchAddress, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(launcher.RecordPath));
    }

    /// <summary>
    /// Each press is added, in order: the same offer lives on two cards, and telling one press from
    /// the other is the whole reason the record is a list rather than a value.
    /// </summary>
    [Fact]
    public async Task Every_address_is_added_in_the_order_it_was_asked_for()
    {
        var launcher = new RecordingExternalLinkLauncher(_root);
        const string Second = "https://www.youtube.com/watch?v=aaaaaaaaaaa";

        Assert.True(await launcher.TryLaunchAsync(WatchAddress, TestContext.Current.CancellationToken));
        Assert.True(await launcher.TryLaunchAsync(Second, TestContext.Current.CancellationToken));

        Assert.Equal(
            [WatchAddress, Second],
            await File.ReadAllLinesAsync(launcher.RecordPath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The same refusals as the exit that talks to the shell, because it is the same policy. A
    /// record that accepted what a browser would not would be a measurement of nothing.
    /// </summary>
    [Theory]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://www.youtube.com@example.invalid/")]
    [InlineData("not an address at all")]
    public async Task A_refused_address_is_not_written_down_either(string link)
    {
        var launcher = new RecordingExternalLinkLauncher(_root);

        Assert.False(await launcher.TryLaunchAsync(link, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(launcher.RecordPath));
    }

    [Fact]
    public async Task An_empty_address_is_rejected_before_anything_is_written()
    {
        var launcher = new RecordingExternalLinkLauncher(_root);

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => launcher.TryLaunchAsync("   ", TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => launcher.TryLaunchAsync(null!, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(launcher.RecordPath));
    }

    [Fact]
    public async Task A_cancelled_request_is_never_written_down()
    {
        var launcher = new RecordingExternalLinkLauncher(_root);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => launcher.TryLaunchAsync(WatchAddress, cancellation.Token));
        Assert.False(File.Exists(launcher.RecordPath));
    }

    /// <summary>
    /// A record that cannot be written is a refusal rather than a crash, the same answer the shell
    /// exit gives when no browser is registered for https.
    /// </summary>
    [Fact]
    public async Task A_record_that_cannot_be_written_is_refused_rather_than_thrown()
    {
        // A file where the folder has to go: creating the directory fails, and so does everything
        // after it.
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "blocked");
        await File.WriteAllTextAsync(blocked, "in the way", TestContext.Current.CancellationToken);
        var launcher = new RecordingExternalLinkLauncher(blocked);

        Assert.False(await launcher.TryLaunchAsync(WatchAddress, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_launcher_with_nowhere_to_write_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentException>(() => new RecordingExternalLinkLauncher("   "));
        _ = Assert.Throws<ArgumentNullException>(() => new RecordingExternalLinkLauncher(null!));
    }
}
