// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Playback;

namespace ApSolutions.LocalMedia.Infrastructure.Media;

/// <summary>
/// The per-second audio fingerprints of one episode's opening and closing windows. Nothing else of
/// the episode is ever read: extraction is bounded by design.
/// </summary>
public sealed record EpisodeAudioFingerprints(
    MediaFileId FileId,
    TimeSpan Duration,
    IReadOnlyList<float[]> Opening,
    TimeSpan ClosingStart,
    IReadOnlyList<float[]> Closing);

/// <summary>
/// Reads the bounded windows of one episode into compact per-second fingerprints, locally and
/// without any network. The seam exists so detection logic can be exercised without decoding.
/// </summary>
public interface ISegmentFeatureExtractor
{
    Task<EpisodeAudioFingerprints> ExtractAsync(
        SegmentDetectionEpisode episode,
        CancellationToken cancellationToken);
}

/// <summary>
/// Decodes the opening and closing windows of an episode with the embedded LibVLC engine and turns
/// them into per-second spectral fingerprints. The decode is bounded, cancelable, and never opens a
/// connection; fingerprints are small enough to cache, so no window is ever decoded twice while the
/// process lives.
/// </summary>
public sealed class LocalSegmentFeatureExtractor : ISegmentFeatureExtractor
{
    /// <summary>Mono at this rate keeps a window under two megabytes and the spectrum wide enough.</summary>
    private const int SampleRateHz = 8000;

    private const int FftSize = 1024;

    private const int HopSize = 512;

    /// <summary>Linear bands of ~59 Hz between ~100 and ~2000 Hz; music and speech live there.</summary>
    private const int BandCount = 32;

    private const int FirstBin = 13;

    private const int LastBin = 256;

    private static readonly TimeSpan OpeningWindowLimit = TimeSpan.FromSeconds(180);

    private static readonly TimeSpan ClosingWindowLimit = TimeSpan.FromSeconds(90);

    /// <summary>A decode that produces nothing within this bound is a failure, never a wait.</summary>
    private static readonly TimeSpan DecodeTimeout = TimeSpan.FromMinutes(4);

    private readonly LibVlcFactory _factory;

    private readonly IMediaProbe _probe;

    private readonly ConcurrentDictionary<string, EpisodeAudioFingerprints> _cache =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The probe is the existing one on purpose: parsing a media and disposing it immediately is
    /// the native failure mode <see cref="LibVlcMediaProbe"/> already works around with its
    /// deferred release, and reproducing that crash here is how this dependency was learnt.
    /// </summary>
    public LocalSegmentFeatureExtractor(LibVlcFactory factory, IMediaProbe? probe = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _probe = probe ?? new LibVlcMediaProbe(_factory);
    }

    public async Task<EpisodeAudioFingerprints> ExtractAsync(
        SegmentDetectionEpisode episode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var file = new FileInfo(episode.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The episode file could not be found.", episode.Path);
        }

        var cacheKey = FormattableString.Invariant(
            $"{episode.Path}|{file.Length}|{file.LastWriteTimeUtc.Ticks}");
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached with { FileId = episode.FileId };
        }

        var duration = episode.Duration
            ?? await ProbeDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
        var halves = TimeSpan.FromTicks(duration.Ticks / 2);
        var openingWindow = Min(OpeningWindowLimit, halves);
        var closingWindow = Min(ClosingWindowLimit, halves);
        var closingStart = duration - closingWindow;

        var opening = Fingerprint(await DecodeWindowAsync(
            episode.Path,
            TimeSpan.Zero,
            openingWindow,
            cancellationToken).ConfigureAwait(false));
        var closing = Fingerprint(await DecodeWindowAsync(
            episode.Path,
            closingStart,
            duration,
            cancellationToken).ConfigureAwait(false));

