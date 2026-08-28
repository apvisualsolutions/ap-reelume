// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Commands;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace ApSolutions.LocalMedia.Presentation.Player;

public sealed partial class TransportControlsView : UserControl
{
    public TransportControlsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Carries a level chosen on the slider to the session that is playing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until this existed the slider was scenery. It was the one <c>Slider</c> of the five in the
    /// application bound <c>OneWay</c>, and <c>SetVolumeAsync</c> had exactly two callers, both of
    /// them the keyboard: moving the thumb with a mouse changed a number on the screen and nothing
    /// the person could hear, and the next state from the engine put the thumb back. Measured on
    /// 2026-08-15 by pressing it.
    /// </para>
    /// <para>
    /// The equality check is what keeps this from looping. Applying the answer raises
    /// <c>VolumePercent</c>, the one-way binding writes it back into the slider, and that raises this
    /// event again — with the value the view model already holds, which is the case that returns.
    /// </para>
    /// </remarks>
    private void OnVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DataContext is not TransportControlsViewModel viewModel)
        {
            return;
        }

        var requested = (int)Math.Round(e.NewValue);
        if (requested == viewModel.VolumePercent)
        {
            return;
        }

        GuardedEvent.Run(() => viewModel.SetVolumeAsync(requested));
    }

    /// <summary>
    /// Carries a position chosen on the scrubber to the session that is playing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same shape as the volume, and it exists for the same reason: <c>SeekAsync</c> had callers
    /// in the keyboard and in the skip buttons and none on a bar, because there was no bar. Nothing
    /// in this application could take somebody to a chosen minute of a film with a pointer.
    /// </para>
    /// <para>
    /// Both sides are rounded to whole seconds, which is what keeps this from looping <em>and</em>
    /// from firing on its own: the engine's position arrives in fractions and the one-way binding
    /// writes each of them into the thumb, so an exact comparison would call a seek on every tick of
    /// playback — a seek to where the session already is, several times a second.
    /// </para>
    /// </remarks>
    private void OnPositionChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DataContext is not TransportControlsViewModel viewModel)
        {
            return;
        }

        // A bar whose maximum is not the file's length has not finished being told what it is, and a
        // value that arrived while that was true is a clamp rather than a choice. This is the second
        // half of the same defect the notification order above describes: with only the order fixed,
        // any future path that set Value before Maximum would seek somebody's film to one second.
        //
        // The scrubber is named rather than taken from `sender`, and that is measured: written as
        // `sender is Slider slider` the pattern adds a branch for "the sender is not the slider",
        // which nothing can take — this handler is attached to that one control and to nothing else.
        // A guard no caller can reach is a branch no test can cover, which is what dragged
        // FluentThemeService's coverage backwards on the same day.
        if (Math.Abs(PositionSlider.Maximum - viewModel.DurationSeconds) > 0.5)
        {
            return;
        }

        var requested = Math.Round(e.NewValue);
        if (Math.Abs(requested - Math.Round(viewModel.PositionSeconds)) < 0.5)
        {
            return;
        }

        GuardedEvent.Run(() => viewModel.SeekAsync(TimeSpan.FromSeconds(requested)));
    }

    /// <summary>
    /// Carries a step chosen in the speed menu to the session that is playing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The third of the same shape, and it is here for the same reason the other two are: what comes
    /// back from the engine is the speed it actually took, clamped by
    /// <see cref="ApSolutions.LocalMedia.Domain.Playback.PlaybackControlPolicy"/>, and a two-way
    /// binding would write the asked-for value into a property that has no setter.
    /// </para>
    /// <para>
    /// The equality check is what keeps this from looping and from firing on its own. Applying the
    /// answer raises <c>SpeedMultiplier</c>, the one-way <c>SelectedValue</c> puts the row back, and
    /// that raises this event again with the value the model already holds — which is the case that
    /// returns. It is also every selection the control makes for itself: a list handed an
    /// <c>ItemsSource</c> selects the matching row while the view is still being built.
    /// </para>
    /// </remarks>
    private void OnSpeedChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = e;
        if (DataContext is not TransportControlsViewModel viewModel
            || SpeedReadout.SelectedValue is not double requested
            || Math.Abs(requested - viewModel.SpeedMultiplier) < 0.001)
        {
            return;
        }

        GuardedEvent.Run(() => viewModel.SetSpeedAsync(requested));
    }
}
