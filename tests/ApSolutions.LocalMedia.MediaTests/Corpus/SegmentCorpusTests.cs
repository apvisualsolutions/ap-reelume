// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>
/// Guards the frozen evaluation corpus of the segment-detection subspec: its structure must match
/// the approved table, recurring material must genuinely recur, unique material must never repeat
/// across seeds, and every episode must be producible from nothing.
/// </summary>
public sealed class SegmentCorpusTests
{
    private sealed record ExpectedSeries(
        string Id,
        CorpusSplit Split,
        int Episodes,
        double? IntroSeconds,
        double? CreditsSeconds,
        double? RecapSeconds,
        int RecapCount,
        bool HasColdOpens,
        int EpisodesWithoutSegments);

    private static readonly ExpectedSeries[] FrozenTable =
    [
        new("S01", CorpusSplit.Development, 10, 25, 30, null, 0, false, 0),
        new("S02", CorpusSplit.Development, 10, 25, 30, null, 0, true, 0),
        new("S03", CorpusSplit.Development, 9, 20, 25, 15, 6, false, 0),
        new("S04", CorpusSplit.Development, 8, 24, 30, null, 0, true, 1),
        new("S05", CorpusSplit.Development, 12, 12, 45, 15, 6, false, 0),
        new("S06", CorpusSplit.Development, 8, null, null, null, 0, false, 8),
        new("S07", CorpusSplit.HeldOut, 10, 22, 30, null, 0, true, 0),
        new("S08", CorpusSplit.HeldOut, 9, 18, 28, 15, 5, false, 1),
        new("S09", CorpusSplit.HeldOut, 11, 30, 35, null, 0, false, 0),
        new("S10", CorpusSplit.HeldOut, 8, null, null, null, 0, false, 8),
    ];

    [Fact]
    public void The_corpus_matches_the_frozen_specification_table()
    {
        Assert.Equal(FrozenTable.Length, SegmentCorpus.Series.Count);
        Assert.Equal(95, SegmentCorpus.Series.Sum(series => series.Episodes.Count));

        foreach (var expected in FrozenTable)
        {
            var series = Assert.Single(SegmentCorpus.Series, candidate => candidate.Id == expected.Id);
            Assert.Equal(expected.Split, series.Split);
            Assert.Equal(expected.Episodes, series.Episodes.Count);

            var episodesWithoutSegments = series.Episodes.Count(episode =>
                SegmentCorpus.GroundTruth(episode).Count == 0);
            Assert.Equal(expected.EpisodesWithoutSegments, episodesWithoutSegments);

            var recaps = series.Episodes
                .SelectMany(episode => episode.Pieces)
                .Where(piece => piece.Kind == SegmentPieceKind.Recap)
                .ToArray();
            Assert.Equal(expected.RecapCount, recaps.Length);
            Assert.All(recaps, recap => Assert.Equal(expected.RecapSeconds!.Value, recap.DurationSeconds));

            foreach (var episode in series.Episodes)
            {
                var intro = episode.Pieces.SingleOrDefault(piece => piece.Kind == SegmentPieceKind.Intro);
                var credits = episode.Pieces.SingleOrDefault(piece => piece.Kind == SegmentPieceKind.Credits);
                if (SegmentCorpus.GroundTruth(episode).Count == 0)
                {
                    Assert.Null(intro);
                    Assert.Null(credits);
                }
                else
                {
                    Assert.Equal(expected.IntroSeconds, intro!.DurationSeconds);
                    Assert.Equal(expected.CreditsSeconds, credits!.DurationSeconds);
                }

                var coldOpen = episode.Pieces.SingleOrDefault(piece => piece.Kind == SegmentPieceKind.ColdOpen);
                if (coldOpen is not null)
                {
                    Assert.True(expected.HasColdOpens, $"{episode.Id} has an unexpected cold open.");
                    Assert.InRange(coldOpen.DurationSeconds, 5, 45);
                }

                var body = Assert.Single(episode.Pieces, piece => piece.Kind == SegmentPieceKind.Body);
                Assert.InRange(body.DurationSeconds, 120, 230);
            }

            if (expected.HasColdOpens)
            {
                Assert.Contains(series.Episodes, episode =>
                    episode.Pieces.Any(piece => piece.Kind == SegmentPieceKind.ColdOpen));
            }
        }
    }

