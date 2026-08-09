using ApSolutions.LocalMedia.Domain.Updates;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Updates;

/// <summary>
/// What the application is willing to be replaced by. Every rule here decides that question before a
/// single byte is downloaded, because a check that runs after the download has already let the
/// network choose what gets written to disk.
/// </summary>
/// <remarks>
/// The rules are deliberately a pure decision over a description: no HTTP, no files, no clock. That
/// is what lets every rejection be stated as a case rather than reproduced as a scenario, and it is
/// why the answer carries a reason — an updater that silently declines is indistinguishable from one
/// that is broken.
/// </remarks>
public sealed class UpdateManifestTests
{
    private const string InstalledVersion = "0.1.0";
    private const string InstalledRuntime = "win-x64";

    [Fact]
    public void A_well_formed_newer_release_for_this_runtime_is_offered()
    {
        var decision = Decide(Valid());

        Assert.True(decision.IsOffered, decision.Reason);
        Assert.Equal(UpdateRejection.None, decision.Rejection);
    }

    /// <summary>
    /// The same version is not an update. Offering it would make the application ask permission to
    /// replace itself with itself, which teaches people to confirm without reading.
    /// </summary>
    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.0.9")]
    [InlineData("0.1.0-rc.1")]
    public void A_release_that_is_not_newer_is_refused(string offered)
    {
        var decision = Decide(Valid() with { Version = offered });

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.NotNewer, decision.Rejection);
    }

    /// <summary>
    /// A package for another architecture would install and then fail to start, which is worse than
    /// not offering it: the working copy is already gone by then.
    /// </summary>
    [Theory]
    [InlineData("win-arm64")]
    [InlineData("win-x86")]
    [InlineData("linux-x64")]
    [InlineData("")]
    public void A_release_for_another_runtime_is_refused(string runtime)
    {
        var decision = Decide(Valid() with { Runtime = runtime });

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.WrongRuntime, decision.Rejection);
    }

    /// <summary>
    /// Plain HTTP would let anyone on the path choose the bytes. The hash would still catch it, but
    /// only after the download, and only if the manifest itself arrived unmodified — which over HTTP
    /// it cannot be known to have done.
    /// </summary>
    [Theory]
    [InlineData("http://example.invalid/apreelume.msix")]
    [InlineData("ftp://example.invalid/apreelume.msix")]
    [InlineData("file:///C:/apreelume.msix")]
    [InlineData("not a url")]
    [InlineData("")]
    public void A_download_that_is_not_https_is_refused(string url)
    {
        var decision = Decide(Valid() with { Url = url });

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.InsecureDownload, decision.Rejection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    // Sixty-three characters: one short, which is the mistake a truncating tool makes.
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]
    // The right length carrying something that is not hexadecimal.
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void A_release_without_a_usable_hash_is_refused(string sha256)
    {
        var decision = Decide(Valid() with { Sha256 = sha256 });

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.UnusableHash, decision.Rejection);
    }

    /// <summary>
    /// SEC-003: a hash nobody signed vouches for nothing — it arrived in the same unsigned answer
    /// as the package it describes. The refusal names the signature so whoever publishes releases
    /// looks for the signing step, not for a missing hash that is right there.
    /// </summary>
    [Fact]
    public void A_release_whose_checksums_are_not_signed_is_refused()
    {
        var decision = Decide(Valid() with { Sha256Signed = false });

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.UnsignedChecksums, decision.Rejection);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    /// <summary>
    /// The declared size is what makes an interrupted download detectable as incomplete rather than
    /// verified as wrong, so a manifest that omits it is refused rather than trusted.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void A_release_without_a_declared_size_is_refused(long size)
    {
        var decision = Decide(Valid() with { SizeInBytes = size });

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.UndeclaredSize, decision.Rejection);
    }

    /// <summary>
    /// Both languages or neither. Confirmation means reading what changed, and a summary the person
    /// cannot read is a confirmation dialogue with nothing in it.
    /// </summary>
    [Theory]
    [InlineData(null, "What changed")]
    [InlineData("Qué cambió", null)]
    [InlineData("", "What changed")]
    [InlineData("Qué cambió", "   ")]
    [InlineData(null, null)]
    public void A_release_without_a_summary_in_both_languages_is_refused(string? spanish, string? english)
    {
        var decision = Decide(Valid() with { SummaryEs = spanish, SummaryEn = english });

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.IncompleteSummary, decision.Rejection);
    }

    /// <summary>
    /// An installation that cannot say what version it is gets offered nothing. Refusing is the safe
    /// answer: the alternative is replacing an application that does not know what it is, and the
    /// only evidence that the replacement was an upgrade would be the manifest's own word for it.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0.1.0.0.0")]
    public void An_installation_that_cannot_state_its_own_version_is_offered_nothing(string installed)
    {
        var decision = UpdatePolicy.Decide(Valid(), installed, InstalledRuntime);

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.NotNewer, decision.Rejection);
    }

    /// <summary>
    /// The absence of a release is an answer, not a failure. It has to be distinguishable from a
    /// check that could not run, or a machine that is offline looks the same as one that is current.
    /// </summary>
    [Fact]
    public void No_release_at_all_is_a_settled_answer_rather_than_a_rejection()
    {
        var decision = UpdatePolicy.Decide(null, InstalledVersion, InstalledRuntime);

        Assert.False(decision.IsOffered);
        Assert.Equal(UpdateRejection.NoReleaseAvailable, decision.Rejection);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    /// <summary>
    /// Every refusal says why. The reason is shown to whoever asked for the check, so "no" and
    /// "something went wrong and nobody looked" cannot be the same outcome.
    /// </summary>
    [Fact]
    public void Every_refusal_carries_a_reason()
    {
        var refusals = new[]
        {
            Decide(Valid() with { Version = InstalledVersion }),
            Decide(Valid() with { Runtime = "win-arm64" }),
            Decide(Valid() with { Url = "http://example.invalid/x.msix" }),
            Decide(Valid() with { Sha256 = "nope" }),
            Decide(Valid() with { SizeInBytes = 0 }),
            Decide(Valid() with { SummaryEs = null }),
        };

        Assert.All(refusals, decision =>
        {
            Assert.False(decision.IsOffered);
            Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
        });
    }

    /// <summary>
    /// A manifest failing several rules is reported by the one that matters most. Telling someone
    /// their summary is missing, when the download was going to arrive over plain HTTP anyway, buries
    /// the finding that mattered.
    /// </summary>
    [Fact]
    public void The_most_serious_problem_is_the_one_reported()
    {
        var everything = Valid() with
        {
            Version = "0.0.1",
            Runtime = "win-arm64",
            Url = "http://example.invalid/x.msix",
            Sha256 = "nope",
            SizeInBytes = 0,
            SummaryEs = null,
        };

        Assert.Equal(UpdateRejection.InsecureDownload, Decide(everything).Rejection);
    }

    private static UpdateDecision Decide(UpdateRelease release) =>
        UpdatePolicy.Decide(release, InstalledVersion, InstalledRuntime);

    private static UpdateRelease Valid() => new(
        Version: "0.2.0",
        Runtime: InstalledRuntime,
        Url: "https://example.invalid/APSolutions.LocalMedia_0.2.0_x64.msix",
        Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        SizeInBytes: 99_614_720,
        SummaryEs: "Corrige la reanudación en unidades desconectadas.",
        SummaryEn: "Fixes resume on disconnected drives.")
    {
        Sha256Signed = true,
    };
}
