using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// The lifecycle of an output choice: it persists across restarts, survives the device disappearing,
/// and a hot switch pauses and resumes rather than tearing the session down.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AudioDeviceLifecycleTests
{
    private static readonly AudioOutputDevice Speakers = new(
        "endpoint-speakers",
        "Altavoces",
        [AudioChannelLayout.Stereo, AudioChannelLayout.Surround51],
        IsDefault: true,
        IsAvailable: true);

    private static readonly AudioOutputDevice Headset = new(
        "endpoint-headset",
        "Auriculares",
        [AudioChannelLayout.Stereo],
        IsDefault: false,
        IsAvailable: true);

    [Fact]
    public async Task The_chosen_device_survives_a_restart_of_the_application()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new PlaybackPreferenceRepository(factory);
        var catalog = new FakeCatalog([Speakers, Headset]);
        var adapter = new LibVlcAudioOutputAdapter(catalog, repository);

        var chosen = await adapter.SelectAsync(
            new AudioOutputRequest(Headset.Id, AudioChannelLayout.Stereo, PreferenceScope.Global, PlaybackPreference.GlobalKey),
            TestContext.Current.CancellationToken);
        Assert.Equal(Headset.Id, chosen!.Device.Id);

        var afterRestart = new LibVlcAudioOutputAdapter(catalog, new PlaybackPreferenceRepository(factory));
        var restored = await afterRestart.ResolveStoredAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            AudioChannelLayout.Stereo,
            TestContext.Current.CancellationToken);

        Assert.Equal(Headset.Id, restored!.Device.Id);
    }

    [Fact]
    public async Task Losing_the_device_during_playback_falls_back_without_failing()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new PlaybackPreferenceRepository(factory);
        var catalog = new FakeCatalog([Speakers, Headset]);
        var adapter = new LibVlcAudioOutputAdapter(catalog, repository);
        _ = await adapter.SelectAsync(
            new AudioOutputRequest(Headset.Id, AudioChannelLayout.Stereo, PreferenceScope.Global, PlaybackPreference.GlobalKey),
            TestContext.Current.CancellationToken);

        catalog.Replace([Speakers, Headset with { IsAvailable = false }]);
        var afterUnplug = await adapter.ResolveStoredAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            AudioChannelLayout.Stereo,
            TestContext.Current.CancellationToken);

        Assert.Equal(Speakers.Id, afterUnplug!.Device.Id);
        Assert.True(afterUnplug.FellBackToDefaultDevice);

        // The stored preference is not rewritten: plugging the headset back in restores it.
        catalog.Replace([Speakers, Headset]);
        var afterReplug = await adapter.ResolveStoredAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            AudioChannelLayout.Stereo,
            TestContext.Current.CancellationToken);
        Assert.Equal(Headset.Id, afterReplug!.Device.Id);
    }

    [Fact]
    public async Task A_hot_switch_pauses_and_resumes_the_session_exactly_once()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var catalog = new FakeCatalog([Speakers, Headset]);
        var adapter = new LibVlcAudioOutputAdapter(catalog, new PlaybackPreferenceRepository(factory));
        var engine = new RecordingEngine();

        _ = await adapter.SelectAsync(
            new AudioOutputRequest(Headset.Id, AudioChannelLayout.Stereo, PreferenceScope.Global, PlaybackPreference.GlobalKey),
            engine,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, engine.PauseCount);
        Assert.Equal(1, engine.PlayCount);
        Assert.Equal(0, engine.StopCount);
        Assert.Equal(["pause", "device", "play"], engine.Order);
    }

    [Fact]
    public async Task A_degraded_layout_is_reported_so_the_interface_can_say_so()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var adapter = new LibVlcAudioOutputAdapter(
            new FakeCatalog([Headset with { IsDefault = true }]),
            new PlaybackPreferenceRepository(factory));

        var selection = await adapter.SelectAsync(
            new AudioOutputRequest(Headset.Id, AudioChannelLayout.Surround71, PreferenceScope.Global, PlaybackPreference.GlobalKey),
            TestContext.Current.CancellationToken);

        Assert.True(selection!.LayoutWasDegraded);
        Assert.Equal(AudioChannelLayout.Surround71, selection.DegradedFrom);
        Assert.Equal(AudioChannelLayout.Stereo, selection.Layout);
    }

    [Fact]
    public async Task With_no_output_at_all_the_adapter_reports_nothing_rather_than_crashing()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var adapter = new LibVlcAudioOutputAdapter(
            new FakeCatalog([]),
            new PlaybackPreferenceRepository(factory));

        var selection = await adapter.ResolveStoredAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            AudioChannelLayout.Stereo,
            TestContext.Current.CancellationToken);

        Assert.Null(selection);
    }

    private sealed class FakeCatalog(IReadOnlyList<AudioOutputDevice> devices) : IAudioDeviceCatalog
    {
        private IReadOnlyList<AudioOutputDevice> _devices = devices;

        public void Replace(IReadOnlyList<AudioOutputDevice> devices) => _devices = devices;

        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(_devices);
    }

    private sealed class RecordingEngine : IAudioOutputTarget
    {
        public List<string> Order { get; } = [];

        public int PauseCount { get; private set; }

        public int PlayCount { get; private set; }

        public int StopCount { get; private set; }

        public bool IsPlaying => true;

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            PauseCount++;
            Order.Add("pause");
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            PlayCount++;
            Order.Add("play");
            return Task.CompletedTask;
        }

        public Task SetOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            Order.Add("device");
            return Task.CompletedTask;
        }
    }
}
