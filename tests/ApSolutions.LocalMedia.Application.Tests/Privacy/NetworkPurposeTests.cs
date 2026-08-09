using ApSolutions.LocalMedia.Application.Privacy;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Privacy;

/// <summary>
/// The host rule the allowlists run on (SEC-004). The wildcard is deliberately narrow: one leading
/// label, subdomains only, never the bare domain — so a declared pattern cannot quietly widen.
/// </summary>
public sealed class NetworkPurposeTests
{
    [Theory]
    [InlineData("github.com", "github.com", true)]
    [InlineData("github.com", "GITHUB.COM", true)]
    [InlineData("github.com", "api.github.com", false)]
    [InlineData("github.com", "notgithub.com", false)]
    [InlineData("*.githubusercontent.com", "objects.githubusercontent.com", true)]
    [InlineData("*.githubusercontent.com", "release-assets.githubusercontent.com", true)]
    [InlineData("*.githubusercontent.com", "githubusercontent.com", false)]
    [InlineData("*.githubusercontent.com", "evilgithubusercontent.com", false)]
    public void A_pattern_covers_exactly_what_it_wrote_down(string pattern, string host, bool expected) =>
        Assert.Equal(expected, NetworkPurpose.Matches(pattern, host));

    [Fact]
    public void A_purpose_answers_for_its_named_host_and_its_declared_redirects()
    {
        var purpose = new NetworkPurpose(
            "Test",
            "github.com",
            "reason",
            RequiresConsent: true,
            AdditionalHosts: ["*.githubusercontent.com"]);

        Assert.True(purpose.Allows("github.com"));
        Assert.True(purpose.Allows("objects.githubusercontent.com"));
        Assert.False(purpose.Allows("example.net"));
        Assert.False(purpose.Allows("  "));
        Assert.Equal(["github.com", "*.githubusercontent.com"], purpose.Hosts);
    }
}
