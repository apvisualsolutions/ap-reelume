// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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

        // The name and what the endpoint can carry are two strings now, because the prototype draws
        // them in two weights. The pair joined is what the foot of the transport still says.
        Assert.Contains(viewModel.Devices, option => option is { Display: "Receptor HDMI", Capabilities: "7.1" });
        Assert.Contains(viewModel.Devices, option => option is { Display: "Auriculares", Capabilities: "2.0" });
        Assert.Contains(viewModel.Devices, option => option.Summary == "Receptor HDMI · 7.1");
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

    /// <summary>Choosing the device already chosen changes nothing and applies nothing again.</summary>
    /// <remarks>
    /// The four tests from here down cover the five branches the coverage gate measured untaken on
    /// 2026-09-11, each with both of its halves in the same test: when reports are merged each line
    /// keeps the best of them, not their union, so a half taken here and the other half taken only by
    /// the walk would read as half for ever.
    /// </remarks>
    [AvaloniaFact]
    public async Task Choosing_the_device_already_chosen_changes_nothing()
    {
        var viewModel = new AudioOutputViewModel(new FakeCatalog([Receiver, Headset]));
        await viewModel.LoadAsync(Headset.Id, TestContext.Current.CancellationToken);
        var receiver = viewModel.Devices.Single(option => option.Device.Id == Receiver.Id);

        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);
        viewModel.SelectedDevice = receiver;
        Assert.Contains(nameof(AudioOutputViewModel.SelectedDevice), announced);

        announced.Clear();
        viewModel.SelectedDevice = receiver;
        Assert.Empty(announced);
        Assert.Same(receiver, viewModel.SelectedDevice);
    }

    /// <summary>With nothing applied the layout in effect is stereo, and with a device it is its own.</summary>
    [AvaloniaFact]
    public async Task With_nothing_applied_the_layout_in_effect_is_stereo()
    {
        var silent = new AudioOutputViewModel(new FakeCatalog([]));
        await silent.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(AudioChannelLayout.Stereo, silent.EffectiveLayout);

        var receiver = new AudioOutputViewModel(new FakeCatalog([Receiver]));
        await receiver.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        receiver.SelectedLayout = AudioChannelLayout.Surround71;
        Assert.Equal(AudioChannelLayout.Surround71, receiver.EffectiveLayout);
    }

    /// <summary>An endpoint that declares no layout at all is offered as stereo, not as nothing.</summary>
    [AvaloniaFact]
    public async Task An_endpoint_that_declares_no_layout_is_offered_as_stereo()
    {
        var bare = new AudioOutputDevice("endpoint-bare", "Altavoz", [], IsDefault: false, IsAvailable: true);
        var viewModel = new AudioOutputViewModel(new FakeCatalog([Receiver, bare]));
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("2.0", viewModel.Devices.Single(option => option.Device.Id == bare.Id).Capabilities);
        Assert.Equal("7.1", viewModel.Devices.Single(option => option.Device.Id == Receiver.Id).Capabilities);
    }

    /// <summary>The two commands act on what is theirs and leave anything else where it was.</summary>
    [AvaloniaFact]
    public async Task The_two_commands_refuse_what_is_not_theirs()
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Receiver, Headset]),
            new FakeConfigurator([AudioChannelLayout.Stereo, AudioChannelLayout.Surround51]));
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        viewModel.ChooseLayoutCommand.Execute("surround51");
        Assert.Equal(AudioChannelLayout.Surround51, viewModel.SelectedLayout);
        viewModel.ChooseLayoutCommand.Execute("quadraphonic");
        viewModel.ChooseLayoutCommand.Execute(null);
        Assert.Equal(AudioChannelLayout.Surround51, viewModel.SelectedLayout);

        var headset = viewModel.Devices.Single(option => option.Device.Id == Headset.Id);
        viewModel.ChooseDeviceCommand.Execute(headset);
        Assert.Same(headset, viewModel.SelectedDevice);
        viewModel.ChooseDeviceCommand.Execute(Receiver.Id);
        Assert.Same(headset, viewModel.SelectedDevice);
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

    /// <summary>
    /// The device is a list of radios, the layout is three buttons, and every one of them is named.
    /// </summary>
    /// <remarks>
    /// The layout stopped being a drop-down on 2026-09-02, because the prototype draws
    /// <c>chList</c> as a row of three with the chosen one accented. A drop-down also showed the
    /// enumeration's own words — «Surround51» — identically in both languages, which is the
    /// bilingual rule broken by a control nobody had read the contents of.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_device_is_a_list_the_layout_is_three_buttons_and_all_are_named()
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

            // The device stopped being a drop-down on 2026-09-02 and is a row of radios, which is
            // what the prototype draws. So there is one control per endpoint rather than one for
            // the list, and each of them is named and reachable by keyboard.
            var devices = view.GetVisualDescendants()
                .OfType<RadioButton>()
                .Where(radio => radio.Classes.Contains("option"))
                .ToArray();
            Assert.Equal(viewModel.Devices.Count, devices.Length);
            Assert.All(devices, radio => Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(radio))));
            Assert.All(devices, radio => Assert.True(radio.Focusable));

            // Exactly one of them is checked, and it is the one the model says is in force. A list
            // where two rows claim the choice, or none does, is the defect a radio group hides best.
            var chosen = Assert.Single(devices, radio => radio.IsChecked == true);
            Assert.Equal(
                viewModel.SelectedDevice!.Display,
                AutomationProperties.GetHelpText(chosen));

            var choices = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Name is "AudioLayoutStereo"
                    or "AudioLayoutSurround51"
                    or "AudioLayoutSurround71")
                .ToArray();
            Assert.Equal(3, choices.Length);
            Assert.All(choices, button => Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))));
            Assert.All(choices, button => Assert.True(button.Focusable));

            // The one in force wears the class the prototype accents, and it is the only one.
            Assert.Single(choices, button => button.Classes.Contains("selected"));

            // And the layout is never named by the enumeration, which reads the same in both
            // languages and in neither of them is a word anybody says.
            Assert.DoesNotContain(
                choices,
                button => string.Equals(
                    AutomationProperties.GetName(button),
                    nameof(AudioChannelLayout.Surround51),
                    StringComparison.Ordinal));

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

    /// <summary>
    /// The command takes the three words the markup carries, and nothing else.
    /// </summary>
    /// <remarks>
    /// A word rather than the enumeration value, because <c>{x:True}</c> and its kin are not measured
    /// in this Avalonia — so a parameter arriving as anything else has to be refused rather than
    /// guessed at, which is what leaves the three buttons looking right and doing nothing.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_layout_command_takes_the_three_words_and_refuses_anything_else()
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Receiver]),
            new FakeConfigurator([
                AudioChannelLayout.Stereo,
                AudioChannelLayout.Surround51,
                AudioChannelLayout.Surround71,
            ]));
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(viewModel.ChooseLayoutCommand.CanExecute("surround71"));
        Assert.False(viewModel.ChooseLayoutCommand.CanExecute("Surround71"));
        Assert.False(viewModel.ChooseLayoutCommand.CanExecute(AudioChannelLayout.Surround71));
        Assert.False(viewModel.ChooseLayoutCommand.CanExecute(null));

        viewModel.ChooseLayoutCommand.Execute("surround51");
        Assert.Equal(AudioChannelLayout.Surround51, viewModel.SelectedLayout);
        Assert.True(viewModel.IsSurround51Chosen);
        Assert.False(viewModel.IsStereoChosen);
        Assert.False(viewModel.IsSurround71Chosen);

        // A word it does not know changes nothing rather than throwing, which is what a button
        // wired with a typo would do.
        viewModel.ChooseLayoutCommand.Execute("quadraphonic");
        Assert.Equal(AudioChannelLayout.Surround51, viewModel.SelectedLayout);
    }

    /// <summary>Each layout button lights for its own layout, and alone.</summary>
    /// <remarks>
    /// The test above asserts that exactly one of the three is lit, and one lit is also what two
    /// swapped bindings produce: that is the shape a gate audit on 2026-09-11 found in three other
    /// rows of pills, where the wrong pill lit and nothing failed. Here all three layouts can be
    /// written, and each button is pressed through its own command, as a click would.
    /// </remarks>
    [AvaloniaFact]
    public async Task Each_layout_button_lights_for_its_own_layout()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Receiver]),
            new FakeConfigurator([
                AudioChannelLayout.Stereo,
                AudioChannelLayout.Surround51,
                AudioChannelLayout.Surround71,
            ]));
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var view = new AudioOutputView { DataContext = viewModel };
        var window = new Window { Width = 480, Height = 320, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var choices = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Name is "AudioLayoutStereo" or "AudioLayoutSurround51" or "AudioLayoutSurround71")
            .ToArray();
        Assert.Equal(3, choices.Length);
        foreach (var choice in choices)
        {
            Assert.True(choice.IsEnabled, $"{choice.Name} cannot be pressed, so its light proves nothing.");
            choice.Command!.Execute(choice.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal([choice.Name], choices.Where(button => button.Classes.Contains("selected")).Select(button => button.Name));
        }

        window.Close();
    }

    /// <summary>
    /// With no endpoint chosen, nothing is on offer and nothing is claimed about writing it.
    /// </summary>
    [AvaloniaFact]
    public async Task With_no_endpoint_nothing_is_offered_even_where_the_layout_can_be_written()
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([]),
            new FakeConfigurator([AudioChannelLayout.Stereo, AudioChannelLayout.Surround71]));

        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasNoOutput);
        Assert.False(viewModel.IsStereoAvailable);
        Assert.False(viewModel.IsSurround51Available);
        Assert.False(viewModel.IsSurround71Available);

        // The machine can still write layouts; it is the endpoint that is missing, and those are
        // two different sentences for the surface to say.
        Assert.True(viewModel.CanChangeLayout);
    }

    /// <summary>
    /// A surface with no reporter says the write is unavailable rather than claiming it worked.
    /// </summary>
    [AvaloniaFact]
    public async Task A_surface_with_no_reporter_claims_nothing_about_the_write()
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Receiver]),
            new FakeConfigurator([AudioChannelLayout.Stereo, AudioChannelLayout.Surround71]))
        {
            SelectionHandler = (_, _) => Task.FromResult<AudioOutputSelection?>(null),
        };
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        viewModel.SelectedLayout = AudioChannelLayout.Surround71;
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(viewModel.LayoutWasApplied);
        Assert.False(viewModel.LayoutWasRefused);
    }

    /// <summary>
    /// A session that goes away under the choice leaves the surface standing.
    /// </summary>
    [AvaloniaFact]
    public async Task A_session_that_fails_under_the_choice_is_not_an_exception_on_screen()
    {
        var viewModel = new AudioOutputViewModel(
            new FakeCatalog([Receiver]),
            new FakeConfigurator([AudioChannelLayout.Stereo]))
        {
            SelectionHandler = (_, _) => throw new PlaybackFailureException("the session went away"),
        };
        await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        viewModel.SelectedLayout = AudioChannelLayout.Surround71;
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(AudioChannelLayout.Surround71, viewModel.SelectedLayout);
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
