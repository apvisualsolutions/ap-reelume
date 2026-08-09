using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Runs the approved container and codec matrix against the real engine. Every sample is generated
/// during the run from synthetic sources, so the matrix is reproducible and nothing is redistributed.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class CodecMatrixTests
{
    private static readonly JsonSerializerOptions ProvenanceOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<string> PlayableSampleIds()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MediaManifest.Samples.Where(s => s.ExpectedOutcome == ExpectedOutcome.Playable))
        {
            data.Add(sample.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PlayableSampleIds))]
    public async Task Every_playable_row_starts_audio_and_video_reports_its_duration_and_reaches_the_end(string id)
    {
        var sample = MediaManifest.Require(id);
        var path = await RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var started = await WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(200));
        var playing = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.True(started, $"Playback never advanced for '{sample.Id}'.");
        Assert.Equal(PlaybackState.Playing, playing.State);
        Assert.True(engine.DecodedFrameCount > 0, $"No frame was decoded for '{sample.Id}'.");

        var video = playing.Tracks.Where(track => track.Kind == MediaTrackKind.Video).ToArray();
        var audio = playing.Tracks.Where(track => track.Kind == MediaTrackKind.Audio).ToArray();
        Assert.Equal(sample.VideoTracks, video.Length);
        Assert.Equal(sample.AudioTracks, audio.Length);
        Assert.All(playing.Tracks, track => Assert.False(
            string.IsNullOrWhiteSpace(track.Codec),
            $"The engine announced no codec for a {track.Kind} track of '{sample.Id}'."));

        Assert.NotNull(playing.Duration);
        Assert.InRange(
            playing.Duration!.Value.TotalSeconds,
            sample.DurationSeconds!.Value - 1.0,
            sample.DurationSeconds!.Value + 1.0);

        await engine.SeekAsync(
            playing.Duration.Value - TimeSpan.FromMilliseconds(600),
            TestContext.Current.CancellationToken);
        var reachedEnd = await WaitForPositionAsync(
            engine,
            playing.Duration.Value - TimeSpan.FromMilliseconds(900));
        Assert.True(reachedEnd, $"'{sample.Id}' never reached the end of the media.");

        await engine.StopAsync(TestContext.Current.CancellationToken);
        var stopped = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackState.Stopped, stopped.State);
        Assert.Equal(0, engine.LiveMediaCount);
    }

    [Fact]
    public async Task A_video_without_an_audio_track_still_plays_and_announces_the_absence()
    {
        var sample = MediaManifest.Require("mkv-h264-no-audio");
        var path = await RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        _ = await WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(200));
        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PlaybackState.Playing, snapshot.State);
        Assert.Contains(snapshot.Tracks, track => track.Kind == MediaTrackKind.Video);
        Assert.DoesNotContain(snapshot.Tracks, track => track.Kind == MediaTrackKind.Audio);
        Assert.True(PlaybackDiagnosticsPolicy.IsMissingAudio(snapshot.Tracks));

        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_matrix_records_its_provenance_for_the_evidence_run()
    {
        var records = new List<object>();
        foreach (var sample in MediaManifest.Samples)
        {
            var path = await RequireSampleAsync(sample);
            records.Add(new
            {
                sample.Id,
                sample.Container,
                sample.VideoCodec,
                sample.AudioCodec,
                sample.ExpectedOutcome,
                sample.ExpectedFailureCode,
                Bytes = new FileInfo(path).Length,
                Sha256 = await MediaManifest.ComputeHashAsync(path, TestContext.Current.CancellationToken),
            });
        }

        var destination = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "T19",
            "green",
            "media-provenance.json");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(
            destination,
            JsonSerializer.Serialize(records, ProvenanceOptions),
            TestContext.Current.CancellationToken);

        Assert.Equal(MediaManifest.Samples.Count, records.Count);
    }

    internal static async Task<string> RequireSampleAsync(MediaSample sample)
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var missing = MediaManifest.MissingEncoders(sample);
        Assert.SkipWhen(
            missing.Count > 0,
            $"The local encoder cannot produce '{sample.Id}': missing {string.Join(", ", missing)}.");
        return await MediaManifest.MaterialiseAsync(sample, TestContext.Current.CancellationToken);
    }

    internal static async Task<bool> WaitForPositionAsync(LibVlcMediaPlayerEngine engine, TimeSpan minimum)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
            if (snapshot.Position >= minimum)
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }
}
