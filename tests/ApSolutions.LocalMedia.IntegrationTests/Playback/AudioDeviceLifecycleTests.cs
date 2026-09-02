// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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

    /// <summary>
    /// The layout is written on the endpoint before the device is routed, and never after.
    /// </summary>
    /// <remarks>
    /// The order is the fix, not a detail. Writing the endpoint's format invalidates every audio
    /// client on it, and LibVLC's recovery from that discards the chosen device for the default one
    /// — so the routing has to come afterwards to put the choice back. Asserted on the order rather
    /// than on the calls, because both orders make both calls and only one of them plays the sound
    /// through the speakers somebody picked.
    /// <para>
    /// Which is why the two doubles share <b>one</b> ledger. Until 2026-09-02 they kept one each and
    /// this asserted that both lists were non-empty, which is true whichever way round the adapter
    /// runs — the gate named the order in its own remarks and then measured something else. Moving
    /// the endpoint block after the routing block left it green; it does not any more.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_layout_is_written_before_the_device_is_routed()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var ledger = new List<string>();
        var endpoints = new RecordingConfigurator(
            [AudioChannelLayout.Stereo, AudioChannelLayout.Surround71],
            ledger);
        var adapter = new LibVlcAudioOutputAdapter(
            new FakeCatalog([Speakers]),
            new PlaybackPreferenceRepository(factory),
            endpoints);
        var engine = new RecordingEngine { Order = ledger };

        _ = await adapter.SelectAsync(
            new AudioOutputRequest(
                Speakers.Id,
                AudioChannelLayout.Surround71,
                PreferenceScope.Global,
                PlaybackPreference.GlobalKey),
            engine,
            TestContext.Current.CancellationToken);

        Assert.Equal([(Speakers.Id, AudioChannelLayout.Surround71)], endpoints.Written);
        Assert.Equal(AudioEndpointChange.Applied, adapter.LastLayoutChange);

        // One list, written by both doubles in the order the adapter called them.
        Assert.Equal(["layout", "pause", "device", "play"], ledger);
    }

    /// <summary>
    /// A driver that refuses the layout is reported rather than routed around.
    /// </summary>
    [Fact]
    public async Task A_layout_the_driver_refuses_is_reported_and_the_device_is_still_routed()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var endpoints = new RecordingConfigurator([AudioChannelLayout.Stereo]);
        var adapter = new LibVlcAudioOutputAdapter(
            new FakeCatalog([Speakers]),
            new PlaybackPreferenceRepository(factory),
            endpoints);
        var engine = new RecordingEngine();

        var selection = await adapter.SelectAsync(
            new AudioOutputRequest(
                Speakers.Id,
                AudioChannelLayout.Surround71,
                PreferenceScope.Global,
                PlaybackPreference.GlobalKey),
            engine,
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.RefusedByDevice, adapter.LastLayoutChange);

        // Refusing a layout is not refusing a device: the output still moves where it was asked to,
        // because those are two choices and only one of them was turned down.
        Assert.NotNull(selection);
        Assert.Contains("device", engine.Order);
    }

    /// <summary>
    /// An adapter built without a configurator routes and writes nothing.
    /// </summary>
    /// <remarks>
    /// Which is every context that is not Windows, and every test that only cares about routing. It
    /// reports Unavailable rather than pretending the layout was honoured — the difference between a
    /// surface that can say «this machine cannot change it» and one that claims it did.
    /// </remarks>
    [Fact]
    public async Task Without_a_configurator_the_layout_is_reported_as_unwritable()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var adapter = new LibVlcAudioOutputAdapter(
            new FakeCatalog([Speakers]),
            new PlaybackPreferenceRepository(factory));

        _ = await adapter.SelectAsync(
            new AudioOutputRequest(
                Speakers.Id,
                AudioChannelLayout.Surround71,
                PreferenceScope.Global,
                PlaybackPreference.GlobalKey),
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioEndpointChange.Unavailable, adapter.LastLayoutChange);
    }

    /// <summary>
    /// A configurator this machine cannot use is the same as not having one.
    /// </summary>
    [Fact]
    public async Task A_configurator_that_is_not_available_writes_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var endpoints = new RecordingConfigurator([AudioChannelLayout.Stereo]) { Available = false };
        var adapter = new LibVlcAudioOutputAdapter(
            new FakeCatalog([Speakers]),
            new PlaybackPreferenceRepository(factory),
            endpoints);

        _ = await adapter.SelectAsync(
            new AudioOutputRequest(
                Speakers.Id,
                AudioChannelLayout.Stereo,
                PreferenceScope.Global,
                PlaybackPreference.GlobalKey),
            TestContext.Current.CancellationToken);

        Assert.Empty(endpoints.Written);
        Assert.Equal(AudioEndpointChange.Unavailable, adapter.LastLayoutChange);
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
        // Settable so a test can hand the same list to the configurator: two ledgers can only say
        // that both things happened, and what has to be asserted here is which came first.
        public List<string> Order { get; init; } = [];

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

    private sealed class RecordingConfigurator(
        IReadOnlyList<AudioChannelLayout> supported,
        List<string>? ledger = null)
        : IAudioEndpointConfigurator
    {
        public List<(string Device, AudioChannelLayout Layout)> Written { get; } = [];

        public bool Available { get; init; } = true;

        public bool IsAvailable => Available;

        public Task<IReadOnlyList<AudioChannelLayout>> GetSupportedLayoutsAsync(
            string deviceId,
            CancellationToken cancellationToken = default) => Task.FromResult(supported);

        public Task<AudioEndpointChange> SetLayoutAsync(
            string deviceId,
            AudioChannelLayout layout,
            CancellationToken cancellationToken = default)
        {
            if (!supported.Contains(layout))
            {
                return Task.FromResult(AudioEndpointChange.RefusedByDevice);
            }

            Written.Add((deviceId, layout));
            ledger?.Add("layout");
            return Task.FromResult(AudioEndpointChange.Applied);
        }
    }
}
