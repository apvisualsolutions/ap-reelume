using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Channel layouts checked against real decoded media. What the source carries is verified here; what
/// the machine can actually play is a separate, hardware-dependent question recorded in the evidence.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class AudioChannelTests
{
    [Theory]
    [InlineData("mkv-audio-stereo", 2, AudioChannelLayout.Stereo)]
    [InlineData("mkv-audio-51", 6, AudioChannelLayout.Surround51)]
    [InlineData("mkv-audio-71", 8, AudioChannelLayout.Surround71)]
    public async Task The_engine_announces_the_channel_count_the_source_actually_carries(
        string id,
        int channels,
        AudioChannelLayout layout)
    {
        var sample = MediaManifest.Require(id);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        _ = await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(150));
        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var audio = snapshot.Tracks.Single(track => track.Kind == MediaTrackKind.Audio);
        Assert.Equal(channels, audio.Channels);
        Assert.Equal(channels, (int)layout);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_surround_source_on_a_stereo_endpoint_is_reported_as_degraded_not_as_surround()
    {
        var sample = MediaManifest.Require("mkv-audio-71");
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var sourceChannels = snapshot.Tracks.Single(track => track.Kind == MediaTrackKind.Audio).Channels;

        var stereoOnly = new AudioOutputDevice(
            "endpoint-stereo",
            "Salida estéreo",
            [AudioChannelLayout.Stereo],
            IsDefault: true,
            IsAvailable: true);
        var selection = AudioOutputPolicy.Resolve(
            [stereoOnly],
            stereoOnly.Id,
            AudioChannelLayout.Surround71);

        Assert.Equal(8, sourceChannels);
        Assert.Equal(AudioChannelLayout.Stereo, selection!.Layout);
        Assert.True(selection.LayoutWasDegraded);
        Assert.Equal(AudioChannelLayout.Surround71, selection.DegradedFrom);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_source_layouts_this_run_could_verify_are_recorded()
    {
        var rows = new List<string> { "sampleId,declaredChannels,observedChannels" };
        foreach (var id in new[] { "mkv-audio-stereo", "mkv-audio-51", "mkv-audio-71" })
        {
            var sample = MediaManifest.Require(id);
            var path = await CodecMatrixTests.RequireSampleAsync(sample);
            await using var factory = LibVlcFactory.CreateHeadless();
            await using var engine = new LibVlcMediaPlayerEngine(factory);
            await engine.InitializeAsync(TestContext.Current.CancellationToken);
            await engine.OpenAsync(
                new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
                TestContext.Current.CancellationToken);
            var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
            var observed = snapshot.Tracks.Single(track => track.Kind == MediaTrackKind.Audio).Channels;
            rows.Add(string.Create(CultureInfo.InvariantCulture, $"{id},{sample.AudioCodec},{observed}"));
            await engine.StopAsync(TestContext.Current.CancellationToken);
        }

        var report = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "T23",
            "green",
            "source-channel-layouts.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(report, rows, TestContext.Current.CancellationToken);

        Assert.Equal(4, rows.Count);
    }
}
