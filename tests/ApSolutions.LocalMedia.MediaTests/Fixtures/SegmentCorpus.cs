// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.MediaTests.Fixtures;

/// <summary>Which half of the corpus a series belongs to; held-out series are never tuned against.</summary>
internal enum CorpusSplit
{
    Development,
    HeldOut,
}

/// <summary>One building block of an episode. Only recap, intro, and credits are ground truth.</summary>
internal enum SegmentPieceKind
{
    Recap,
    ColdOpen,
    Intro,
    Body,
    Credits,
}

internal sealed record SegmentCorpusPiece
{
    [JsonConverter(typeof(JsonStringEnumConverter<SegmentPieceKind>))]
    public SegmentPieceKind Kind { get; init; }

    public double DurationSeconds { get; init; }

    public int Seed { get; init; }
}

internal sealed record SegmentCorpusEpisode
{
    public required string Id { get; init; }

    public required string RelativePath { get; init; }

    public IReadOnlyList<SegmentCorpusPiece> Pieces { get; init; } = [];
}

internal sealed record SegmentCorpusSeries
{
    public required string Id { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<CorpusSplit>))]
    public CorpusSplit Split { get; init; }

    public IReadOnlyList<SegmentCorpusEpisode> Episodes { get; init; } = [];
}

internal sealed record SegmentCorpusGenerator
{
    public int SampleRateHz { get; init; }

    public double ToneSeconds { get; init; }

    public double BaseFrequencyHz { get; init; }

    public double FrequencyStepHz { get; init; }

    public int FrequencyCount { get; init; }

    public int VideoWidth { get; init; }

    public int VideoHeight { get; init; }

    public int VideoFrameRate { get; init; }

    public string Container { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredEncoders { get; init; } = [];
}

internal sealed record SegmentCorpusDocument
{
    public int FormatVersion { get; init; }

    public string ProvenanceStatement { get; init; } = string.Empty;

    public required SegmentCorpusGenerator Generator { get; init; }

    public IReadOnlyList<SegmentCorpusSeries> Series { get; init; } = [];
}

/// <summary>
/// The frozen multi-show evaluation corpus of the segment-detection subspec. The repository stores
/// only this structure; every episode is synthesised on demand from deterministic tone sequences,
/// so nothing personal or third-party can ever enter the benchmark.
/// </summary>
internal static class SegmentCorpus
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static SegmentCorpusDocument Document { get; } = Load();

    public static SegmentCorpusGenerator Generator => Document.Generator;

    public static IReadOnlyList<SegmentCorpusSeries> Series => Document.Series;

    public static IEnumerable<SegmentCorpusSeries> Split(CorpusSplit split) =>
        Series.Where(series => series.Split == split);

    /// <summary>
    /// The two-sine chord the piece plays at the given tone index. One deterministic implementation
    /// exists on purpose: recipes and any expectation derive from this exact function. A chord
    /// rather than a single tone makes the per-step space large enough that unique material never
    /// repeats a run by accident.
    /// </summary>
    public static (double FirstHz, double SecondHz) ToneChord(int seed, int toneIndex) =>
        (Frequency(seed, toneIndex, 2246822519u), Frequency(seed, toneIndex, 3266489917u));

    /// <summary>The chord sequence of one piece, last chord possibly shorter than the tone length.</summary>
    public static IReadOnlyList<(double FirstHz, double SecondHz, double DurationSeconds)> Tones(
        SegmentCorpusPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        var tones = new List<(double, double, double)>();
        var remaining = piece.DurationSeconds;
        for (var index = 0; remaining > 0; index++)
        {
            var duration = Math.Min(Generator.ToneSeconds, remaining);
            var (first, second) = ToneChord(piece.Seed, index);
            tones.Add((first, second, duration));
            remaining -= duration;
        }

        return tones;
    }

    private static double Frequency(int seed, int toneIndex, uint salt)
    {
        var hash = (uint)seed * 2654435761u;
        hash ^= (uint)toneIndex * salt;
        hash ^= hash >> 13;
        hash *= 2654435769u;
        hash ^= hash >> 16;
        return Generator.BaseFrequencyHz + (Generator.FrequencyStepHz * (hash % (uint)Generator.FrequencyCount));
    }

