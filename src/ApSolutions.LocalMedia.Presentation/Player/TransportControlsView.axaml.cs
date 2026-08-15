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
}
