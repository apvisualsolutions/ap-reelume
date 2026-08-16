// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

/// <summary>
/// The rule that decides whether an address may leave the application at all, wherever it leaves
/// from.
/// </summary>
/// <remarks>
/// It is one rule and not one per exit on purpose. The person whose profile this is gets a browser;
/// a run that keeps its data somewhere of its own gets the same address written down instead, so a
/// harness can assert on what would have opened. That assertion is only worth something while both
/// exits refuse exactly the same things, which is what having a single policy makes true.
/// </remarks>
public sealed class ExternalLinkPolicyTests
{
    [Fact]
    public void An_https_address_with_its_own_host_is_accepted_in_its_absolute_form()
    {
        Assert.True(ExternalLinkPolicy.TryAccept("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out var address));
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", address.AbsoluteUri);
    }

    /// <summary>
    /// What comes back is the address that was judged, not the caller's spelling of it: a host in
    /// capitals is the same host, and letting the original text through would mean the thing checked
    /// and the thing opened are two different strings.
    /// </summary>
    [Fact]
    public void What_comes_back_is_the_address_that_was_judged()
    {
        Assert.True(ExternalLinkPolicy.TryAccept("https://WWW.YouTube.com/watch?v=dQw4w9WgXcQ", out var address));
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", address.AbsoluteUri);
    }

    /// <summary>
    /// The user-information case is the one a reader does not expect:
    /// <c>https://www.youtube.com@example.invalid/</c> is a valid https address whose host is
    /// <c>example.invalid</c>. Everything left of the <c>@</c> is there to be read by a person.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
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
    [InlineData("https:///watch")]
    public void Anything_but_an_https_address_with_its_own_host_is_refused(string? link)
    {
        Assert.False(ExternalLinkPolicy.TryAccept(link, out var address));
        Assert.Null(address);
    }

    /// <summary>
    /// Why the policy has no check for an empty host: an absolute https address with no host cannot
    /// be built at all.
    /// </summary>
    /// <remarks>
    /// The launcher this rule came from carried that check, and nothing had ever reached it — the
    /// coverage gate is what said so, at seven branches of eight. This is the measurement that
    /// replaced it: every spelling of a host-less https address fails to parse, so the guard was a
    /// claim of a defence rather than one. If a second scheme is ever allowed through the policy, it
    /// has to come back with it.
    /// </remarks>
    [Theory]
    [InlineData("https://")]
    [InlineData("https:")]
    [InlineData("https:///")]
    [InlineData("https:////")]
    [InlineData("https://:8080/")]
    [InlineData("https://@/")]
    [InlineData("https:///watch?v=dQw4w9WgXcQ")]
    public void An_https_address_with_no_host_cannot_even_be_built(string candidate)
    {
        var built = Uri.TryCreate(candidate, UriKind.Absolute, out var address)
            && string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && address.Host.Length == 0;

        Assert.False(built, $"{candidate} became an https address whose host is empty.");
    }
}
