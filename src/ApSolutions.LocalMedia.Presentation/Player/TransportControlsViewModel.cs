// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Show;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// One step of the speed menu: the multiplier it sets, and the three things the prototype writes on
/// its row.
/// </summary>
/// <remarks>
/// <para>
/// The prototype's row is a mark, a name and a note — «● Normal · 1×», «  2× · más rápida» — and this
/// application drew eleven bare numbers in a <c>MenuFlyout</c>. Three columns rather than one,
/// because the number alone does not say which way it goes: <c>0,75×</c> and <c>1,25×</c> are the
/// same distance from normal and read the same until something says slower or faster.
/// </para>
/// <para>
/// <see cref="Value"/> and <see cref="Label"/> are two properties and not one, and that is the whole
/// difference between the closed pill and the open row: the pill says <c>1×</c> — a multiplier, in
/// the row of a control called SPEED — and the row says <c>Normal</c>, which is the word for it.
/// Every other step spells the two the same.
/// </para>
/// <para>
/// The words are resolved here rather than bound through a converter because a step is built once and
/// the list is rebuilt when the language changes; <see cref="ShowText"/> is the same helper the series
/// card assembles its own strings with, fallback included, so a headless mount with no dictionaries
/// prints something legible rather than a blank.
/// </para>
/// </remarks>
/// <param name="Multiplier">The speed this step sets, and the value the drop-down selects on.</param>
/// <param name="Value">The multiplier as the pill writes it, in the culture in force.</param>
/// <param name="Label">The row's name: the multiplier, or the word for one.</param>
/// <param name="Note">Which way this step goes, or the multiplier when it goes nowhere.</param>
public sealed record SpeedOption(double Multiplier, string Value, string Label, string Note)
{
    /// <summary>The nine the policy allows, in the order the menu lists them.</summary>
    /// <remarks>
    /// <para>
    /// Built from <see cref="PlaybackControlPolicy.SpeedSteps"/> rather than written out again. The
    /// menu used to write its own numbers into the markup and a test read them back out of the file
    /// to compare — which is the shape three suites in this repository have already gone blind in,
    /// because a file that stops matching a regular expression reports nothing rather than failing.
    /// </para>
    /// <para>
    /// A method and not a cached list: the words come out of the dictionary in force, and
    /// <c>App.ApplyLanguage</c> replaces that dictionary wholesale. A static list would hold the
    /// language the first player of the session opened in. It is called once per transport, which is
    /// once per playback session, exactly as the series card resolves its own words once per card.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<SpeedOption> All() => [.. PlaybackControlPolicy.SpeedSteps.Select(Of)];

    /// <summary>The multiplier, which is what the closed pill says.</summary>
    /// <remarks>
    /// The pill's presenter is given this step with no template — <c>ComboBox.speed-pill</c> sets its
    /// <c>ContentTemplate</c> to null on purpose — so this is the closed face, and it is one line
    /// rather than a second <c>DataTemplate</c> for one <c>TextBlock</c>. Painting a record's
    /// generated <c>ToString</c> is the defect <see cref="MarkerRowLabelConverter"/> was written to
    /// undo; this one is not generated.
    /// </remarks>
    public override string ToString() => Value;

    private static SpeedOption Of(double multiplier)
    {
        var value = multiplier.ToString("0.##×", CultureInfo.CurrentCulture);
        return multiplier switch
        {
            1.0 => new SpeedOption(multiplier, value, ShowText.Resource("TransportSpeedNormal", value), value),
            < 1.0 => new SpeedOption(multiplier, value, value, ShowText.Resource("TransportSpeedSlower", value)),
            _ => new SpeedOption(multiplier, value, value, ShowText.Resource("TransportSpeedFaster", value)),
        };
    }
}

/// <summary>
/// The transport bar: play, pause, skips, speed, volume, and the boost warning. Every command is a
/// use case call, so the view never touches the engine and every value arrives already clamped.
/// </summary>
public sealed class TransportControlsViewModel : INotifyPropertyChanged
{
    private readonly ControlPlayback _control;
    private IReadOnlyList<SpeedOption>? _speedOptions;
    private Commands.AsyncRelayCommand? _setSpeed;
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

