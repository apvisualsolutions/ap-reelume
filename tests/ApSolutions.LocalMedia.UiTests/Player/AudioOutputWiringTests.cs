// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Choosing an audio output reaches the engine (AUD-A01): the surface existed, the adapter that
/// pauses, routes, resumes and stores existed, and nothing joined them — a pick on screen changed
/// nothing about where the sound went.
/// </summary>
public sealed class AudioOutputWiringTests
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
    public async Task Picking_a_device_hands_it_to_the_session_and_shows_the_machines_answer()
    {
        var applied = new List<(string DeviceId, AudioChannelLayout Layout)>();
        var viewModel = new AudioOutputViewModel(new StubCatalog(Speakers, Headset))
        {
            SelectionHandler = (deviceId, layout) =>
            {
                applied.Add((deviceId, layout));
                return Task.FromResult<AudioOutputSelection?>(new AudioOutputSelection(
                    Headset,
                    AudioChannelLayout.Stereo,
                    FellBackToDefaultDevice: false,
                    DegradedFrom: AudioChannelLayout.Surround51));
            },
        };
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        viewModel.SelectedDevice = viewModel.Devices.Single(option => option.Device.Id == Headset.Id);
        await WaitForAsync(() => applied.Count == 1);

        Assert.Equal((Headset.Id, AudioChannelLayout.Stereo), applied.Single());

        // The handler's answer is authoritative: it said the layout was reduced, so the flag on
        // screen says so too — the click's intention is not what gets displayed.
        Assert.True(viewModel.LayoutWasDegraded);
    }

    [Fact]
    public async Task A_session_that_died_under_the_pick_leaves_the_surface_standing()
    {
        var viewModel = new AudioOutputViewModel(new StubCatalog(Speakers, Headset))
        {
            SelectionHandler = (_, _) => throw new PlaybackFailureException(new PlaybackFailure(
                PlaybackFailureCode.EngineUnavailable,
                "No media is currently open.")),
        };
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        viewModel.SelectedDevice = viewModel.Devices.Single(option => option.Device.Id == Headset.Id);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(Headset.Id, viewModel.SelectedDevice?.Device.Id);
    }

    [Fact]
    public async Task Without_a_handler_the_surface_still_lists_and_selects()
    {
        var viewModel = new AudioOutputViewModel(new StubCatalog(Speakers, Headset));
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        viewModel.SelectedDevice = viewModel.Devices.Single(option => option.Device.Id == Headset.Id);

        Assert.Equal(Headset.Id, viewModel.SelectedDevice?.Device.Id);
    }

    [Fact]
    public void The_composition_joins_the_surface_the_adapter_and_the_engine()
    {
        var composition = CompositionSource();

        Assert.Contains("SelectionHandler", composition, StringComparison.Ordinal);
        Assert.Contains("EngineAudioOutputTarget", composition, StringComparison.Ordinal);
        Assert.Contains("ResolveStoredAsync", composition, StringComparison.Ordinal);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static string CompositionSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs");
        Assert.True(File.Exists(path), "CompositionRoot.cs was not found where the host keeps it.");
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private sealed class StubCatalog(params AudioOutputDevice[] devices) : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioOutputDevice>>(devices);
    }
}
