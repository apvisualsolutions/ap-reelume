// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The output picker lists what the machine actually offers, says when a layout had to be reduced,
/// and never presents bitstream passthrough as an option.
/// </summary>
public sealed class AudioOutputViewTests
{
    private static readonly AudioOutputDevice Receiver = new(
        "endpoint-receiver",
        "Receptor HDMI",
        [AudioChannelLayout.Stereo, AudioChannelLayout.Surround51, AudioChannelLayout.Surround71],
        IsDefault: true,
        IsAvailable: true);

    private static readonly AudioOutputDevice Headset = new(
        "endpoint-headset",
        "Auriculares",
        [AudioChannelLayout.Stereo],
        IsDefault: false,
        IsAvailable: true);

    [AvaloniaFact]
    public async Task The_list_shows_each_endpoint_with_the_largest_layout_it_can_carry()
    {
        var viewModel = new AudioOutputViewModel(new FakeCatalog([Receiver, Headset]));

        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, viewModel.Devices.Count);
        Assert.Contains(viewModel.Devices, option => option.Display.EndsWith("7.1", StringComparison.Ordinal));
        Assert.Contains(viewModel.Devices, option => option.Display.EndsWith("2.0", StringComparison.Ordinal));
        Assert.False(viewModel.HasNoOutput);
        Assert.Equal(Receiver.Id, viewModel.SelectedDevice!.Device.Id);
        Assert.True(viewModel.IsLayoutAvailable(AudioChannelLayout.Surround71));
    }

    [AvaloniaFact]
    public async Task Choosing_a_layout_the_endpoint_cannot_carry_is_announced_as_reduced()
    {
        var viewModel = new AudioOutputViewModel(new FakeCatalog([Receiver, Headset]));
        await viewModel.LoadAsync(Headset.Id, TestContext.Current.CancellationToken);

        viewModel.SelectedLayout = AudioChannelLayout.Surround71;

        Assert.Equal(AudioChannelLayout.Stereo, viewModel.EffectiveLayout);
        Assert.True(viewModel.LayoutWasDegraded);

        viewModel.SelectedDevice = viewModel.Devices.Single(option => option.Device.Id == Receiver.Id);
        Assert.Equal(AudioChannelLayout.Surround71, viewModel.EffectiveLayout);
        Assert.False(viewModel.LayoutWasDegraded);
    }

    [AvaloniaFact]
    public async Task A_machine_with_no_output_says_so_instead_of_offering_nothing_silently()
    {
        var viewModel = new AudioOutputViewModel(new FakeCatalog([]));

        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasNoOutput);
        Assert.Null(viewModel.SelectedDevice);
        Assert.False(viewModel.IsLayoutAvailable(AudioChannelLayout.Stereo));
    }

    [AvaloniaFact]
    public async Task A_stored_device_that_is_gone_falls_back_and_the_view_says_it()
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Receiver, Headset with { IsAvailable = false }]));

        await viewModel.LoadAsync(Headset.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Receiver.Id, viewModel.SelectedDevice!.Device.Id);
        Assert.True(viewModel.FellBackToDefaultDevice);
    }

    [AvaloniaFact]
    public async Task Both_selectors_are_named_and_no_option_offers_passthrough()
    {
        Assert.NotNull(Avalonia.Application.Current);
        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var viewModel = new AudioOutputViewModel(new FakeCatalog([Receiver, Headset]));
            await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
            var view = new AudioOutputView { DataContext = viewModel };
            var window = new Window { Width = 480, Height = 320, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var selectors = view.GetVisualDescendants()
                .OfType<ComboBox>()
                .Where(box => box.Name is "AudioDeviceSelector" or "AudioLayoutSelector")
                .ToArray();
            Assert.Equal(2, selectors.Length);
            Assert.All(selectors, box => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(box))));
            Assert.All(selectors, box => Assert.True(box.Focusable));

            Assert.False(AudioOutputViewModel.SupportsBitstreamPassthrough);
            Assert.All(
                AudioOutputViewModel.Layouts,
                layout => Assert.DoesNotContain(
                    "passthrough",
                    layout.ToString(),
                    StringComparison.OrdinalIgnoreCase));
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_view_model_refuses_to_exist_without_a_catalog() =>
        Assert.Throws<ArgumentNullException>(() => new AudioOutputViewModel(null!));

    /// <summary>
    /// What can be chosen comes from the driver, not from what the endpoint is set to.
    /// </summary>
    /// <remarks>
    /// The one-way door this exists to prevent: the catalogue reports the layout an endpoint
    /// <b>carries</b>, so a headset reduced to stereo would offer stereo alone and could never be
    /// raised again. Here the catalogue says stereo and the driver says all three, and it is the
    /// driver that decides.
    /// </remarks>
    [AvaloniaFact]
    public async Task What_can_be_chosen_is_what_the_driver_takes_and_not_what_the_endpoint_carries()
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Headset]),
            new FakeConfigurator([
                AudioChannelLayout.Stereo,
                AudioChannelLayout.Surround51,
                AudioChannelLayout.Surround71,
            ]));

        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(Headset.SupportedLayouts);
        Assert.True(viewModel.IsLayoutAvailable(AudioChannelLayout.Surround71));
        Assert.True(viewModel.CanChangeLayout);
    }

    /// <summary>
    /// Where nothing can write the layout, the interface says so instead of offering a choice.
    /// </summary>
    [AvaloniaFact]
    public async Task A_machine_that_cannot_write_the_layout_offers_what_the_endpoint_already_carries()
    {
        var viewModel = new AudioOutputViewModel(new FakeCatalog([Headset]));

        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(viewModel.CanChangeLayout);
        Assert.False(viewModel.LayoutChangeIsSystemWide);
        Assert.True(viewModel.IsLayoutAvailable(AudioChannelLayout.Stereo));
        Assert.False(viewModel.IsLayoutAvailable(AudioChannelLayout.Surround71));
    }

    /// <summary>
    /// What the surface says about a choice is what the write reported, not what was clicked.
    /// </summary>
    /// <remarks>
    /// The two sentences are different and so are their causes: a device can route perfectly while
    /// its driver refuses the layout. Asserting on the click would make both of them say the same
    /// thing, which is the shape of claim that got this control rewritten.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(AudioEndpointChange.Applied, true, false)]
    [InlineData(AudioEndpointChange.RefusedByDevice, false, true)]
    [InlineData(AudioEndpointChange.Unavailable, false, false)]
    public async Task The_surface_reports_what_the_write_did_rather_than_what_was_clicked(
        AudioEndpointChange reported,
        bool applied,
        bool refused)
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Receiver]),
            new FakeConfigurator([AudioChannelLayout.Stereo, AudioChannelLayout.Surround71]))
        {
            SelectionHandler = (_, _) => Task.FromResult<AudioOutputSelection?>(null),
            LayoutChangeReporter = () => reported,
        };
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        viewModel.SelectedLayout = AudioChannelLayout.Surround71;
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(applied, viewModel.LayoutWasApplied);
        Assert.Equal(refused, viewModel.LayoutWasRefused);
    }

    private sealed class FakeCatalog(IReadOnlyList<AudioOutputDevice> devices) : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(devices);
    }

    private sealed class FakeConfigurator(IReadOnlyList<AudioChannelLayout> supported)
        : IAudioEndpointConfigurator
    {
        public bool IsAvailable => true;

        public Task<IReadOnlyList<AudioChannelLayout>> GetSupportedLayoutsAsync(
            string deviceId,
            CancellationToken cancellationToken = default) => Task.FromResult(supported);

        public Task<AudioEndpointChange> SetLayoutAsync(
            string deviceId,
            AudioChannelLayout layout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                supported.Contains(layout)
                    ? AudioEndpointChange.Applied
                    : AudioEndpointChange.RefusedByDevice);
    }
}
