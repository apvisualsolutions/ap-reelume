// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Windows.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Metadata;

/// <summary>
/// The launcher that hands a web address to the browser must be safe by construction. These tests
/// never reach the shell: they pin the refusals, which are the part that could send a browser
/// somewhere the application never intended.
/// </summary>
/// <remarks>
/// The other half — a well-formed address reaching the shell — is deliberately not driven, for the
/// same reason the external playback launcher's is not: a test that opened a real browser on the
/// machine running it would be worse than the coverage it bought. That also means the refusals here
/// have no measured "before": running them against a launcher with the checks removed would mean
/// handing the shell exactly what the checks exist to withhold. What was measured instead is the
/// layer above, where fifteen malformed keys composed addresses until
/// <c>TrailerLinkPolicy</c> stopped composing them.
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
}
