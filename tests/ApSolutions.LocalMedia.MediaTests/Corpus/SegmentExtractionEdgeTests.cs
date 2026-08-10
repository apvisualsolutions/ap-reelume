// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>
/// The extractor's edges: what happens with a missing file, a file that is not media, a duration
/// nobody supplied, a window past the end, silence, and every malformed WAV the parser can meet.
/// </summary>
public sealed class SegmentExtractionEdgeTests
{
    [Fact]
    public void The_extractor_refuses_to_exist_without_an_engine()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new LocalSegmentFeatureExtractor(null!));
    }

    [Fact]
    public async Task A_missing_file_fails_before_anything_is_decoded()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);
        var missing = Path.Combine(MediaToolchain.OutputRoot, "segments", "does-not-exist.mkv");

        _ = await Assert.ThrowsAsync<FileNotFoundException>(() => extractor.ExtractAsync(
            new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), missing, TimeSpan.FromMinutes(3)),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Similarity_of_an_empty_fingerprint_is_zero_in_both_directions()
    {
        var vector = new float[] { 1f, 0f };

        Assert.Equal(0, LocalSegmentFeatureExtractor.Similarity([], vector));
        Assert.Equal(0, LocalSegmentFeatureExtractor.Similarity(vector, []));
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task A_second_extraction_of_the_same_file_reuses_the_cache_with_the_new_identity()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var episode = SegmentCorpus.Series.Single(series => series.Id == "S01").Episodes[0];
        var path = await SegmentCorpus.MaterialiseAsync(episode, TestContext.Current.CancellationToken);
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);
        var duration = SegmentCorpus.EpisodeDuration(episode);

        var first = await extractor.ExtractAsync(
            new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), path, duration),
            TestContext.Current.CancellationToken);
        var secondId = new MediaFileId(Guid.NewGuid());
        var second = await extractor.ExtractAsync(
            new SegmentDetectionEpisode(secondId, path, duration),
            TestContext.Current.CancellationToken);

        Assert.Equal(secondId, second.FileId);
        Assert.Same(first.Opening, second.Opening);
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task An_unknown_duration_is_probed_from_the_file_itself()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var episode = SegmentCorpus.Series.Single(series => series.Id == "S01").Episodes[1];
        var path = await SegmentCorpus.MaterialiseAsync(episode, TestContext.Current.CancellationToken);
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);

        var prints = await extractor.ExtractAsync(
            new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), path, Duration: null),
            TestContext.Current.CancellationToken);

        Assert.InRange(
            prints.Duration.TotalSeconds,
            SegmentCorpus.EpisodeDuration(episode).TotalSeconds - 2,
            SegmentCorpus.EpisodeDuration(episode).TotalSeconds + 2);
        Assert.NotEmpty(prints.Opening);
    }

    [Fact]
    public async Task A_file_that_is_not_media_fails_the_probe_with_a_clear_error()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);
        var garbage = Path.Combine(Path.GetTempPath(), "ApSolutions.LocalMedia", $"garbage-{Guid.NewGuid():N}.mkv");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(garbage)!);
        await File.WriteAllTextAsync(
            garbage,
            "this is not a container",
            TestContext.Current.CancellationToken);
        try
        {
            _ = await Assert.ThrowsAsync<InvalidDataException>(() => extractor.ExtractAsync(
                new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), garbage, Duration: null),
                TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(garbage);
        }
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task Silence_produces_fingerprints_that_match_nothing_not_even_themselves()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            "segments-edge/silence.mkv",
            "-f lavfi -i color=c=0x101010:size=320x180:rate=12:duration=8 "
            + "-f lavfi -i anullsrc=r=22050:cl=mono -t 8 -c:v mpeg4 -q:v 12 -c:a aac -b:a 32k",
            TestContext.Current.CancellationToken);
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);

        var prints = await extractor.ExtractAsync(
            new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), path, TimeSpan.FromSeconds(8)),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(prints.Opening);
        Assert.All(prints.Opening, print => Assert.Equal(
            0,
            LocalSegmentFeatureExtractor.Similarity(print, print)));
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task A_claimed_duration_far_past_the_end_still_returns_bounded_windows()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var episode = SegmentCorpus.Series.Single(series => series.Id == "S01").Episodes[2];
        var path = await SegmentCorpus.MaterialiseAsync(episode, TestContext.Current.CancellationToken);
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);

        var prints = await extractor.ExtractAsync(
            new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), path, TimeSpan.FromMinutes(20)),
            TestContext.Current.CancellationToken);

        // The opening window is honestly decodable; the closing window starts past the real end and
        // simply carries whatever little the engine could read, without hanging or throwing.
        Assert.NotEmpty(prints.Opening);
        Assert.True(prints.Opening.Count <= 180);
    }

    [Fact]
    public async Task A_probe_that_reports_no_duration_is_refused()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory, new DurationlessProbe());
        var existing = Path.Combine(Path.GetTempPath(), "ApSolutions.LocalMedia", $"real-{Guid.NewGuid():N}.mkv");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllBytesAsync(existing, new byte[64], TestContext.Current.CancellationToken);
        try
        {
            _ = await Assert.ThrowsAsync<InvalidDataException>(() => extractor.ExtractAsync(
                new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), existing, Duration: null),
                TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(existing);
        }
    }

    [Fact]
    public async Task A_file_that_is_not_media_fails_the_decode_when_the_duration_was_supplied()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);
        var garbage = Path.Combine(Path.GetTempPath(), "ApSolutions.LocalMedia", $"garbage-{Guid.NewGuid():N}.mkv");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(garbage)!);
        await File.WriteAllTextAsync(
            garbage,
            "still not a container",
            TestContext.Current.CancellationToken);
        try
        {
            _ = await Assert.ThrowsAsync<InvalidDataException>(() => extractor.ExtractAsync(
                new SegmentDetectionEpisode(new MediaFileId(Guid.NewGuid()), garbage, TimeSpan.FromMinutes(3)),
                TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(garbage);
        }
    }

    [Fact]
    public async Task The_wav_parser_survives_every_malformation_it_can_meet()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ApSolutions.LocalMedia", $"wav-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            // A canonical file: RIFF header, a fmt chunk the parser skips, and four samples.
            var canonical = Path.Combine(directory, "canonical.wav");
            await File.WriteAllBytesAsync(
                canonical,
                Wav(FmtChunk(), DataChunk([1, 2, 3, 4], declaredSize: 8)),
                TestContext.Current.CancellationToken);
            Assert.Equal(new short[] { 1, 2, 3, 4 }, LocalSegmentFeatureExtractor.ReadWavSamples(canonical));

            // A writer that never closed the size: the declared length exceeds what exists.
            var unclosed = Path.Combine(directory, "unclosed.wav");
            await File.WriteAllBytesAsync(
                unclosed,
                Wav(FmtChunk(), DataChunk([5, 6], declaredSize: uint.MaxValue)),
                TestContext.Current.CancellationToken);
            Assert.Equal(new short[] { 5, 6 }, LocalSegmentFeatureExtractor.ReadWavSamples(unclosed));

            // A truncated file: fewer bytes than the data chunk claims.
            var truncated = Path.Combine(directory, "truncated.wav");
            await File.WriteAllBytesAsync(
                truncated,
                Wav(DataChunk([7, 8], declaredSize: 400)),
                TestContext.Current.CancellationToken);
            Assert.Equal(new short[] { 7, 8 }, LocalSegmentFeatureExtractor.ReadWavSamples(truncated));

            // No data chunk at all, and a file too short to hold any chunk: both fail loudly.
            var dataless = Path.Combine(directory, "dataless.wav");
            await File.WriteAllBytesAsync(
                dataless,
                Wav(FmtChunk()),
                TestContext.Current.CancellationToken);
            _ = Assert.Throws<InvalidDataException>(
                () => LocalSegmentFeatureExtractor.ReadWavSamples(dataless));

            var stub = Path.Combine(directory, "stub.wav");
            await File.WriteAllBytesAsync(stub, "RIFF"u8.ToArray(), TestContext.Current.CancellationToken);
            _ = Assert.Throws<InvalidDataException>(
                () => LocalSegmentFeatureExtractor.ReadWavSamples(stub));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class DurationlessProbe : ApSolutions.LocalMedia.Domain.Discovery.IMediaProbe
    {
        public Task<ApSolutions.LocalMedia.Domain.Catalog.TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApSolutions.LocalMedia.Domain.Catalog.TechnicalMetadata(
                Duration: null,
                Container: "mkv",
                VideoCodecs: [],
                AudioCodecs: [],
                Width: null,
                Height: null));
    }

    private static byte[] Wav(params byte[][] chunks)
    {
        var content = chunks.SelectMany(chunk => chunk).ToArray();
        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes((uint)(4 + content.Length)));
        bytes.AddRange("WAVE"u8.ToArray());
        bytes.AddRange(content);
        return [.. bytes];
    }

    private static byte[] FmtChunk()
    {
        var bytes = new List<byte>();
        bytes.AddRange("fmt "u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes(16u));
        bytes.AddRange(new byte[16]);
        return [.. bytes];
    }

    private static byte[] DataChunk(short[] samples, uint declaredSize)
    {
        var bytes = new List<byte>();
        bytes.AddRange("data"u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes(declaredSize));
        foreach (var sample in samples)
        {
            bytes.AddRange(BitConverter.GetBytes(sample));
        }

        return [.. bytes];
    }
}