        var result = new EpisodeAudioFingerprints(episode.FileId, duration, opening, closingStart, closing);
        _cache[cacheKey] = result;
        return result;
    }

    /// <summary>Cosine similarity of two fingerprints; near-silence never matches anything.</summary>
    public static double Similarity(float[] first, float[] second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Length == 0 || second.Length == 0)
        {
            return 0;
        }

        double dot = 0;
        for (var index = 0; index < first.Length && index < second.Length; index++)
        {
            dot += first[index] * second[index];
        }

        return dot;
    }

    private async Task<TimeSpan> ProbeDurationAsync(string path, CancellationToken cancellationToken)
    {
        var metadata = await _probe.ProbeAsync(path, cancellationToken).ConfigureAwait(false);
        return metadata.Duration
            ?? throw new InvalidDataException("The duration of the episode could not be read.");
    }

    /// <summary>
    /// Decodes one bounded window into a temporary mono WAV through the engine's stream output,
    /// which runs as fast as the machine decodes rather than in real time, and reads it back.
    /// </summary>
    private async Task<short[]> DecodeWindowAsync(
        string path,
        TimeSpan start,
        TimeSpan stop,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ApSolutions.LocalMedia", "segment-extraction");
        _ = Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, FormattableString.Invariant($"window-{Guid.NewGuid():N}.wav"));
        try
        {
            var media = _factory.CreateMedia(path);
            var player = _factory.CreateMediaPlayer();
            try
            {
                media.AddOption(":no-video");
                media.AddOption(FormattableString.Invariant(
                    $":start-time={start.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}"));
                media.AddOption(FormattableString.Invariant(
                    $":stop-time={stop.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}"));
                // The video branch stays in the pipeline on purpose. It cannot be muxed into a WAV
                // and the engine says so in its log for every episode — but removing it (with
                // no-sout-video, measured) makes start-time seek so coarsely that half the closing
                // windows land seconds away and the boundaries stop being true. Noise in a log
                // loses to wrong timestamps.
                media.AddOption(
                    $":sout=#transcode{{acodec=s16l,channels=1,samplerate={SampleRateHz}}}"
                    + $":std{{access=file,mux=wav,dst=\"{temporary.Replace('\\', '/')}\"}}");

                var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                player.EndReached += OnEnded;
                player.EncounteredError += OnFailed;
                try
                {
                    player.Media = media;
                    if (!player.Play())
                    {
                        throw new InvalidDataException("The engine refused to read the episode window.");
                    }

                    await finished.Task
                        .WaitAsync(DecodeTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    player.EndReached -= OnEnded;
                    player.EncounteredError -= OnFailed;

                    // Stopping a player that is still playing crashes this LibVLC build; pausing
                    // first drains the decoder — the same discipline the engine's teardown keeps.
                    if (player.IsPlaying)
                    {
                        player.SetPause(pause: true);
                    }

                    // Stop flushes and closes the stream output; the file is not complete before it.
                    player.Stop();
                }

                void OnEnded(object? sender, EventArgs args) => finished.TrySetResult();

                void OnFailed(object? sender, EventArgs args) =>
                    finished.TrySetException(
                        new InvalidDataException("The engine failed while reading the episode window."));
            }
            finally
            {
                // Every native media is released before the player that referenced it, and only
                // after the quiescence window — releasing the player first crashes the LibVLC
                // teardown path, and disposing the media immediately is the native failure mode
                // this file's own probe dependency exists to avoid.
                player.Media = null;
                _factory.DeferRelease(media);
                _factory.ReleaseMediaPlayer(player);
            }

            return ReadWavSamples(temporary);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Reads the sample data of a canonical 16-bit mono WAV, tolerating a size the writer never
    /// went back to close. Public for the same reason <see cref="Similarity"/> is: the parser is a
    /// pure function whose edges deserve direct tests.
    /// </summary>
    public static short[] ReadWavSamples(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
            var chunkSize = BitConverter.ToUInt32(bytes, offset + 4);
            offset += 8;
            if (string.Equals(chunkId, "data", StringComparison.Ordinal))
            {
                var available = bytes.Length - offset;
                var length = chunkSize > (uint)available ? available : (int)chunkSize;
                var samples = new short[length / 2];
                for (var index = 0; index < samples.Length; index++)
                {
                    samples[index] = BitConverter.ToInt16(bytes, offset + (index * 2));
                }

                return samples;
            }

            offset += (int)Math.Min(chunkSize, (uint)(bytes.Length - offset));
        }

        throw new InvalidDataException("The decoded window carries no sample data.");
    }

    /// <summary>One normalized log-power band vector per second of audio.</summary>
    private static List<float[]> Fingerprint(short[] samples)
    {
        var seconds = samples.Length / SampleRateHz;
        var prints = new List<float[]>(seconds);
        var window = new double[FftSize];
        for (var index = 0; index < FftSize; index++)
        {
            window[index] = 0.5 - (0.5 * Math.Cos(2 * Math.PI * index / (FftSize - 1)));
        }

        var real = new double[FftSize];
        var imaginary = new double[FftSize];
        for (var second = 0; second < seconds; second++)
        {
            var bands = new double[BandCount];
            var frameStart = second * SampleRateHz;
            var frameEnd = frameStart + SampleRateHz - FftSize;
            for (var offset = frameStart; offset <= frameEnd; offset += HopSize)
            {
                for (var index = 0; index < FftSize; index++)
                {
                    real[index] = samples[offset + index] / 32768.0 * window[index];
                    imaginary[index] = 0;
                }

                Fft(real, imaginary);
                for (var bin = FirstBin; bin < LastBin; bin++)
                {
                    var band = (bin - FirstBin) * BandCount / (LastBin - FirstBin);
                    bands[band] += (real[bin] * real[bin]) + (imaginary[bin] * imaginary[bin]);
                }
            }

            // Amplitude rather than log power: a logarithm lifts the codec's noise floor into the
            // same order as the real peaks, and then every second resembles every other. Measured:
            // with log compression two unrelated episodes sat at 0.90+ cosine across the board.
            var print = new float[BandCount];
            double energy = 0;
            double mean = 0;
            for (var band = 0; band < BandCount; band++)
            {
                energy += bands[band];
                var value = Math.Sqrt(bands[band]);
                print[band] = (float)value;
                mean += value;
            }

            // Centring removes whatever baseline the bands share, so the comparison sees the shape
            // of the spectrum rather than its floor.
            mean /= BandCount;
            double norm = 0;
            for (var band = 0; band < BandCount; band++)
            {
                print[band] -= (float)mean;
                norm += print[band] * print[band];
            }

            // A near-silent or perfectly flat second matches nothing: normalising noise or a zero
            // vector would invent similarity out of arithmetic.
            if (energy < 1e-6 || norm <= 0)
            {
                prints.Add(new float[BandCount]);
                continue;
            }

            var scale = (float)(1 / Math.Sqrt(norm));
            for (var band = 0; band < BandCount; band++)
            {
                print[band] *= scale;
            }

            prints.Add(print);
        }

        return prints;
    }

    /// <summary>In-place iterative radix-2 FFT; the size is a fixed power of two.</summary>
    private static void Fft(double[] real, double[] imaginary)
    {
        var length = real.Length;
        for (int index = 1, reversed = 0; index < length; index++)
        {
            var bit = length >> 1;
            for (; (reversed & bit) != 0; bit >>= 1)
            {
                reversed ^= bit;
            }

            reversed |= bit;
            if (index < reversed)
            {
                (real[index], real[reversed]) = (real[reversed], real[index]);
                (imaginary[index], imaginary[reversed]) = (imaginary[reversed], imaginary[index]);
            }
        }

        for (var stride = 2; stride <= length; stride <<= 1)
        {
            var angle = -2 * Math.PI / stride;
            var stepReal = Math.Cos(angle);
            var stepImaginary = Math.Sin(angle);
            for (var block = 0; block < length; block += stride)
            {
                double factorReal = 1;
                double factorImaginary = 0;
                for (var pair = 0; pair < stride / 2; pair++)
                {
                    var even = block + pair;
                    var odd = block + pair + (stride / 2);
                    var productReal = (real[odd] * factorReal) - (imaginary[odd] * factorImaginary);
                    var productImaginary = (real[odd] * factorImaginary) + (imaginary[odd] * factorReal);
                    real[odd] = real[even] - productReal;
                    imaginary[odd] = imaginary[even] - productImaginary;
                    real[even] += productReal;
                    imaginary[even] += productImaginary;
                    var nextReal = (factorReal * stepReal) - (factorImaginary * stepImaginary);
                    factorImaginary = (factorReal * stepImaginary) + (factorImaginary * stepReal);
                    factorReal = nextReal;
                }
            }
        }
    }

    private static TimeSpan Min(TimeSpan first, TimeSpan second) => first <= second ? first : second;
}
