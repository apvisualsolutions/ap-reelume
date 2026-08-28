// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

/// <summary>
/// A poster path is a remote string, so the address is refused before it is composed.
/// </summary>
/// <remarks>
/// The same rule <c>TrailerLinkPolicy</c> exists for, on the other value TMDB sends that becomes a
/// URL. What this refuses is what a malformed path could otherwise reach: a second slash climbing
/// out of <c>/t/p/w780/</c>, a whole address of somebody else's choosing, or a scheme.
/// </remarks>
public sealed class PosterAddressPolicyTests
{
    [Fact]
    public void A_well_formed_path_becomes_the_one_address_this_application_fetches()
    {
        var address = PosterAddressPolicy.TryBuildPosterAddress("/wXsQvli6tWqja51pYxXNG1LFIGV.jpg");

        Assert.Equal(
            "https://image.tmdb.org/t/p/w780/wXsQvli6tWqja51pYxXNG1LFIGV.jpg",
            address);

        // The host is the one the network registry declares for this component, and the size is the
        // single one fetched — both are read from the policy rather than written twice.
        Assert.Contains(PosterAddressPolicy.Host, address, StringComparison.Ordinal);
        Assert.Contains(PosterAddressPolicy.Size, address, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // No leading slash: a bare name would compose against the size segment and change it.
    [InlineData("wXsQ.jpg")]
    // A second slash is a path, and a path can climb out of the size segment.
    [InlineData("/../../etc/passwd.jpg")]
    [InlineData("/a/b.jpg")]
    // A whole address of somebody else's choosing.
    [InlineData("/https://evil.example/x.jpg")]
    [InlineData("https://evil.example/x.jpg")]
    // A scheme smuggled in, which is what the trailer key's own policy refuses.
    [InlineData("/javascript:alert(1).jpg")]
    // A query or a fragment would carry whatever follows into the request.
    [InlineData("/wXsQ.jpg?x=1")]
    [InlineData("/wXsQ.jpg#x")]
    // Percent encoding, which is a second alphabet and therefore a second reading.
    [InlineData("/wXsQ%2e%2e.jpg")]
    // No extension, an extension with nothing before it, and a trailing dot.
    [InlineData("/wXsQ")]
    [InlineData("/.jpg")]
    [InlineData("/wXsQ.")]
    // Too short to be anything at all.
    [InlineData("/")]
    [InlineData("/a.")]
    // A digit Unicode knows that ASCII does not — the reason for IsAsciiLetterOrDigit.
    [InlineData("/wXsQ٣.jpg")]
    public void Anything_else_is_refused_rather_than_composed(string? posterPath)
    {
        Assert.Null(PosterAddressPolicy.TryBuildPosterAddress(posterPath));
    }

    /// <summary>
    /// Every refusal above is one the composing-first version would have accepted, or nearly.
    /// </summary>
    /// <remarks>
    /// Asserted as a property rather than by listing: whatever is built is absolute, on the declared
    /// host, and under the size segment. A path that got through and changed any of the three would
    /// be the defect this policy exists for.
    /// </remarks>
    [Theory]
    [InlineData("/wXsQvli6tWqja51pYxXNG1LFIGV.jpg")]
    [InlineData("/a-b_c.png")]
    [InlineData("/0.webp")]
    public void Whatever_is_built_is_on_the_declared_host_and_under_the_size(string posterPath)
    {
        var built = PosterAddressPolicy.TryBuildPosterAddress(posterPath);

        Assert.NotNull(built);
        var uri = new Uri(built!, UriKind.Absolute);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal(PosterAddressPolicy.Host, uri.Host);
        Assert.StartsWith($"/t/p/{PosterAddressPolicy.Size}/", uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Empty(uri.Query);
        Assert.Empty(uri.Fragment);
    }
}
