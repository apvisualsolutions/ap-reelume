using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// The I3 gate condition: changing media, path, and output device repeatedly must not grow resources,
/// and there must never be two engines or two sessions alive at once.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class PlaybackGateEnduranceTests
{
    private const int Cycles = 50;

    [Fact]
    public async Task Fifty_cycles_of_media_path_and_device_changes_leave_resources_flat_and_one_engine()
    {
        var samples = new List<string>();
        foreach (var id in new[] { "mp4-h264-aac", "mkv-hevc-eac3", "webm-vp9-opus", "mkv-audio-51" })
        {
            samples.Add(await CodecMatrixTests.RequireSampleAsync(MediaManifest.Require(id)));
        }

        var devices = new[]
        {
            new AudioOutputDevice("endpoint-a", "Salida A", [AudioChannelLayout.Stereo], true, true),
            new AudioOutputDevice("endpoint-b", "Salida B", [AudioChannelLayout.Stereo], false, true),
        };
        var catalog = new StaticCatalog(devices);
        var preferences = new InMemoryPreferences();
        using var outputs = new LibVlcAudioOutputAdapter(catalog, preferences);

        await using var factory = LibVlcFactory.CreateHeadless();
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var baselineHandles = process.HandleCount;
        var baselineWorkingSet = process.WorkingSet64;
        var rows = new List<string> { "cycle,handles,workingSetBytes,nativeInstances,liveMedia,livePlayers" };

        await using (var engine = new LibVlcMediaPlayerEngine(factory))
        {
            await engine.InitializeAsync(TestContext.Current.CancellationToken);
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                var path = samples[cycle % samples.Count];
                await engine.OpenAsync(
                    new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
                    TestContext.Current.CancellationToken);
                await engine.PlayAsync(TestContext.Current.CancellationToken);
                _ = await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(80));

                var selection = await outputs.SelectAsync(
                    new AudioOutputRequest(
                        devices[cycle % devices.Length].Id,
                        AudioChannelLayout.Stereo,
                        PreferenceScope.Global,
                        PlaybackPreference.GlobalKey),
                    TestContext.Current.CancellationToken);
                Assert.NotNull(selection);

                await engine.StopAsync(TestContext.Current.CancellationToken);

                // Exactly one native instance and no leaked media or player, on every cycle.
                Assert.Equal(1, LibVlcFactory.NativeInstanceCount);
                Assert.Equal(0, engine.LiveMediaCount);
                Assert.Equal(1, factory.LiveMediaPlayerCount);

                process.Refresh();
                rows.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{cycle + 1},{process.HandleCount},{process.WorkingSet64},{LibVlcFactory.NativeInstanceCount},{engine.LiveMediaCount},{factory.LiveMediaPlayerCount}"));
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        process.Refresh();
        rows.Insert(1, string.Create(
            CultureInfo.InvariantCulture,
            $"0,{baselineHandles},{baselineWorkingSet},1,0,1"));
        rows.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"final,{process.HandleCount},{process.WorkingSet64},{LibVlcFactory.NativeInstanceCount},0,{factory.LiveMediaPlayerCount}"));

        var report = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "C4",
            "endurance-resources.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(report, rows, TestContext.Current.CancellationToken);

        Assert.Equal(0, factory.LiveMediaPlayerCount);
        Assert.Equal(1, LibVlcFactory.NativeInstanceCount);

        // The gate condition is "no sustained growth", not "no growth at all": opening four different
        // formats loads four sets of decoders, which is a one-time cost paid in the first cycles.
        // What must stay flat is the trend once that cost is paid, so two settled windows are
        // compared instead of the first sample against the last.
        //
        // The bound is deliberately loose, and the reason was measured rather than assumed. Working
        // set is read from the whole process, which also runs the test host and the coverage
        // collector, and how much of it .NET returns to the operating system depends on what else the
        // machine is doing. Seven runs of this loop with no code change produced between -7.9 MB and
        // +37.6 MB, so a 32 MB bound sat inside the spread and the suite failed roughly one run in
        // three. Fitting a slope instead was tried and measured: it ranged from -170 to +1107 KB per
        // cycle across the same runs, so it is no steadier — the variation is a shift in the whole
        // figure, not a spike a trend line would absorb.
        //
        // What actually catches a leak is asserted on every one of the fifty cycles above, exactly
        // and deterministically: one native instance, no live media, one live player. This bound is
        // here for a gross regression those counters could miss — something on the order of megabytes
        // per cycle — and calling it anything more precise would be claiming a measurement nobody
        // took.
        var early = Window(rows, 11, 25);
        var late = Window(rows, 36, 50);
        Assert.InRange(late.WorkingSet - early.WorkingSet, long.MinValue, 128L * 1024 * 1024);

        // Handles are measured on the whole process, which also runs the test host and the coverage
        // collector, so this bound catches a gross regression rather than pinning a precise figure.
        // The two handles per cycle measured here belong to the hardware decoder: with software
        // decoding the same loop gains none, which HandleGrowthTests pins. The C4 evidence records
        // the attribution.
        const int cyclesBetweenWindows = 25;
        const int allowedHandlesPerCycle = 8;
        Assert.InRange(
            late.Handles - early.Handles,
            int.MinValue,
            cyclesBetweenWindows * allowedHandlesPerCycle);
    }

    private static (long WorkingSet, int Handles) Window(IReadOnlyList<string> rows, int first, int last)
    {
        var samples = rows
            .Skip(1)
            .Select(row => row.Split(','))
            .Where(fields => int.TryParse(fields[0], CultureInfo.InvariantCulture, out var cycle)
                && cycle >= first
                && cycle <= last)
            .ToArray();
        return (
            (long)samples.Average(fields => long.Parse(fields[2], CultureInfo.InvariantCulture)),
            (int)samples.Average(fields => int.Parse(fields[1], CultureInfo.InvariantCulture)));
    }

    private sealed class StaticCatalog(IReadOnlyList<AudioOutputDevice> devices) : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(devices);
    }

    private sealed class InMemoryPreferences : IPlaybackPreferenceRepository
    {
        private readonly Dictionary<(PreferenceScope, string), PlaybackPreference> _stored = [];

        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_stored.TryGetValue((scope, scopeKey), out var value) ? value : null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(preference);
            _stored[(preference.Scope, preference.ScopeKey)] = preference;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            _ = _stored.Remove((scope, scopeKey));
            return Task.CompletedTask;
        }
    }
}
