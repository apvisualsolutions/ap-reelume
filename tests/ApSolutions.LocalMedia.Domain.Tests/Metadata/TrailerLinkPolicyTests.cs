// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

/// <summary>
/// The one place that turns a string TMDB sent into an address a browser is asked to open.
/// </summary>
/// <remarks>
/// The key is validated before anything is composed, not after: composing first and inspecting the
/// result afterwards means the malformed address already exists, and every later reader has to
/// remember to distrust it. Eleven characters of the YouTube alphabet is the whole contract, so
/// anything that could steer a browser somewhere else — a scheme, a host, a query, a fragment, a
/// path separator — fails to be a key long before it could be a link.
/// </remarks>
public sealed class TrailerLinkPolicyTests
{
    [Fact]
    public void A_well_formed_key_becomes_the_watch_address_and_nothing_else()
    {
        Assert.Equal(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            TrailerLinkPolicy.TryBuildWatchLink("dQw4w9WgXcQ"));
    }

    [Theory]
    [InlineData("aaaaaaaaaaa")]
    [InlineData("AAAAAAAAAAA")]
    [InlineData("00000000000")]
    [InlineData("___________")]
    [InlineData("-----------")]
    [InlineData("aA0_-aA0_-a")]
    public void Every_character_YouTube_uses_is_accepted(string key)
    {
        Assert.Equal($"https://www.youtube.com/watch?v={key}", TrailerLinkPolicy.TryBuildWatchLink(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dQw4w9WgXc")]
    [InlineData("dQw4w9WgXcQQ")]
    [InlineData("dQw4w9WgXc.")]
    [InlineData("dQw4w9WgX Q")]
    [InlineData("dQw4w9Wg/cQ")]
    [InlineData("dQw4w9Wg\\cQ")]
    [InlineData("dQw4w9Wg?cQ")]
    [InlineData("dQw4w9Wg&cQ")]
    [InlineData("dQw4w9Wg#cQ")]
    [InlineData("dQw4w9Wg%cQ")]
    [InlineData("dQw4w9Wg:cQ")]
    [InlineData("https://evil.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example")]
    [InlineData("dQw4w9WgXcQ\nfile:///C:/Windows")]
    public void Anything_that_is_not_exactly_a_key_produces_no_link(string? key)
    {
        Assert.Null(TrailerLinkPolicy.TryBuildWatchLink(key));
    }

    /// <summary>
    /// Whatever comes out is an absolute https address on YouTube's own host — the property the
    /// hardened launcher is entitled to rely on.
    /// </summary>
    [Fact]
    public void What_comes_out_is_always_an_absolute_https_YouTube_address()
    {
        var link = TrailerLinkPolicy.TryBuildWatchLink("dQw4w9WgXcQ");

        Assert.NotNull(link);
        Assert.True(Uri.TryCreate(link, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
        Assert.Equal("www.youtube.com", uri.Host);
    }
}
