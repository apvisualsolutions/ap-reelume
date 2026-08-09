using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Every unplayable row must fail with a localisable domain code, offer non-destructive recovery,
/// and leave both the file and the catalogue entity untouched.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class CorruptMediaTests
{
    public static TheoryData<string> UnsupportedSampleIds()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MediaManifest.Samples
            .Where(s => s.ExpectedOutcome == ExpectedOutcome.ActionableUnsupported))
        {
            data.Add(sample.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UnsupportedSampleIds))]
    public async Task Every_unplayable_row_reports_its_expected_domain_code(string id)
    {
        var sample = MediaManifest.Require(id);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        var expected = Enum.Parse<PlaybackFailureCode>(sample.ExpectedFailureCode!);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(
            () => engine.OpenAsync(
                new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
                TestContext.Current.CancellationToken));

        Assert.Equal(expected, failure.Failure.Code);
        Assert.NotEmpty(failure.Failure.RecoveryActions);
        Assert.Contains(PlaybackRecoveryAction.OpenExternally, failure.Failure.RecoveryActions);
        Assert.Contains(PlaybackRecoveryAction.ChooseAnotherVersion, failure.Failure.RecoveryActions);
        Assert.Equal(0, engine.LiveMediaCount);
    }

    [Theory]
    [MemberData(nameof(UnsupportedSampleIds))]
    public async Task A_failed_row_never_deletes_or_rewrites_the_media_and_keeps_the_engine_reusable(string id)
    {
        var sample = MediaManifest.Require(id);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        var before = new FileInfo(path).Length;
        var hashBefore = await MediaManifest.ComputeHashAsync(path, TestContext.Current.CancellationToken);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<PlaybackFailureException>(
            () => engine.OpenAsync(
                new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
                TestContext.Current.CancellationToken));

        Assert.True(File.Exists(path), $"The engine removed '{sample.Id}'.");
        Assert.Equal(before, new FileInfo(path).Length);
        Assert.Equal(hashBefore, await MediaManifest.ComputeHashAsync(path, TestContext.Current.CancellationToken));

        var playable = MediaManifest.Require("mp4-h264-aac");
        var playablePath = await CodecMatrixTests.RequireSampleAsync(playable);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), playablePath),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        Assert.True(await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(150)));
        await engine.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, engine.LiveMediaCount);
    }

    [Fact]
    public async Task A_missing_file_is_recoverable_without_offering_a_destructive_action()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        var missing = Path.Combine(MediaToolchain.OutputRoot, "T19", "never-generated.mkv");

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(
            () => engine.OpenAsync(
                new PlaybackRequest(new MediaFileId(Guid.NewGuid()), missing),
                TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.FileNotFound, failure.Failure.Code);
        Assert.Contains(PlaybackRecoveryAction.Retry, failure.Failure.RecoveryActions);
        Assert.Contains(PlaybackRecoveryAction.ChooseAnotherVersion, failure.Failure.RecoveryActions);
        Assert.All(
            Enum.GetValues<PlaybackFailureCode>(),
            code => Assert.DoesNotContain(
                PlaybackDiagnosticsPolicy.RecoveryActionsFor(code),
                action => action.ToString().Contains("Delete", StringComparison.OrdinalIgnoreCase)));
    }
}
