using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The transport bar: play, pause, skips, speed, volume, and the boost warning. Every command is a
/// use case call, so the view never touches the engine and every value arrives already clamped.
/// </summary>
public sealed class TransportControlsViewModel : INotifyPropertyChanged
{
    private readonly ControlPlayback _control;
    private PlaybackControlState _state;

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
        SkipBackwardCommand = new RelayCommand(() => _control.SkipBackwardAsync(), Apply);
        SkipForwardCommand = new RelayCommand(() => _control.SkipForwardAsync(), Apply);
        ToggleMuteCommand = new RelayCommand(() => _control.ToggleMuteAsync(), Apply);
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand(
        Func<Task<PlaybackControlState>> execute,
        Action<PlaybackControlState> onCompleted) : ICommand
    {
        private bool _isRunning;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isRunning;

        public async void Execute(object? parameter)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                onCompleted(await execute().ConfigureAwait(true));
            }
            finally
            {
                _isRunning = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