    public static double MinimumVolumePercent => VolumeBoostPolicy.MinimumPercent;

    public static double MaximumVolumePercent => VolumeBoostPolicy.MaximumBoostPercent;

    public TimeSpan Position => _state.Position;

    public double SpeedMultiplier => _state.SpeedMultiplier;

    public int VolumePercent => _state.Volume.Percent;

    /// <summary>
    /// The level as it is written beside the slider, which had no number at all before.
    /// </summary>
    /// <remarks>
    /// The percent sign is quoted, and it has to be: an unquoted <c>%</c> in a numeric format string
    /// is the percent <em>specifier</em>, which multiplies by a hundred. Written the obvious way this
    /// said "8000 %" at eighty, and the test that caught it was the one asserting the text rather
    /// than the number behind it.
    /// </remarks>
    public string VolumeLabel => _state.Volume.Percent.ToString("0 '%'", CultureInfo.CurrentCulture);

    public bool IsMuted => _state.Volume.IsMuted;

    /// <summary>True while the level is above one hundred percent; the view shows text and an icon.</summary>
    public bool IsBoosted => _state.Volume.IsBoosted;

    /// <summary>Always true while boosted: the limiter and the boost are inseparable.</summary>
    public bool LimiterEngaged => _state.Volume.LimiterEngaged;

    public bool RequiresBoostWarning => _state.Volume.RequiresWarning;

    public string BackwardSkipLabel => FormatSeconds(_state.BackwardSkip);

    public string ForwardSkipLabel => FormatSeconds(_state.ForwardSkip);

    public string SpeedLabel => _state.SpeedMultiplier.ToString("0.##×", CultureInfo.CurrentCulture);

    /// <summary>Where the session is, as a clock, which is what the transport paints on the left.</summary>
    public string PositionLabel => PlaybackClock.Format(_state.Position);

    /// <summary>
    /// How long the file runs, as a clock, or nothing when the engine has not said.
    /// </summary>
    /// <remarks>
    /// Empty rather than a zero, and that is the whole reason this is a string and not a
    /// <see cref="TimeSpan"/> the view formats: a transport that answered "0:00" to "I do not know
    /// yet" would be saying the film has no length. What it does instead is show the position alone
    /// until the duration arrives, which is exactly how <see cref="HasDuration"/> is spent.
    /// </remarks>
    public string DurationLabel => _state.Duration is { } duration ? PlaybackClock.Format(duration) : string.Empty;

    /// <summary>
    /// True once the engine has said how long the file is, which is what a scrubber needs to exist.
    /// </summary>
    /// <remarks>
    /// A slider whose maximum is unknown cannot be dragged to a meaningful place — a thumb halfway
    /// along a bar of unknown length points at nothing. So the bar is absent rather than disabled
    /// while the duration is: absent says "not yet", and a greyed bar would say "not for you".
    /// </remarks>
    public bool HasDuration => _state.Duration is { } duration && duration > TimeSpan.Zero;

    /// <summary>The position in seconds, for the scrubber, which works in numbers rather than spans.</summary>
    public double PositionSeconds => _state.Position.TotalSeconds;

    /// <summary>
    /// The scrubber's maximum, in seconds.
    /// </summary>
    /// <remarks>
    /// One when nothing is known, and never zero: a <c>Slider</c> whose maximum equals its minimum
    /// puts its thumb at whatever it likes and divides by that difference to place it. The bar is not
    /// on screen in that state anyway — <see cref="HasDuration"/> keeps it away — and this keeps the
    /// arithmetic behind it from being the reason somebody finds out.
    /// </remarks>
    public double DurationSeconds => _state.Duration is { } duration && duration > TimeSpan.Zero
        ? duration.TotalSeconds
        : 1.0;