    /// <summary>Ground truth of an episode: the recap/intro/credits ranges its structure implies.</summary>
    public static IReadOnlyList<(MarkerKind Kind, TimeSpan Start, TimeSpan End)> GroundTruth(
        SegmentCorpusEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var truth = new List<(MarkerKind, TimeSpan, TimeSpan)>();
        var offset = TimeSpan.Zero;
        foreach (var piece in episode.Pieces)
        {
            var end = offset + TimeSpan.FromSeconds(piece.DurationSeconds);
            MarkerKind? kind = piece.Kind switch
            {
                SegmentPieceKind.Recap => MarkerKind.Recap,
                SegmentPieceKind.Intro => MarkerKind.Intro,
                SegmentPieceKind.Credits => MarkerKind.Credits,
                _ => null,
            };
            if (kind is { } marker)
            {
                truth.Add((marker, offset, end));
            }

            offset = end;
        }

        return truth;
    }

    public static TimeSpan EpisodeDuration(SegmentCorpusEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return TimeSpan.FromSeconds(episode.Pieces.Sum(piece => piece.DurationSeconds));
    }

    /// <summary>The encoders the corpus needs that the local toolchain cannot provide.</summary>
    public static IReadOnlyList<string> MissingEncoders() =>
        [.. Generator.RequiredEncoders.Where(encoder => !MediaToolchain.HasEncoder(encoder))];

    /// <summary>Synthesises the episode under the ignored artifacts tree and returns its path.</summary>
    public static Task<string> MaterialiseAsync(SegmentCorpusEpisode episode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return MediaToolchain.EnsureSampleAsync(episode.RelativePath, BuildRecipe(episode), cancellationToken);
    }

    /// <summary>
    /// The complete ffmpeg argument list for one episode: one colour clip and one tone chain per
    /// piece, concatenated in order. Everything is synthetic; no input file is ever read.
    /// </summary>
    public static string BuildRecipe(SegmentCorpusEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var graph = new StringBuilder();
        var concatInputs = new StringBuilder();
        for (var pieceIndex = 0; pieceIndex < episode.Pieces.Count; pieceIndex++)
        {
            var piece = episode.Pieces[pieceIndex];
            _ = graph.Append(CultureInfo.InvariantCulture,
                $"color=c=0x{PieceColour(piece):x6}:size={Generator.VideoWidth}x{Generator.VideoHeight}" +
                $":rate={Generator.VideoFrameRate}:duration={Seconds(piece.DurationSeconds)}[v{pieceIndex}];");

            var tones = Tones(piece);
            for (var toneIndex = 0; toneIndex < tones.Count; toneIndex++)
            {
                var (first, second, duration) = tones[toneIndex];
                _ = graph.Append(CultureInfo.InvariantCulture,
                    $"aevalsrc=exprs='0.5*(sin(2*PI*{Seconds(first)}*t)+sin(2*PI*{Seconds(second)}*t))'" +
                    $":d={Seconds(duration)}:s={Generator.SampleRateHz}[t{pieceIndex}_{toneIndex}];");
            }

            var toneLabels = string.Concat(Enumerable.Range(0, tones.Count)
                .Select(toneIndex => FormattableString.Invariant($"[t{pieceIndex}_{toneIndex}]")));
            _ = graph.Append(CultureInfo.InvariantCulture,
                $"{toneLabels}concat=n={tones.Count}:v=0:a=1[a{pieceIndex}];");
            _ = concatInputs.Append(CultureInfo.InvariantCulture, $"[v{pieceIndex}][a{pieceIndex}]");
        }

        _ = graph.Append(CultureInfo.InvariantCulture,
            $"{concatInputs}concat=n={episode.Pieces.Count}:v=1:a=1[vout][aout]");
        return
            $"-filter_complex \"{graph}\" -map [vout] -map [aout] " +
            "-c:v mpeg4 -q:v 12 -c:a aac -b:a 32k -ac 1";
    }

    /// <summary>A deterministic colour per piece so a human can see the structure while it plays.</summary>
    private static int PieceColour(SegmentCorpusPiece piece) => piece.Kind switch
    {
        SegmentPieceKind.Recap => 0x5a2a2a,
        SegmentPieceKind.ColdOpen => 0x2a3a5a,
        SegmentPieceKind.Intro => 0x2a5a3a + ((piece.Seed % 8) * 0x000010),
        SegmentPieceKind.Credits => 0x3a3a3a,
        _ => 0x1a1a2a + ((piece.Seed % 16) * 0x000100),
    };

    private static string Seconds(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static SegmentCorpusDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "segment-corpus-manifest.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The segment corpus manifest was not copied to '{path}'.");
        }

        return JsonSerializer.Deserialize<SegmentCorpusDocument>(File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidOperationException("The segment corpus manifest could not be read.");
    }
}
