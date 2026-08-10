// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private AudioOutputOption? _selectedDevice;
    private AudioChannelLayout _selectedLayout = AudioChannelLayout.Stereo;
    private AudioOutputSelection? _effective;

    public AudioOutputViewModel(IAudioDeviceCatalog catalog) =>
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Applies a person's choice to the running session and stores it (AUD-A01). Optional the way
    /// the gesture handler is: a surface built without it shows the machine's outputs and applies
    /// nothing, which is what a session-less context wants. The handler's answer is authoritative,
    /// because only it knows whether the device fell back or the layout was reduced.
    /// </summary>
    public Func<string, AudioChannelLayout, Task<AudioOutputSelection?>>? SelectionHandler { get; set; }

    public ObservableCollection<AudioOutputOption> Devices { get; } = [];

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
            if (applied is not null)
            {
                _effective = applied;
                NotifyEffective();
            }
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
        NotifyEffective();
    }

    /// <summary>True when at least one present endpoint can carry the layout.</summary>
    public bool IsLayoutAvailable(AudioChannelLayout layout) =>
        Devices.Any(option => option.Device.SupportedLayouts.Contains(layout));

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
}