    /// <summary>The nine steps the drop-down lists, each with the three things its row writes.</summary>
    /// <remarks>
    /// Built on the first read and not in the constructor, and that is measured rather than tidy:
    /// the words come from the dictionary, resolving one needs the theme variant, and reading that
    /// needs the UI thread. Built at construction it made <em>every</em> caller of this model a UI
    /// thread caller — two plain <c>[Fact]</c>s that had only ever asked it about a playhead failed
    /// with "the calling thread cannot access this object", which is the same trap
    /// <c>[AvaloniaTheory]</c> was the answer to three days earlier. The view reads this on the UI
    /// thread, which is where a list of words belongs.
    /// </remarks>
    public IReadOnlyList<SpeedOption> SpeedOptions => _speedOptions ??= SpeedOption.All();

    /// <summary>
    /// «Volver a 1×»: the parameter arrives as the button's literal text, parsed invariant, so
    /// «1.25» means the same step on every machine.
    /// </summary>
    /// <remarks>
    /// It was the menu's eleventh row until 2026-08-28, where the prototype has always put a button
    /// of its own beside the pill — and a row that resets is a row that is not a speed, sitting in a
    /// list of speeds. The command is unchanged: what moved is which control presses it.
    /// </remarks>
    public ICommand SetSpeedCommand => _setSpeed ??= new Commands.AsyncRelayCommand(
        parameter => SetSpeedAsync(
            double.Parse((string)parameter!, System.Globalization.CultureInfo.InvariantCulture),
            CancellationToken.None),
        parameter => parameter is string text
            && double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out _));

    /// <summary>«Volver a 1×» exists only while there is something to come back from.</summary>
    public bool IsAwayFromNormalSpeed => Math.Abs(SpeedMultiplier - 1.0) > 0.001;

    public async Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
        Apply(await _control.SetSpeedAsync(multiplier, cancellationToken).ConfigureAwait(true));

    public async Task SetVolumeAsync(int percent, CancellationToken cancellationToken = default) =>
        Apply(await _control.SetVolumeAsync(percent, cancellationToken).ConfigureAwait(true));

    public async Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        Apply(await _control.SeekAsync(position, cancellationToken).ConfigureAwait(true));

    private static string FormatSeconds(TimeSpan interval) =>
        interval.TotalSeconds.ToString("0 s", CultureInfo.CurrentCulture);

    /// <summary>Takes the playhead from the engine, which is the only thing that moves this bar.</summary>
    /// <remarks>
    /// Every other value here changes because somebody pressed something, and until this existed the
    /// position did too: <see cref="Apply"/> runs on this bar's own commands and on nothing else, so
    /// a film could play for an hour and a half with no scrubber and no clock on the screen —
    /// <see cref="HasDuration"/> stays false while the duration is null, and nothing was ever going
    /// to set it. Measured on 2026-08-24 against a real film; the bar appeared the instant a skip was
    /// pressed, which is what said where the wire was missing rather than what it carried.
    ///
    /// <para>
    /// The duration is taken only when the engine gives one: the first ticks of a session arrive
    /// before the length is known, and writing a zero would put a bar on the screen whose maximum is
    /// a lie. The rest of the state — speed, volume, the two skips — is the person's, and a tick of
    /// the playhead has nothing to say about it.
    /// </para>
    /// </remarks>
    public void Observe(TimeSpan position, TimeSpan? duration)
    {
        Apply(_state with
        {
            Position = position,
            Duration = duration ?? _state.Duration,
        });
    }

    private void Apply(PlaybackControlState state)
    {
        _state = state;
        foreach (var name in new[]
        {
            nameof(Position),
            // The scale before the value, and the order is load-bearing rather than tidy. A Slider
            // coerces whatever is written into Value against the Maximum it holds at that instant,
            // and DurationSeconds answers 1 until the engine says otherwise — so announcing the
            // position first put 120 seconds into a bar whose maximum was still 1, the bar clamped it
            // to 1, and the handler below turned that clamp into a real seek. Measured on 2026-08-22:
            // the first state after a two-minute seek came back reading 0:01.
            nameof(HasDuration),
            nameof(DurationSeconds),
            nameof(DurationLabel),
            nameof(PositionSeconds),
            nameof(PositionLabel),
            nameof(SpeedMultiplier),
            nameof(SpeedLabel),
            nameof(IsAwayFromNormalSpeed),
            nameof(VolumePercent),
            nameof(VolumeLabel),
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