    [Fact]
    public void Recurring_pieces_repeat_exactly_and_unique_pieces_never_do()
    {
        // Within a series, every intro/recap/credits piece carries the series seed, so its tone
        // sequence is identical in every episode. That is what "recurring" means here.
        foreach (var series in SegmentCorpus.Series)
        {
            foreach (var kind in new[] { SegmentPieceKind.Recap, SegmentPieceKind.Intro, SegmentPieceKind.Credits })
            {
                var pieces = series.Episodes
                    .SelectMany(episode => episode.Pieces)
                    .Where(piece => piece.Kind == kind)
                    .ToArray();
                if (pieces.Length == 0)
                {
                    continue;
                }

                Assert.Single(pieces.Select(piece => piece.Seed).Distinct());
                var reference = SegmentCorpus.Tones(pieces[0]);
                Assert.All(pieces, piece => Assert.Equal(reference, SegmentCorpus.Tones(piece)));
            }
        }

        // Across different seeds, no four consecutive chords ever coincide. Four chords are ten
        // seconds; the detector's shortest recurring run is longer, so accidental matches between
        // unique material cannot reach it.
        var ownersByRun = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var series in SegmentCorpus.Series)
        {
            foreach (var piece in series.Episodes.SelectMany(episode => episode.Pieces))
            {
                var tones = SegmentCorpus.Tones(piece);
                for (var start = 0; start + 4 <= tones.Count; start++)
                {
                    var run = string.Join(
                        '|',
                        tones.Skip(start).Take(4).Select(tone => FormattableString.Invariant(
                            $"{tone.FirstHz:0.###}+{tone.SecondHz:0.###}")));
                    if (ownersByRun.TryGetValue(run, out var owner))
                    {
                        Assert.True(
                            owner == piece.Seed,
                            $"Seeds {owner} and {piece.Seed} share the tone run {run}.");
                    }
                    else
                    {
                        ownersByRun[run] = piece.Seed;
                    }
                }
            }
        }
    }

    [Fact]
    public void Ground_truth_follows_the_piece_structure()
    {
        var episode = new SegmentCorpusEpisode
        {
            Id = "SXXE01",
            RelativePath = "segments/SXX/SXXE01.mkv",
            Pieces =
            [
                new SegmentCorpusPiece { Kind = SegmentPieceKind.Recap, DurationSeconds = 15, Seed = 2 },
                new SegmentCorpusPiece { Kind = SegmentPieceKind.ColdOpen, DurationSeconds = 20, Seed = 4 },
                new SegmentCorpusPiece { Kind = SegmentPieceKind.Intro, DurationSeconds = 25, Seed = 1 },
                new SegmentCorpusPiece { Kind = SegmentPieceKind.Body, DurationSeconds = 100, Seed = 5 },
                new SegmentCorpusPiece { Kind = SegmentPieceKind.Credits, DurationSeconds = 30, Seed = 3 },
            ],
        };

        var truth = SegmentCorpus.GroundTruth(episode);

        Assert.Equal(
            [
                (MarkerKind.Recap, TimeSpan.Zero, TimeSpan.FromSeconds(15)),
                (MarkerKind.Intro, TimeSpan.FromSeconds(35), TimeSpan.FromSeconds(60)),
                (MarkerKind.Credits, TimeSpan.FromSeconds(160), TimeSpan.FromSeconds(190)),
            ],
            truth);
        Assert.Equal(TimeSpan.FromSeconds(190), SegmentCorpus.EpisodeDuration(episode));
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task The_whole_corpus_materialises_from_the_manifest()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var missing = SegmentCorpus.MissingEncoders();
        Assert.SkipWhen(missing.Count > 0, $"The local ffmpeg build lacks: {string.Join(", ", missing)}.");

        var produced = 0;
        foreach (var episode in SegmentCorpus.Series.SelectMany(series => series.Episodes))
        {
            var path = await SegmentCorpus.MaterialiseAsync(episode, TestContext.Current.CancellationToken);
            Assert.True(new FileInfo(path).Length > 0, $"The corpus generator produced an empty '{path}'.");
            produced++;
        }

        Assert.Equal(95, produced);
    }

    [Fact]
    [Trait("Category", "RealMedia")]
    public async Task An_episode_materialises_from_nothing_with_the_expected_duration()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var missing = SegmentCorpus.MissingEncoders();
        Assert.SkipWhen(missing.Count > 0, $"The local ffmpeg build lacks: {string.Join(", ", missing)}.");

        var series = SegmentCorpus.Series.Single(candidate => candidate.Id == "S03");
        var episode = series.Episodes.First(candidate =>
            candidate.Pieces.Any(piece => piece.Kind == SegmentPieceKind.Recap));

        // Lesson from the media generator: a machine with previous artifacts reuses them, which
        // hides a generator that cannot start from nothing. Delete first, then produce.
        var destination = Path.Combine(
            MediaToolchain.OutputRoot,
            episode.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        var path = await SegmentCorpus.MaterialiseAsync(episode, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(path), $"The corpus generator produced nothing at '{path}'.");
        var metadata = await new LibVlcMediaProbe().ProbeAsync(path, TestContext.Current.CancellationToken);
        Assert.NotNull(metadata.Duration);
        Assert.InRange(
            metadata.Duration!.Value.TotalSeconds,
            SegmentCorpus.EpisodeDuration(episode).TotalSeconds - 1.5,
            SegmentCorpus.EpisodeDuration(episode).TotalSeconds + 1.5);
    }
}
