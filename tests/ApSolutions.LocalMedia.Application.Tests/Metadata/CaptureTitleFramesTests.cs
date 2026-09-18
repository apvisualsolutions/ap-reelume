// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Metadata;

/// <summary>
/// The background pass that takes a frame for every title with no other cover (LIB-021, ADR-0009).
/// </summary>
/// <remarks>
/// It opens videos on its own, which is the widening of LibVLC's use the ADR accepted with its limit:
/// only files already in the library, once per title, and the result kept. These tests hold each half
/// of that limit, and the pass's manners: it yields to somebody watching or to a scan, and it tells the
/// grid in batches rather than per frame or only at the end.
/// </remarks>
public sealed class CaptureTitleFramesTests : IDisposable
{
    private static readonly CourseThumbnailStamp Stamp = new(1000, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));

    private readonly FramePaths _paths = new(Path.Combine(Path.GetTempPath(), "capture-frames-" + Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task With_nothing_to_take_nothing_is_decoded_and_nobody_is_told()
    {
        var grabber = new RecordingGrabber();
        var told = 0;

        var taken = await Subject([], grabber).ExecuteAsync(() => { told++; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        Assert.Equal(0, taken);
        Assert.Equal(0, grabber.Calls);
        Assert.Equal(0, told);
    }

    [Fact]
    public async Task A_frame_is_taken_a_tenth_of_the_way_in_where_the_resolver_looks_and_its_state_kept()
    {
        var source = Source(1, TimeSpan.FromMinutes(100));
        var grabber = new RecordingGrabber();

        var taken = await Subject([source], grabber).ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, taken);
        Assert.Equal(source.VideoPath, grabber.LastVideo);
        Assert.Equal(TimeSpan.FromMinutes(10), grabber.LastAt);
        Assert.Equal(ResolveTitlePoster.FrameFileFor(_paths, source.TitleId), grabber.LastDestination);
        Assert.Equal(Stamp, FrameStampFile.Read(grabber.LastDestination!));
    }

    [Fact]
    public async Task An_unchanged_file_is_not_decoded_twice_and_a_changed_one_is()
    {
        var source = Source(1, TimeSpan.FromMinutes(100));
        var grabber = new RecordingGrabber();
        _ = await Subject([source], grabber).ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

        _ = await Subject([source], grabber).ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, grabber.Calls);

        _ = await Subject([source with { Stamp = Stamp with { Length = 2000 } }], grabber)
            .ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, grabber.Calls);
    }

    [Fact]
    public async Task A_file_with_no_known_length_is_taken_from_its_start()
    {
        var grabber = new RecordingGrabber();

        _ = await Subject([Source(1, duration: null)], grabber).ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.Zero, grabber.LastAt);
    }

    [Fact]
    public async Task A_file_the_decoder_refuses_leaves_its_title_without_a_frame_and_the_pass_goes_on()
    {
        var refused = Source(1, TimeSpan.FromMinutes(10));
        var fine = Source(2, TimeSpan.FromMinutes(10));
        var grabber = new RecordingGrabber { Refuse = refused.VideoPath };

        var taken = await Subject([refused, fine], grabber).ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, taken);
        Assert.Equal(2, grabber.Calls);
        Assert.False(File.Exists(ResolveTitlePoster.FrameFileFor(_paths, refused.TitleId) + ".from"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Somebody_watching_or_a_scan_running_stops_the_pass_before_the_next_title(bool playing, bool scanning)
    {
        var activity = new Activity();
        var grabber = new RecordingGrabber { AfterCapture = () => { activity.Playing = playing; activity.Scanning = scanning; } };

        var taken = await Subject([Source(1, TimeSpan.FromMinutes(1)), Source(2, TimeSpan.FromMinutes(1))], grabber, activity)
            .ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, taken);
        Assert.Equal(1, grabber.Calls);
    }

    [Fact]
    public async Task The_grid_is_told_once_per_batch_that_took_something_and_once_for_the_remainder()
    {
        var sources = Enumerable.Range(1, CaptureTitleFrames.BatchSize + 5).Select(n => Source(n, TimeSpan.FromMinutes(1))).ToArray();
        var told = new List<int>();
        var grabber = new RecordingGrabber();

        _ = await Subject(sources, grabber).ExecuteAsync(
            () => { told.Add(grabber.Calls); return Task.CompletedTask; },
            TestContext.Current.CancellationToken);

        Assert.Equal([CaptureTitleFrames.BatchSize, CaptureTitleFrames.BatchSize + 5], told);
    }

    [Fact]
    public async Task A_batch_that_took_nothing_tells_nobody()
    {
        var sources = Enumerable.Range(1, CaptureTitleFrames.BatchSize).Select(n => Source(n, TimeSpan.FromMinutes(1))).ToArray();
        var grabber = new RecordingGrabber { RefuseAll = true };
        var told = 0;

        _ = await Subject(sources, grabber).ExecuteAsync(() => { told++; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        Assert.Equal(0, told);
    }

    [Fact]
    public void What_it_cannot_work_without_is_refused_where_it_is_built()
    {
        var sources = new FixedSources([]);
        var grabber = new RecordingGrabber();
        var activity = new Activity();

        _ = Assert.Throws<ArgumentNullException>(() => new CaptureTitleFrames(null!, grabber, _paths, activity, activity));
        _ = Assert.Throws<ArgumentNullException>(() => new CaptureTitleFrames(sources, null!, _paths, activity, activity));
        _ = Assert.Throws<ArgumentNullException>(() => new CaptureTitleFrames(sources, grabber, null!, activity, activity));
        _ = Assert.Throws<ArgumentNullException>(() => new CaptureTitleFrames(sources, grabber, _paths, null!, activity));
        _ = Assert.Throws<ArgumentNullException>(() => new CaptureTitleFrames(sources, grabber, _paths, activity, null!));
    }

    public void Dispose() => _paths.Dispose();

    private static TitleFrameSource Source(int n, TimeSpan? duration) => new(
        new TitleId(new Guid(n, 0, 0, new byte[8])),
        $@"D:\films\{n}.mkv",
        duration,
        Stamp);

    private CaptureTitleFrames Subject(IReadOnlyList<TitleFrameSource> sources, RecordingGrabber grabber, Activity? activity = null)
    {
        activity ??= new Activity();
        return new CaptureTitleFrames(new FixedSources(sources), grabber, _paths, activity, activity);
    }

    private sealed class FixedSources(IReadOnlyList<TitleFrameSource> sources) : ITitleFrameSources
    {
        public Task<IReadOnlyList<TitleFrameSource>> ListWithoutCoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(sources);
    }

    private sealed class Activity : IPlaybackActivity, IScanActivity
    {
        public bool Playing { get; set; }

        public bool Scanning { get; set; }

        public bool IsPlaybackActive => Playing;

        public bool IsScanActive => Scanning;
    }

    private sealed class RecordingGrabber : IVideoFrameGrabber
    {
        public int Calls { get; private set; }

        public string? LastVideo { get; private set; }

        public TimeSpan LastAt { get; private set; }

        public string? LastDestination { get; private set; }

        public string? Refuse { get; init; }

        public bool RefuseAll { get; init; }

        public Action? AfterCapture { get; init; }

        public Task<bool> TryCaptureAsync(string videoPath, TimeSpan at, string destinationPath, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastVideo = videoPath;
            LastAt = at;
            LastDestination = destinationPath;
            var succeeds = !RefuseAll && videoPath != Refuse;
            if (succeeds)
            {
                File.WriteAllBytes(destinationPath, [0x89, (byte)'P', (byte)'N', (byte)'G']);
            }

            AfterCapture?.Invoke();
            return Task.FromResult(succeeds);
        }
    }

    private sealed class FramePaths(string root) : IAppDataPaths, IDisposable
    {
        public string DataRoot { get; } = root;

        public string DatabasePath => Path.Combine(DataRoot, "library.db");

        public string SettingsPath => Path.Combine(DataRoot, "settings.json");

        public string BackupsDirectory => Path.Combine(DataRoot, "backups");

        public string PersonalArtworkDirectory => Path.Combine(DataRoot, "personal-artwork");

        public string RemoteCacheDirectory => Path.Combine(DataRoot, "cache", "artwork");

        public string CourseThumbnailDirectory => Path.Combine(DataRoot, "cache", "course-thumbnails");

        public string TitleFrameDirectory => Path.Combine(DataRoot, "cache", "title-frames");

        public string DiagnosticsDirectory => Path.Combine(DataRoot, "diagnostics");

        public string StartupRegistrySubKey => @"Software\Test";

        public string? SystemHandoffDirectory => null;

        public void Dispose()
        {
            if (Directory.Exists(DataRoot))
            {
                Directory.Delete(DataRoot, recursive: true);
            }
        }
    }
}
