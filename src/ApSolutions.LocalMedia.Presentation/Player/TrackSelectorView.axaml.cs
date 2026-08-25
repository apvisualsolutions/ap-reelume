// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The two track pickers of a session, mounted whole or one half at a time.
/// </summary>
/// <remarks>
/// The prototype heads the player with four pills and puts audio tracks under one and subtitle
/// tracks under another, which is a person's grouping rather than this view model's: both lists come
/// out of the same file and the same reader. Rather than split the view — a second .axaml is a
/// second entry in the coverage ledger and a second row in the leading-action table for one
/// ComboBox — each half says whether it is drawn, and the two panels mount this view twice.
/// <para>
/// Both default to <see langword="true"/>, so the view mounted with nothing set is the whole of it:
/// that is what the overflow and leading-action gates measure, and a default of false would hand
/// them an empty control and call it measured.
/// </para>
/// </remarks>
public sealed partial class TrackSelectorView : UserControl
{
    /// <summary>Whether the audio picker is part of this mounting.</summary>
    public static readonly StyledProperty<bool> ShowsAudioProperty =
        AvaloniaProperty.Register<TrackSelectorView, bool>(nameof(ShowsAudio), defaultValue: true);

    /// <summary>Whether the subtitle picker is part of this mounting.</summary>
    public static readonly StyledProperty<bool> ShowsSubtitlesProperty =
        AvaloniaProperty.Register<TrackSelectorView, bool>(nameof(ShowsSubtitles), defaultValue: true);

    public TrackSelectorView()
    {
        InitializeComponent();
    }

    public bool ShowsAudio
    {
        get => GetValue(ShowsAudioProperty);
        set => SetValue(ShowsAudioProperty, value);
    }

    public bool ShowsSubtitles
    {
        get => GetValue(ShowsSubtitlesProperty);
        set => SetValue(ShowsSubtitlesProperty, value);
    }
}
