// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using LibVLCSharp.Shared;
using Xunit;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// PLY-016 phase-1 spike. LibVLC 3 documents per-media video-filter options, but nothing here is
/// believed from documentation: each candidate chain is applied to a real low-resolution sample,
/// frames are captured from the same RV32 video path the engine publishes through
/// <see cref="IVideoFrameSource"/>, and the effect is the measured change in Laplacian variance —
/// the sharpness metric — against the unfiltered baseline, alongside the measured cost. The
/// baseline run is the archived RED: it records what low-resolution media look like today,
/// without any enhancement.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class LowResEnhancementSpikeTests
{
    /// <summary>
    /// DVD-era stand-in: 720×480 MPEG-2 at a starved bitrate with temporal source noise, which is
    /// the material PLY-016 exists for. Synthetic, generated locally, never redistributed.
    /// </summary>
    private const string DvdEraSampleRelativePath = "PLY16/mpeg2-480p-noisy.mkv";

    private const string DvdEraSampleRecipe =
        "-f lavfi -i testsrc2=size=720x480:rate=25,noise=alls=10:allf=t -t 6 " +
        "-f lavfi -i sine=frequency=440:duration=6 " +
        "-c:v mpeg2video -b:v 900k -pix_fmt yuv420p -c:a mp2 -b:a 128k -shortest";

    /// <summary>The existing low-resolution H.264 sample the smoke suite already exercises.</summary>
    private const string H264SampleRelativePath = "T18/h264-aac.mp4";

    private const string H264SampleRecipe =
        "-f lavfi -i testsrc2=size=320x240:rate=15:duration=3 " +
        "-f lavfi -i sine=frequency=440:duration=3 " +
        "-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -shortest";

    /// <summary>
    /// The candidate chains from the plan, in ascending cost order, plus the combined chain that
    /// denoises before sharpening so the sharpener never amplifies the noise it sits behind. Each
    /// chain runs against hardware decoding (the engine's default) and against per-media software
    /// decoding, because the D3D11 decoder hands the filter chain GPU surfaces that VLC 3 failed
    /// to convert for CPU filters — the measured round-1 finding this matrix exists to attribute.
    /// </summary>
    private static readonly (string Id, bool UseHardware, string[] Options)[] Candidates =
    [
        ("sharpen", true, [":video-filter=sharpen", ":sharpen-sigma=1.0"]),
        ("hqdn3d", true, [":video-filter=hqdn3d"]),
        ("postproc", true, [":video-filter=postproc", ":postproc-q=6"]),
        ("swscale-lanczos", true, [":swscale-mode=9"]),
        ("hqdn3d+sharpen", true, [":video-filter=hqdn3d:sharpen", ":sharpen-sigma=1.0"]),
        ("sw-sharpen", false, [":video-filter=sharpen", ":sharpen-sigma=1.0"]),
        ("sw-hqdn3d", false, [":video-filter=hqdn3d"]),
        ("sw-postproc", false, [":video-filter=postproc", ":postproc-q=6"]),
        ("sw-swscale-lanczos", false, [":swscale-mode=9"]),
        ("sw-hqdn3d+sharpen", false, [":video-filter=hqdn3d:sharpen", ":sharpen-sigma=1.0"]),
    ];

    /// <summary>
    /// RED: the corpus measured through the production engine's own frame source with no filter
    /// options at all. This is the number the enhancement has to beat, archived before any
    /// candidate runs.
    /// </summary>
    [Fact]
    public async Task Red_baseline_measures_the_unfiltered_corpus_through_the_engine_frame_source()
    {
        var rows = new List<string> { "sample,frames,meanLaplacianVariance,openToFirstFrameMs,cpuCoresBusy,wallSeconds" };
        foreach (var (relativePath, recipe) in SampleSet())
        {
            var path = await RequireSampleAsync(relativePath, recipe);
            var measurement = await MeasureEngineBaselineAsync(path);

            Assert.True(measurement.Frames > 0, $"The engine produced no frame for {relativePath}.");
            Assert.True(
                measurement.MeanLaplacianVariance > 0,
                $"The sharpness metric collapsed to zero for {relativePath}; the metric itself is broken.");
            rows.Add(measurement.ToCsvRow(SampleId(relativePath)));
        }

        WriteMeasurements("red", "PLY16-baseline-engine.csv", rows);
    }

    /// <summary>
    /// Every candidate chain must still open and decode; its measured effect and cost land in the
    /// archived CSV for the spike verdict. The harness baseline row is what makes each candidate's
    /// delta attributable to the filter and not to the harness.
    /// </summary>
    [Fact]
    public async Task Every_candidate_chain_opens_decodes_and_records_its_measured_effect()
    {
        var rows = new List<string> { "sample,candidate,frames,meanLaplacianVariance,openToFirstFrameMs,cpuCoresBusy,wallSeconds" };
        foreach (var (relativePath, recipe) in SampleSet())
        {
            var path = await RequireSampleAsync(relativePath, recipe);
            var sample = SampleId(relativePath);

            var baseline = await MeasureHarnessAsync(path, useHardware: true, []);
            Assert.True(baseline.Frames > 0, $"The harness baseline produced no frame for {sample}.");
            rows.Add(baseline.ToCsvRow(sample, "none"));

            var softwareBaseline = await MeasureHarnessAsync(path, useHardware: false, []);
            Assert.True(
                softwareBaseline.Frames > 0,
                $"The software-decoding baseline produced no frame for {sample}.");
            rows.Add(softwareBaseline.ToCsvRow(sample, "sw-none"));

            foreach (var (candidateId, useHardware, options) in Candidates)
            {
                var measured = await MeasureHarnessAsync(path, useHardware, options);
                Assert.True(
                    measured.Frames > 0,
                    $"Candidate '{candidateId}' stopped {sample} from decoding any frame.");
                rows.Add(measured.ToCsvRow(sample, candidateId));
            }
        }

        WriteMeasurements("green", "PLY16-candidates.csv", rows);
    }

    /// <summary>
    /// Control group. If per-media options had no effect, that fact is only attributable to the
    /// per-media route once the same filter demonstrably works somewhere: an instance created with
    /// <c>--video-filter=sharpen</c> must move the sharpness metric, or the whole filter mechanism
    /// is inert on this video path and the verdict changes accordingly.
    /// </summary>
    [Fact]
    public async Task Control_instance_level_sharpen_shows_whether_the_filter_mechanism_works_at_all()
    {
        var path = await RequireSampleAsync(DvdEraSampleRelativePath, DvdEraSampleRecipe);
        var baseline = await MeasureInstanceAsync(path, instanceOptions: []);
        var sharpened = await MeasureInstanceAsync(
            path,
            instanceOptions: ["--video-filter=sharpen", "--sharpen-sigma=2.0"]);

        Assert.True(baseline.Frames > 0, "The control baseline produced no frame.");
        Assert.True(sharpened.Frames > 0, "The sharpened control produced no frame.");
        WriteMeasurements(
            "green",
            "PLY16-control-instance.csv",
            [
                "sample,candidate,frames,meanLaplacianVariance,openToFirstFrameMs,cpuCoresBusy,wallSeconds",
                baseline.ToCsvRow(SampleId(DvdEraSampleRelativePath), "instance-none"),
                sharpened.ToCsvRow(SampleId(DvdEraSampleRelativePath), "instance-sharpen-2.0"),
            ]);
    }

    /// <summary>
    /// The decisive control: the same instance-level sharpen, but with the display chroma the
    /// filters actually process (planar I420) instead of the RV32 the engine's view needs. If the
    /// metric moves here and nowhere else, the forced RV32 output is the named blocker.
    /// </summary>
    [Fact]
    public async Task Control_i420_display_shows_whether_the_forced_rv32_chroma_is_the_blocker()
    {
        var path = await RequireSampleAsync(DvdEraSampleRelativePath, DvdEraSampleRecipe);
        var baseline = await MeasureInstanceAsync(path, [], useI420Sink: true);
        var sharpened = await MeasureInstanceAsync(
            path,
            ["--video-filter=sharpen", "--sharpen-sigma=2.0"],
            useI420Sink: true);

        Assert.True(baseline.Frames > 0, "The I420 control baseline produced no frame.");
        Assert.True(sharpened.Frames > 0, "The I420 sharpened control produced no frame.");
        WriteMeasurements(
            "green",
            "PLY16-control-i420.csv",
            [
                "sample,candidate,frames,meanLaplacianVariance,openToFirstFrameMs,cpuCoresBusy,wallSeconds",
                baseline.ToCsvRow(SampleId(DvdEraSampleRelativePath), "i420-none"),
                sharpened.ToCsvRow(SampleId(DvdEraSampleRelativePath), "i420-sharpen-2.0"),
            ]);
    }

    /// <summary>
    /// Measures with the filter on the LibVLC instance itself — the route the design rejected for
    /// production, used here only to prove the mechanism. The instance is created directly and
    /// disposed with the player released first, outside the factory's shared-instance registry.
    /// </summary>
    private static async Task<SpikeMeasurement> MeasureInstanceAsync(
        string path,
        string[] instanceOptions,
        bool useI420Sink = false)
    {
        Core.Initialize();
        string[] options =
        [
            "--no-metadata-network-access",
            "--no-sub-autodetect-file",
            "--no-video-title-show",
            "--aout=dummy",
            .. instanceOptions,
        ];
        using var libVlc = new LibVLC(options);
        var filterLog = new List<string>();
        var logSync = new Lock();
        libVlc.Log += (_, args) =>
        {
            // Only the lines that say what happened to the filter chain; the rest is decoder noise.
            if (args.Message.Contains("filter", StringComparison.OrdinalIgnoreCase)
                || args.Message.Contains("sharpen", StringComparison.OrdinalIgnoreCase))
            {
                lock (logSync)
                {
                    filterLog.Add(FormattableString.Invariant($"[{args.Level}] {args.Module}: {args.Message}"));
                }
            }
        };
        using var player = new VlcMediaPlayer(libVlc);
        using var media = new LibVLCSharp.Shared.Media(libVlc, path, FromType.FromPath);
        foreach (var option in LibVlcVideoCapabilities.AccelerationOptions(useHardware: true))
        {
            media.AddOption(option);
        }

        using var collector = new FrameSharpnessCollector();
        using var sink = useI420Sink
            ? new I420SpikeVideoSink(player, collector)
            : (IDisposable)new SpikeVideoSink(player, collector);
        player.Media = media;

        var opening = Stopwatch.StartNew();
        _ = player.Play();
        var measurement = await collector.CollectAsync(opening);
        player.SetPause(pause: true);
        player.Stop();

        // The same quiescence the factory observes: the media rests before its handle is disposed,
        // and the player is disposed after it by the using order above.
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        lock (logSync)
        {
            if (filterLog.Count > 0)
            {
                var chromaSuffix = useI420Sink ? "i420" : "rv32";
                var filterSuffix = instanceOptions.Length == 0 ? "baseline" : "sharpen";
                WriteMeasurements(
                    "green",
                    $"PLY16-control-vlc-log-{chromaSuffix}-{filterSuffix}.txt",
                    filterLog);
            }
        }

        return measurement;
    }

    private static IEnumerable<(string RelativePath, string Recipe)> SampleSet() =>
    [
        (DvdEraSampleRelativePath, DvdEraSampleRecipe),
        (H264SampleRelativePath, H264SampleRecipe),
    ];

    private static string SampleId(string relativePath) =>
        Path.GetFileNameWithoutExtension(relativePath);

    /// <summary>Measures the unfiltered corpus through the real engine and its frame source.</summary>
    private static async Task<SpikeMeasurement> MeasureEngineBaselineAsync(string path)
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        using var collector = new FrameSharpnessCollector();
        engine.FrameRendered += collector.OnFrameRendered;

        var opening = Stopwatch.StartNew();
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var measurement = await collector.CollectAsync(opening);
        await engine.StopAsync(TestContext.Current.CancellationToken);
        return measurement;
    }

    /// <summary>
    /// Measures one option chain through a player wired exactly like the engine's video sink: the
    /// same RV32 format callbacks, the same acceleration options — plus the chain under test,
    /// which the production engine cannot yet carry. That gap is what phase 2 closes.
    /// </summary>
    private static async Task<SpikeMeasurement> MeasureHarnessAsync(
        string path,
        bool useHardware,
        string[] candidateOptions)
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        var player = factory.CreateMediaPlayer();
        var media = factory.CreateMedia(path);
        try
        {
            foreach (var option in LibVlcVideoCapabilities.AccelerationOptions(useHardware))
            {
                media.AddOption(option);
            }

            foreach (var option in candidateOptions)
            {
                media.AddOption(option);
            }

            using var collector = new FrameSharpnessCollector();
            using var sink = new SpikeVideoSink(player, collector);
            player.Media = media;

            var opening = Stopwatch.StartNew();
            _ = player.Play();
            var measurement = await collector.CollectAsync(opening);
            player.SetPause(pause: true);
            player.Stop();
            return measurement;
        }
        finally
        {
            factory.DeferRelease(media);
            factory.ReleaseMediaPlayer(player);
        }
    }

    private static async Task<string> RequireSampleAsync(string relativePath, string recipe)
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        return await MediaToolchain.EnsureSampleAsync(
            relativePath,
            recipe,
            TestContext.Current.CancellationToken);
    }

    private static void WriteMeasurements(string phase, string fileName, IEnumerable<string> rows)
    {
        var destination = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "PLY16",
            phase,
            fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllLines(destination, rows);
    }

    private sealed record SpikeMeasurement(
        int Frames,
        double MeanLaplacianVariance,
        double OpenToFirstFrameMs,
        double CpuCoresBusy,
        double WallSeconds)
    {
        public string ToCsvRow(string sample, string? candidate = null)
        {
            var prefix = candidate is null ? sample : $"{sample},{candidate}";
            return FormattableString.Invariant(
                $"{prefix},{Frames},{MeanLaplacianVariance:F2},{OpenToFirstFrameMs:F0},{CpuCoresBusy:F3},{WallSeconds:F2}");
        }
    }

    /// <summary>
    /// Computes the sharpness of every delivered frame and aggregates the run. The identical
    /// instrumentation runs for the baseline and for every candidate, so measured cost deltas
    /// are attributable to the filter chain and never to the metric.
    /// </summary>
    private sealed class FrameSharpnessCollector : IDisposable
    {
        private static readonly TimeSpan MeasurementWindow = TimeSpan.FromSeconds(2.5);
        private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(15);
        private readonly Lock _sync = new();
        private readonly List<double> _variances = [];
        private readonly TaskCompletionSource _firstFrame =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnFrameRendered(object? sender, VideoFrameEventArgs args)
        {
            var variance = LaplacianVariance(args.Pixels.Span, args.Width, args.Height, args.Stride);
            Record(variance);
        }

        /// <summary>For sinks that already deliver a luma plane; no colour conversion involved.</summary>
        public void OnGreyPlane(ReadOnlySpan<byte> greyPlane, int width, int height, int stride)
        {
            var grey = new float[width * height];
            for (var y = 0; y < height; y++)
            {
                var row = y * stride;
                for (var x = 0; x < width; x++)
                {
                    grey[(y * width) + x] = greyPlane[row + x];
                }
            }

            Record(VarianceOfLaplacian(grey, width, height));
        }

        private void Record(double variance)
        {
            lock (_sync)
            {
                _variances.Add(variance);
            }

            _firstFrame.TrySetResult();
        }

        /// <summary>Waits for the first frame, keeps collecting for the window, then aggregates.</summary>
        public async Task<SpikeMeasurement> CollectAsync(Stopwatch opening)
        {
            var arrived = await Task.WhenAny(
                _firstFrame.Task,
                Task.Delay(FirstFrameTimeout, TestContext.Current.CancellationToken));
            var openToFirstFrameMs = opening.Elapsed.TotalMilliseconds;
            if (arrived != _firstFrame.Task)
            {
                return new SpikeMeasurement(0, 0, openToFirstFrameMs, 0, 0);
            }

            var process = Process.GetCurrentProcess();
            process.Refresh();
            var cpuBefore = process.TotalProcessorTime;
            var window = Stopwatch.StartNew();
            await Task.Delay(MeasurementWindow, TestContext.Current.CancellationToken);
            window.Stop();
            process.Refresh();
            var cpuBusy = (process.TotalProcessorTime - cpuBefore).TotalSeconds
                / window.Elapsed.TotalSeconds;

            lock (_sync)
            {
                var mean = _variances.Count > 0 ? _variances.Average() : 0;
                return new SpikeMeasurement(
                    _variances.Count,
                    mean,
                    openToFirstFrameMs,
                    cpuBusy,
                    window.Elapsed.TotalSeconds);
            }
        }

        public void Dispose() => _firstFrame.TrySetCanceled();

        /// <summary>
        /// Variance of the 3×3 Laplacian over the grey plane: the standard no-reference sharpness
        /// metric. A sharper picture has stronger second derivatives and a higher variance.
        /// </summary>
        private static double LaplacianVariance(ReadOnlySpan<byte> bgra, int width, int height, int stride)
        {
            if (width < 3 || height < 3)
            {
                return 0;
            }

            var grey = new float[width * height];
            for (var y = 0; y < height; y++)
            {
                var row = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var pixel = row + (x * 4);
                    grey[(y * width) + x] =
                        (0.114f * bgra[pixel]) + (0.587f * bgra[pixel + 1]) + (0.299f * bgra[pixel + 2]);
                }
            }

            return VarianceOfLaplacian(grey, width, height);
        }

        private static double VarianceOfLaplacian(float[] grey, int width, int height)
        {
            if (width < 3 || height < 3)
            {
                return 0;
            }

            double sum = 0;
            double sumOfSquares = 0;
            long count = 0;
            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var centre = (y * width) + x;
                    var laplacian = grey[centre - width] + grey[centre + width]
                        + grey[centre - 1] + grey[centre + 1]
                        - (4f * grey[centre]);
                    sum += laplacian;
                    sumOfSquares += laplacian * laplacian;
                    count++;
                }
            }

            var meanValue = sum / count;
            return (sumOfSquares / count) - (meanValue * meanValue);
        }
    }

    /// <summary>
    /// The engine's video sink, replicated verbatim for the spike: RV32 into process memory with
    /// the frame republished per display callback. Keeping the callbacks alive for the player's
    /// lifetime is the same delegate-rooting discipline the engine observes.
    /// </summary>
    private sealed class SpikeVideoSink : IDisposable
    {
        private const uint MaximumFrameWidth = 3840;
        private const uint MaximumFrameHeight = 2160;

        private readonly FrameSharpnessCollector _collector;
        private readonly VlcMediaPlayer.LibVLCVideoFormatCb _formatCallback;
        private readonly VlcMediaPlayer.LibVLCVideoCleanupCb _cleanupCallback;
        private readonly VlcMediaPlayer.LibVLCVideoLockCb _lockCallback;
        private readonly VlcMediaPlayer.LibVLCVideoDisplayCb _displayCallback;
        private nint _frameBuffer;
        private byte[]? _managedFrame;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameStride;

        public SpikeVideoSink(VlcMediaPlayer player, FrameSharpnessCollector collector)
        {
            _collector = collector;
            _formatCallback = OnVideoFormat;
            _cleanupCallback = OnVideoCleanup;
            _lockCallback = (_, planes) =>
            {
                Marshal.WriteIntPtr(planes, _frameBuffer);
                return nint.Zero;
            };
            _displayCallback = (_, _) =>
            {
                if (_managedFrame is not { } managed)
                {
                    return;
                }

                Marshal.Copy(_frameBuffer, managed, 0, managed.Length);
                _collector.OnFrameRendered(
                    this,
                    new VideoFrameEventArgs(managed, _frameWidth, _frameHeight, _frameStride));
            };

            player.SetVideoFormatCallbacks(_formatCallback, _cleanupCallback);
            player.SetVideoCallbacks(_lockCallback, null, _displayCallback);
        }

        public void Dispose() => ReleaseFrameBuffer();

        private uint OnVideoFormat(
            ref nint opaque,
            nint chroma,
            ref uint width,
            ref uint height,
            ref uint pitches,
            ref uint lines)
        {
            width = Math.Clamp(width, 1, MaximumFrameWidth);
            height = Math.Clamp(height, 1, MaximumFrameHeight);
            Marshal.Copy("RV32"u8.ToArray(), 0, chroma, 4);
            pitches = width * 4;
            lines = height;

            ReleaseFrameBuffer();
            _frameWidth = (int)width;
            _frameHeight = (int)height;
            _frameStride = (int)pitches;
            _frameBuffer = Marshal.AllocHGlobal(_frameStride * _frameHeight);
            _managedFrame = new byte[_frameStride * _frameHeight];
            return 1;
        }

        private void OnVideoCleanup(ref nint opaque) => ReleaseFrameBuffer();

        private void ReleaseFrameBuffer()
        {
            if (_frameBuffer == nint.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(_frameBuffer);
            _frameBuffer = nint.Zero;
            _managedFrame = null;
        }
    }

    /// <summary>
    /// Diagnostic sink that asks for planar I420 — the chroma VLC's CPU video filters process —
    /// and measures the luma plane directly. Exists only to attribute the round-1/round-2 finding:
    /// it is never what the production view could consume.
    /// </summary>
    private sealed class I420SpikeVideoSink : IDisposable
    {
        private readonly FrameSharpnessCollector _collector;
        private readonly VlcMediaPlayer.LibVLCVideoFormatCb _formatCallback;
        private readonly VlcMediaPlayer.LibVLCVideoCleanupCb _cleanupCallback;
        private readonly VlcMediaPlayer.LibVLCVideoLockCb _lockCallback;
        private readonly VlcMediaPlayer.LibVLCVideoDisplayCb _displayCallback;
        private nint _lumaPlane;
        private nint _chromaUPlane;
        private nint _chromaVPlane;
        private byte[]? _managedLuma;
        private int _frameWidth;
        private int _frameHeight;

        public I420SpikeVideoSink(VlcMediaPlayer player, FrameSharpnessCollector collector)
        {
            _collector = collector;
            _formatCallback = OnVideoFormat;
            _cleanupCallback = OnVideoCleanup;
            _lockCallback = (_, planes) =>
            {
                Marshal.WriteIntPtr(planes, 0, _lumaPlane);
                Marshal.WriteIntPtr(planes, nint.Size, _chromaUPlane);
                Marshal.WriteIntPtr(planes, 2 * nint.Size, _chromaVPlane);
                return nint.Zero;
            };
            _displayCallback = (_, _) =>
            {
                if (_managedLuma is not { } managed)
                {
                    return;
                }

                Marshal.Copy(_lumaPlane, managed, 0, managed.Length);
                _collector.OnGreyPlane(managed, _frameWidth, _frameHeight, _frameWidth);
            };

            player.SetVideoFormatCallbacks(_formatCallback, _cleanupCallback);
            player.SetVideoCallbacks(_lockCallback, null, _displayCallback);
        }

        public void Dispose() => ReleasePlanes();

        private uint OnVideoFormat(
            ref nint opaque,
            nint chroma,
            ref uint width,
            ref uint height,
            ref uint pitches,
            ref uint lines)
        {
            Marshal.Copy("I420"u8.ToArray(), 0, chroma, 4);

            // The native signature carries one pitch and one line count per plane; the binding
            // exposes the first element by reference, so the chroma planes are reached through it.
            pitches = width;
            Unsafe.Add(ref pitches, 1) = width / 2;
            Unsafe.Add(ref pitches, 2) = width / 2;
            lines = height;
            Unsafe.Add(ref lines, 1) = height / 2;
            Unsafe.Add(ref lines, 2) = height / 2;

            ReleasePlanes();
            _frameWidth = (int)width;
            _frameHeight = (int)height;
            _lumaPlane = Marshal.AllocHGlobal(_frameWidth * _frameHeight);
            _chromaUPlane = Marshal.AllocHGlobal(_frameWidth * _frameHeight / 4);
            _chromaVPlane = Marshal.AllocHGlobal(_frameWidth * _frameHeight / 4);
            _managedLuma = new byte[_frameWidth * _frameHeight];
            return 1;
        }

        private void OnVideoCleanup(ref nint opaque) => ReleasePlanes();

        private void ReleasePlanes()
        {
            if (_lumaPlane == nint.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(_lumaPlane);
            Marshal.FreeHGlobal(_chromaUPlane);
            Marshal.FreeHGlobal(_chromaVPlane);
            _lumaPlane = nint.Zero;
            _chromaUPlane = nint.Zero;
            _chromaVPlane = nint.Zero;
            _managedLuma = null;
        }
    }
}
