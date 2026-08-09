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

    private sealed class FakeCatalog(IReadOnlyList<AudioOutputDevice> devices) : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(devices);
    }
}
