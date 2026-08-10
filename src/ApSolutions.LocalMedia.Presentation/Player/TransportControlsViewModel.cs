// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The transport bar: play, pause, skips, speed, volume, and the boost warning. Every command is a
/// use case call, so the view never touches the engine and every value arrives already clamped.
/// </summary>
public sealed class TransportControlsViewModel : INotifyPropertyChanged
{
    private readonly ControlPlayback _control;
    private PlaybackControlState _state;
    private bool _isRunning;

    public TransportControlsViewModel(ControlPlayback control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _state = new PlaybackControlState(
            TimeSpan.Zero,
            null,
            1.0,
            VolumeBoostPolicy.Decide(VolumeBoostPolicy.MaximumNormalPercent, muted: false),
            PlaybackControlPolicy.DefaultBackwardSkip,
            PlaybackControlPolicy.DefaultForwardSkip);
        // These three keep something the other command surfaces never had: a skip already in flight
        // refuses the next one. Pressing skip twice quickly would otherwise send two seeks the engine
        // has to reconcile, and the second would be measured from a position the first had not
        // reached yet. The guard is here rather than in AsyncRelayCommand because it is this bar's
        // rule, not every button's.
        SkipBackwardCommand = Transport(() => _control.SkipBackwardAsync());
        SkipForwardCommand = Transport(() => _control.SkipForwardAsync());
        ToggleMuteCommand = Transport(() => _control.ToggleMuteAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SkipBackwardCommand { get; }

    public ICommand SkipForwardCommand { get; }

    public ICommand ToggleMuteCommand { get; }

    public static IReadOnlyList<double> SpeedSteps => PlaybackControlPolicy.SpeedSteps;

    public static double MinimumVolumePercent => VolumeBoostPolicy.MinimumPercent;

    public static double MaximumVolumePercent => VolumeBoostPolicy.MaximumBoostPercent;

    public TimeSpan Position => _state.Position;

    public TimeSpan? Duration => _state.Duration;

    public double SpeedMultiplier => _state.SpeedMultiplier;

    public int VolumePercent => _state.Volume.Percent;

    public bool IsMuted => _state.Volume.IsMuted;

    /// <summary>True while the level is above one hundred percent; the view shows text and an icon.</summary>
    public bool IsBoosted => _state.Volume.IsBoosted;

    /// <summary>Always true while boosted: the limiter and the boost are inseparable.</summary>
    public bool LimiterEngaged => _state.Volume.LimiterEngaged;

    public bool RequiresBoostWarning => _state.Volume.RequiresWarning;

    public string BackwardSkipLabel => FormatSeconds(_state.BackwardSkip);

    public string ForwardSkipLabel => FormatSeconds(_state.ForwardSkip);

    public string SpeedLabel => _state.SpeedMultiplier.ToString("0.##×", CultureInfo.CurrentCulture);

    public async Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
        Apply(await _control.SetSpeedAsync(multiplier, cancellationToken).ConfigureAwait(true));

    public async Task SetVolumeAsync(int percent, CancellationToken cancellationToken = default) =>
        Apply(await _control.SetVolumeAsync(percent, cancellationToken).ConfigureAwait(true));

    public async Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        Apply(await _control.SeekAsync(position, cancellationToken).ConfigureAwait(true));

    public async Task ConfigureSkipsAsync(
        TimeSpan backward,
        TimeSpan forward,
        CancellationToken cancellationToken = default) =>
        Apply(await _control.ConfigureSkipsAsync(backward, forward, cancellationToken).ConfigureAwait(true));

    private static string FormatSeconds(TimeSpan interval) =>
        interval.TotalSeconds.ToString("0 s", CultureInfo.CurrentCulture);

    private void Apply(PlaybackControlState state)
    {
        _state = state;
        foreach (var name in new[]
        {
            nameof(Position),
            nameof(Duration),
            nameof(SpeedMultiplier),
            nameof(SpeedLabel),
            nameof(VolumePercent),
            nameof(IsMuted),
            nameof(IsBoosted),
            nameof(LimiterEngaged),
            nameof(RequiresBoostWarning),
            nameof(BackwardSkipLabel),
            nameof(ForwardSkipLabel),
        })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    /// One transport button: it runs, it applies whatever state came back, and while it is in flight
    /// it cannot be pressed again. The command is asked twice about that — once to refuse the press,
    /// and once more when the work ends so the button comes back.
    /// </summary>
    private AsyncRelayCommand Transport(Func<Task<PlaybackControlState>> execute)
    {
        AsyncRelayCommand? command = null;
        command = new AsyncRelayCommand(
            async () =>
            {
                _isRunning = true;
                command!.RaiseCanExecuteChanged();
                try
                {
                    Apply(await execute().ConfigureAwait(true));
                }
                finally
                {
                    _isRunning = false;
                    command!.RaiseCanExecuteChanged();
                }
            },
            () => !_isRunning);
        return command;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
