// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Windows.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Metadata;

/// <summary>
/// The launcher that hands a web address to the browser must be safe by construction. These tests
/// never reach the shell: they pin the refusals, which are the part that could send a browser
/// somewhere the application never intended.
/// </summary>
/// <remarks>
/// <para>
/// No real browser is opened. The call to the shell is handed in, so the accepting path is driven
/// against a double that records what it was given — which is how the address itself can be
/// asserted, rather than only the refusals. The coverage gate is what asked for this: with the call
/// hard-wired, everything past the refusals was unreachable and the class sat at 43.75% of lines.
/// </para>
/// <para>
/// The refusals have no measured "before": running them against a launcher with the checks removed
/// would mean handing the shell exactly what the checks exist to withhold — a <c>javascript:</c> or
/// a <c>file:</c> would open something on the machine measuring it. What was measured instead is the
/// layer above, where fifteen malformed keys composed addresses until <c>TrailerLinkPolicy</c>
/// stopped composing them.
/// </para>
/// </remarks>
public sealed class ExternalLinkLauncherTests
{
    [Fact]
    public async Task An_empty_address_is_rejected_before_any_shell_call()
    {
        var launcher = new ShellExternalLinkLauncher();

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => launcher.TryLaunchAsync("   ", TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => launcher.TryLaunchAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Only https reaches the shell, and only with the host it appears to name.
    /// </summary>
    /// <remarks>
    /// The user-information case is the one a reader does not expect:
    /// <c>https://www.youtube.com@example.invalid/</c> is a valid https address whose host is
    /// <c>example.invalid</c>. Everything left of the <c>@</c> is there to be read by a person.
    /// </remarks>
    [Theory]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:privacy")]
    [InlineData("ftp://example.invalid/payload")]
    [InlineData("https://www.youtube.com@example.invalid/")]
    [InlineData("https://user:password@example.invalid/")]
    [InlineData("www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("/watch?v=dQw4w9WgXcQ")]
    [InlineData("not an address at all")]
    public async Task Anything_but_an_https_address_with_its_own_host_is_refused(string link)
    {
        var launcher = new ShellExternalLinkLauncher();

        Assert.False(await launcher.TryLaunchAsync(link, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_cancelled_request_never_reaches_the_shell()
    {
        var launcher = new ShellExternalLinkLauncher();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => launcher.TryLaunchAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ", cancellation.Token));
    }

    /// <summary>
    /// What actually reaches the shell: the address as one whole instruction, with no command line
    /// composed around it and nothing else to interpret.
    /// </summary>
    [Fact]
    public async Task An_accepted_address_reaches_the_shell_whole_and_alone()
    {
        var seen = new List<ProcessStartInfo>();
        var launcher = new ShellExternalLinkLauncher(startInfo =>
        {
            seen.Add(startInfo);
            return null;
        });

        var launched = await launcher.TryLaunchAsync(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            TestContext.Current.CancellationToken);

        Assert.True(launched);
        var startInfo = Assert.Single(seen);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Empty(startInfo.ArgumentList);
        Assert.Empty(startInfo.Arguments);
    }

    /// <summary>
    /// A browser already running takes the address and no child process comes back. That is a
    /// success, and reporting it as a failure would tell somebody nothing happened while their
    /// browser opened a tab.
    /// </summary>
    [Fact]
    public async Task A_shell_that_returns_no_process_is_still_a_success()
    {
        var launcher = new ShellExternalLinkLauncher(_ => null);

        Assert.True(await launcher.TryLaunchAsync(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Nothing registered for https is a refusal, not a crash: the card goes on offering everything
    /// else it can do.
    /// </summary>
    [Fact]
    public async Task No_registered_browser_is_refused_rather_than_thrown()
    {
        var launcher = new ShellExternalLinkLauncher(
            _ => throw new System.ComponentModel.Win32Exception(2));

        Assert.False(await launcher.TryLaunchAsync(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_launcher_without_a_way_to_reach_the_shell_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ShellExternalLinkLauncher(null!));
    }

    /// <summary>
    /// The refusals happen before the shell, not after: nothing is handed over and then judged.
    /// </summary>
    [Theory]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://www.youtube.com@example.invalid/")]
    public async Task A_refused_address_never_reaches_the_shell_at_all(string link)
    {
        var seen = new List<ProcessStartInfo>();
        var launcher = new ShellExternalLinkLauncher(startInfo =>
        {
            seen.Add(startInfo);
            return null;
        });

        Assert.False(await launcher.TryLaunchAsync(link, TestContext.Current.CancellationToken));
        Assert.Empty(seen);
    }
}
