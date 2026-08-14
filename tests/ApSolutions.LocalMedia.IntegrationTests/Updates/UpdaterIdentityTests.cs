// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Tests.Updates;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Updates;

/// <summary>
/// What the updater tells GitHub it is (ARQ-014).
/// </summary>
/// <remarks>
/// The User-Agent announced <c>AP-Reelume-Updater/1.0</c> while the product declared <c>0.1.0</c>: a
/// version written by hand that nobody synchronised, and the only thing the other end has to
/// identify a request by. The brand is the public name and stays; the number comes from the assembly
/// now, and this pins it against <c>Directory.Build.props</c>, which is the single source of the
/// version in this repository.
/// </remarks>
public sealed class UpdaterIdentityTests
{
    private const string LatestPath = "/repos/ap-solutions/ap-reelume/releases/latest";

    [Fact]
    public async Task The_updater_announces_the_version_the_product_declares()
    {
        using var server = new FakeReleaseServer();
        _ = server.Map(LatestPath, _ => new FakeResponse(HttpStatusCode.NotFound, [], []));
        using var client = server.CreateClient();

        _ = await new GitHubReleaseUpdateProvider(client, "ap-solutions", "ap-reelume")
            .GetLatestAsync("win-x64", TestContext.Current.CancellationToken);

        var request = Assert.Single(server.Requests);
        var userAgent = Assert.Contains("user-agent", request.Headers);
        Assert.Equal($"AP-Reelume-Updater/{DeclaredVersion()}", userAgent);
    }

    /// <summary>
    /// The one place this repository states its version. Read rather than repeated: a copy here
    /// would be the same defect this test exists to catch.
    /// </summary>
    private static string DeclaredVersion()
    {
        var props = File.ReadAllText(RepositoryLayout.PathFromRoot("Directory.Build.props"));
        var match = Regex.Match(
            props,
            @"<Version>(?<version>[^<]+)</Version>",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        Assert.True(match.Success, "Directory.Build.props declares no <Version> for anything to match.");
        return match.Groups["version"].Value;
    }
}
