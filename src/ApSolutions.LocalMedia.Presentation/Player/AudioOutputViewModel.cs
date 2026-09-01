// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>One selectable output, described by its name and the layouts it can actually carry.</summary>
public sealed record AudioOutputOption(AudioOutputDevice Device, string Display);

/// <summary>
/// Lets the person choose the output and the channel layout. A layout no endpoint accepts is shown as
/// unavailable rather than offered and silently reduced, and a stored device that is gone falls back
/// to the default without forgetting the choice.
/// </summary>
public sealed class AudioOutputViewModel : INotifyPropertyChanged
{
    private readonly IAudioDeviceCatalog _catalog;
    private readonly IAudioEndpointConfigurator? _endpoints;
    private AudioOutputOption? _selectedDevice;
    private AudioChannelLayout _selectedLayout = AudioChannelLayout.Stereo;
    private AudioOutputSelection? _effective;
    private IReadOnlyList<AudioChannelLayout> _offered = AudioOutputPolicy.SelectableLayouts;
    private AudioEndpointChange _lastChange = AudioEndpointChange.AlreadySet;

    /// <summary>
    /// The configurator is optional, and its absence is the difference between a control that can
    /// change the sound and one that can only report it. A surface built without it says so rather
    /// than offering a choice it cannot honour.
    /// </summary>
    public AudioOutputViewModel(IAudioDeviceCatalog catalog, IAudioEndpointConfigurator? endpoints = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _endpoints = endpoints;
        ChooseLayoutCommand = new ChooseLayoutCommandImplementation(layout => SelectedLayout = layout);
    }

    /// <summary>
    /// Chooses one of the three layouts, by the word the markup carries.
    /// </summary>
    /// <remarks>
    /// A word and not the enumeration value, because <c>{x:True}</c> and friends are not measured in
    /// this Avalonia and a parameter that arrived as the string "Surround71" would leave every button
    /// unpressed while looking exactly right — the same trap the course dialog's two pills were
    /// rescued from on 2026-08-31.
    /// </remarks>
    public ICommand ChooseLayoutCommand { get; }

    /// <summary>The three of them, each answering whether it is the one in force.</summary>
    public bool IsStereoChosen => _selectedLayout == AudioChannelLayout.Stereo;

    /// <summary>The three of them, each answering whether it is the one in force.</summary>
    public bool IsSurround51Chosen => _selectedLayout == AudioChannelLayout.Surround51;

    /// <summary>The three of them, each answering whether it is the one in force.</summary>
    public bool IsSurround71Chosen => _selectedLayout == AudioChannelLayout.Surround71;

    /// <summary>And whether the chosen endpoint's driver will take it.</summary>
    public bool IsStereoAvailable => IsLayoutAvailable(AudioChannelLayout.Stereo);

    /// <summary>And whether the chosen endpoint's driver will take it.</summary>
    public bool IsSurround51Available => IsLayoutAvailable(AudioChannelLayout.Surround51);

    /// <summary>And whether the chosen endpoint's driver will take it.</summary>
    public bool IsSurround71Available => IsLayoutAvailable(AudioChannelLayout.Surround71);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Applies a person's choice to the running session and stores it (AUD-A01). Optional the way
    /// the gesture handler is: a surface built without it shows the machine's outputs and applies
    /// nothing, which is what a session-less context wants. The handler's answer is authoritative,
    /// because only it knows whether the device fell back or the layout was reduced.
    /// </summary>
    public Func<string, AudioChannelLayout, Task<AudioOutputSelection?>>? SelectionHandler { get; set; }

    /// <summary>What the session reported about the last layout write, for the surface to describe.</summary>
    public Func<AudioEndpointChange>? LayoutChangeReporter { get; set; }

    public ObservableCollection<AudioOutputOption> Devices { get; } = [];

    /// <summary>Every layout the interface draws, whether or not this endpoint takes it.</summary>
    /// <remarks>
    /// All three, always, because the prototype draws all three and dims the ones the device will not
    /// take — a row that loses a button when a headset is plugged in is a row that moves under the
    /// pointer. <see cref="IsLayoutAvailable"/> is what decides which of them can be pressed.
    /// </remarks>
    public static IReadOnlyList<AudioChannelLayout> Layouts => AudioOutputPolicy.SelectableLayouts;

    public AudioOutputOption? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetField(ref _selectedDevice, value))
            {
                Recalculate();
                _ = ApplySelectionAsync();
            }
        }
    }

    public AudioChannelLayout SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (SetField(ref _selectedLayout, value))
            {
                Recalculate();
                _ = ApplySelectionAsync();
            }
        }
    }

    /// <summary>
    /// Hands the person's choice to the session. What comes back is what actually happened, so the
    /// flags on screen describe the machine's answer rather than the click's intention.
    /// </summary>
    private async Task ApplySelectionAsync()
    {
        if (SelectionHandler is not { } handler || _selectedDevice is not { } option)
        {
            return;
        }

        try
        {
            var applied = await handler(option.Device.Id, _selectedLayout).ConfigureAwait(true);
            _lastChange = LayoutChangeReporter?.Invoke() ?? AudioEndpointChange.Unavailable;
            if (applied is not null)
            {
                _effective = applied;
            }

            await RefreshOfferedLayoutsAsync().ConfigureAwait(true);
            NotifyEffective();
        }
        catch (PlaybackFailureException)
        {
            // The session went away underneath the choice; the stored preference is untouched and
            // the next session will apply it.
        }
    }

    /// <summary>True when the chosen layout had to be reduced for the chosen endpoint.</summary>
    public bool LayoutWasDegraded => _effective?.LayoutWasDegraded == true;

    /// <summary>True when the stored device is gone and the default answered instead.</summary>
    public bool FellBackToDefaultDevice => _effective?.FellBackToDefaultDevice == true;

    /// <summary>True when the machine offers no output at all.</summary>
    public bool HasNoOutput => Devices.Count == 0;

    /// <summary>Bitstream passthrough is never offered; the interface states it.</summary>
    public static bool SupportsBitstreamPassthrough => AudioOutputPolicy.SupportsBitstreamPassthrough;

    public AudioChannelLayout EffectiveLayout => _effective?.Layout ?? AudioChannelLayout.Stereo;

    /// <summary>Rebuilds the list from what the machine offers right now.</summary>
    public async Task LoadAsync(string? storedDeviceId = null, CancellationToken cancellationToken = default)
    {
        var devices = await _catalog.GetOutputsAsync(cancellationToken).ConfigureAwait(true);
        Devices.Clear();
        foreach (var device in devices.Where(device => device.IsAvailable))
        {
            Devices.Add(new AudioOutputOption(device, Describe(device)));
        }

        var resolved = AudioOutputPolicy.Resolve(devices, storedDeviceId, _selectedLayout);
        _selectedDevice = resolved is null
            ? null
            : Devices.FirstOrDefault(option => option.Device.Id == resolved.Device.Id);
        _effective = resolved;
        OnPropertyChanged(nameof(SelectedDevice));
        OnPropertyChanged(nameof(HasNoOutput));
        await RefreshOfferedLayoutsAsync().ConfigureAwait(true);
        NotifyEffective();
    }

    /// <summary>Asks the chosen endpoint's driver what it takes, once per chosen endpoint.</summary>
    /// <remarks>
    /// Only for the one that is chosen: the query activates an audio client, and a machine with a
    /// dozen endpoints would pay for eleven answers nobody is going to read.
    /// </remarks>
    private async Task RefreshOfferedLayoutsAsync()
    {
        if (_selectedDevice is not { } option)
        {
            // No endpoint, nothing on offer. A fallback to the full scale here would light all three
            // choices on a machine with no sound at all, which is the shape of claim this whole
            // change exists to remove.
            _offered = [];
            return;
        }

        if (_endpoints is not { IsAvailable: true })
        {
            // Nothing can write the layout, so what is on offer is what the endpoint already carries.
            _offered = option.Device.SupportedLayouts;
            return;
        }

        _offered = await _endpoints.GetSupportedLayoutsAsync(option.Device.Id).ConfigureAwait(true);
    }

    /// <summary>True when the chosen endpoint's driver will take the layout.</summary>
    /// <remarks>
    /// The driver's answer and not the catalogue's. The catalogue reads what an endpoint is currently
    /// <b>set to</b>, so asking it here would dim every layout above the current one and a person who
    /// reduced to stereo once could never raise it again.
    /// </remarks>
    public bool IsLayoutAvailable(AudioChannelLayout layout) => _offered.Contains(layout);

    /// <summary>True where this machine can change the layout at all.</summary>
    /// <remarks>
    /// When it is false the three choices are a readout rather than a control, and the surface says
    /// which of the two it is: an interface that offers a choice it cannot honour is the defect this
    /// whole change exists to remove.
    /// </remarks>
    public bool CanChangeLayout => _endpoints is { IsAvailable: true };

    /// <summary>True while the choice is one that would change a Windows setting.</summary>
    public bool LayoutChangeIsSystemWide => CanChangeLayout;

    /// <summary>True when the last choice reached Windows.</summary>
    public bool LayoutWasApplied => _lastChange == AudioEndpointChange.Applied;

    /// <summary>True when the chosen endpoint's driver refused the last choice.</summary>
    public bool LayoutWasRefused => _lastChange == AudioEndpointChange.RefusedByDevice;

    private static string Describe(AudioOutputDevice device)
    {
        var largest = device.SupportedLayouts.Count == 0
            ? AudioChannelLayout.Stereo
            : device.SupportedLayouts.MaxBy(layout => (int)layout);
        var suffix = largest switch
        {
            AudioChannelLayout.Surround71 => " · 7.1",
            AudioChannelLayout.Surround51 => " · 5.1",
            _ => " · 2.0",
        };

        return device.Name + suffix;
    }

    private void Recalculate()
    {
        if (_selectedDevice is not { } option)
        {
            _effective = null;
            NotifyEffective();
            return;
        }

        _effective = new AudioOutputSelection(
            option.Device,
            AudioOutputPolicy.ResolveLayout(option.Device, _selectedLayout),
            FellBackToDefaultDevice: false,
            DegradedFrom: AudioOutputPolicy.ResolveLayout(option.Device, _selectedLayout) == _selectedLayout
                ? null
                : _selectedLayout);
        NotifyEffective();
    }

    private void NotifyEffective()
    {
        OnPropertyChanged(nameof(EffectiveLayout));
        OnPropertyChanged(nameof(LayoutWasDegraded));
        OnPropertyChanged(nameof(FellBackToDefaultDevice));
        OnPropertyChanged(nameof(CanChangeLayout));
        OnPropertyChanged(nameof(LayoutChangeIsSystemWide));
        OnPropertyChanged(nameof(LayoutWasApplied));
        OnPropertyChanged(nameof(LayoutWasRefused));
        OnPropertyChanged(nameof(IsStereoChosen));
        OnPropertyChanged(nameof(IsSurround51Chosen));
        OnPropertyChanged(nameof(IsSurround71Chosen));
        OnPropertyChanged(nameof(IsStereoAvailable));
        OnPropertyChanged(nameof(IsSurround51Available));
        OnPropertyChanged(nameof(IsSurround71Available));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class ChooseLayoutCommandImplementation(Action<AudioChannelLayout> apply) : ICommand
    {
        private static readonly Dictionary<string, AudioChannelLayout> Words =
            new(StringComparer.Ordinal)
            {
                ["stereo"] = AudioChannelLayout.Stereo,
                ["surround51"] = AudioChannelLayout.Surround51,
                ["surround71"] = AudioChannelLayout.Surround71,
            };

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) =>
            parameter is string word && Words.ContainsKey(word);

        public void Execute(object? parameter)
        {
            if (parameter is string word && Words.TryGetValue(word, out var layout))
            {
                apply(layout);
            }
        }
    }
}
