// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>One selectable track, described by its attributes rather than by its position.</summary>
/// <remarks>
/// <para>
/// A class and no longer a record, because a row now carries whether it is the one in force. A
/// record's synthesised equality compares every field, so two options that agree on track and label
/// would be equal while disagreeing about which is chosen — and the selection setter compares with
/// <c>EqualityComparer&lt;T&gt;</c>, which is where that would have surfaced as a choice that does
/// not stick.
/// </para>
/// <para>
/// The state lives here rather than in the view for the reason <c>SeasonViewModel</c> already
/// carries: a row that cannot say whether it is the chosen one is a list of identical rows, and a
/// second one could quietly light up beside the first.
/// </para>
/// </remarks>
public sealed class TrackOption(MediaTrack? track, string display) : INotifyPropertyChanged
{
    private bool _isSelected;

    public MediaTrack? Track { get; } = track;

    public string Display { get; } = display ?? throw new ArgumentNullException(nameof(display));

    /// <summary>The entry that turns subtitles off, which carries no track of its own.</summary>
    public bool IsDisabled => Track is null;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Whether this is the track being played, which is what fills its row.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

/// <summary>
/// Lets the person change the audio and subtitle track of the active session and remembers the
/// choice for the scope they picked, so the next episode of the same show starts the same way.
/// </summary>
public sealed class TrackSelectorViewModel : INotifyPropertyChanged
{
    private readonly SelectTrack _selectTrack;
    private readonly string _fileScopeKey;
    private readonly string? _seriesScopeKey;
    private TrackOption? _selectedAudio;
    private TrackOption? _selectedSubtitle;
    private bool _rememberForSeries;
    private bool _isLoading;

    public TrackSelectorViewModel(
        SelectTrack selectTrack,
        string fileScopeKey,
        string? seriesScopeKey = null,
        string? disabledSubtitleDisplay = null)
    {
        _selectTrack = selectTrack ?? throw new ArgumentNullException(nameof(selectTrack));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileScopeKey);
        _fileScopeKey = fileScopeKey;
        _seriesScopeKey = seriesScopeKey;
        DisabledSubtitleDisplay = string.IsNullOrWhiteSpace(disabledSubtitleDisplay)
            ? "—"
            : disabledSubtitleDisplay;
        ChooseAudioCommand = new ChooseTrackCommand(option => SelectedAudio = option);
        ChooseSubtitleCommand = new ChooseTrackCommand(option => SelectedSubtitle = option);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Picks one row of the audio list, by the option the row carries.</summary>
    /// <remarks>
    /// A command and not a two-way <c>IsChecked</c>, which is the shape <c>ShowDetailsView</c>'s
    /// season pills already have and for the same reason: a click that <em>un</em>checked a row
    /// would leave the list showing a track no row claims. The choice of which row is lit belongs to
    /// the selection, and the selection is what a click asks to change.
    /// </remarks>
    public ICommand ChooseAudioCommand { get; }

    /// <summary>Picks one row of the subtitle list, by the option the row carries.</summary>
    public ICommand ChooseSubtitleCommand { get; }

    public ObservableCollection<TrackOption> AudioTracks { get; } = [];

    public ObservableCollection<TrackOption> SubtitleTracks { get; } = [];

    /// <summary>
    /// There is nothing to choose between, which here is <b>not</b> an empty list.
    /// </summary>
    /// <remarks>
    /// The subtitle list always carries the "off" option this view model adds itself, so it never
    /// reaches zero: counting items would define a state that cannot happen. One audio track and at
    /// most one subtitle track beside "off" is what leaves a person with no decision to make.
    /// </remarks>
    public bool HasNothingToChoose => AudioTracks.Count <= 1 && SubtitleTracks.Count <= 2;

    /// <summary>Localised label of the "no subtitles" entry, supplied by the composition root.</summary>
    public string DisabledSubtitleDisplay { get; }

    /// <summary>True when the person can ask for the choice to apply to the whole series.</summary>
    public bool CanRememberForSeries => _seriesScopeKey is not null;

    public bool RememberForSeries
    {
        get => _rememberForSeries;
        set => _ = SetField(ref _rememberForSeries, value && CanRememberForSeries);
    }

    /// <summary>
    /// The chosen audio track. Choosing one applies it: a list that changes the label and leaves the
    /// media as it was is a control that does nothing, which is what the real application did.
    /// </summary>
    public TrackOption? SelectedAudio
    {
        get => _selectedAudio;
        set
        {
            if (!SetField(ref _selectedAudio, value))
            {
                return;
            }

            Mark(AudioTracks, value);
            if (!_isLoading)
            {
                _ = ApplyAsync(MediaTrackKind.Audio);
            }
        }
    }

    public TrackOption? SelectedSubtitle
    {
        get => _selectedSubtitle;
        set
        {
            if (!SetField(ref _selectedSubtitle, value))
            {
                return;
            }

            Mark(SubtitleTracks, value);
            if (!_isLoading)
            {
                _ = ApplyAsync(MediaTrackKind.Subtitle);
            }
        }
    }

    /// <summary>
    /// Lights the chosen row and puts out every other one in the same list.
    /// </summary>
    /// <remarks>
    /// Written over the whole list rather than over the pair that changed, because the pair is not
    /// always two: loading rebuilds both lists, and a row left lit from the previous media would sit
    /// under a selection that no longer names it.
    /// </remarks>
    private static void Mark(IEnumerable<TrackOption> options, TrackOption? chosen)
    {
        foreach (var option in options)
        {
            option.IsSelected = ReferenceEquals(option, chosen);
        }
    }

    /// <summary>Rebuilds the lists from what the engine announced for the active media.</summary>
    public void Load(IReadOnlyList<MediaTrack> tracks, MediaTrack? activeAudio, MediaTrack? activeSubtitle)
    {
        ArgumentNullException.ThrowIfNull(tracks);

        // Showing what is already playing is not a choice, so loading never stores a preference.
        _isLoading = true;
        try
        {
            LoadCore(tracks, activeAudio, activeSubtitle);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadCore(IReadOnlyList<MediaTrack> tracks, MediaTrack? activeAudio, MediaTrack? activeSubtitle)
    {
        AudioTracks.Clear();
        SubtitleTracks.Clear();
        SubtitleTracks.Add(new TrackOption(null, DisabledSubtitleDisplay));

        foreach (var track in tracks.Where(track => track.Kind == MediaTrackKind.Audio))
        {
            AudioTracks.Add(new TrackOption(track, Describe(track)));
        }

        foreach (var track in tracks.Where(track => track.Kind == MediaTrackKind.Subtitle))
        {
            SubtitleTracks.Add(new TrackOption(track, Describe(track)));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNothingToChoose)));
        SelectedAudio = AudioTracks.FirstOrDefault(option => option.Track?.Id == activeAudio?.Id);
        SelectedSubtitle = SubtitleTracks.FirstOrDefault(option => option.Track?.Id == activeSubtitle?.Id)
            ?? SubtitleTracks[0];
    }

    /// <summary>Applies the current selection to the session and stores it in the chosen scope.</summary>
    public async Task ApplyAsync(MediaTrackKind kind, CancellationToken cancellationToken = default)
    {
        var option = kind == MediaTrackKind.Audio ? SelectedAudio : SelectedSubtitle;
        var scope = RememberForSeries && _seriesScopeKey is not null
            ? PreferenceScope.Series
            : PreferenceScope.File;
        var key = scope == PreferenceScope.Series ? _seriesScopeKey! : _fileScopeKey;

        await _selectTrack
            .ExecuteAsync(new SelectTrackCommand(kind, option?.Track, scope, key), cancellationToken)
            .ConfigureAwait(true);
    }

    private static string Describe(MediaTrack track)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(track.Description))
        {
            parts.Add(track.Description);
        }
        else if (!string.IsNullOrWhiteSpace(track.Language))
        {
            parts.Add(track.Language);
        }

        if (track.Channels is { } channels)
        {
            parts.Add(channels switch
            {
                1 => "1.0",
                2 => "2.0",
                6 => "5.1",
                8 => "7.1",
                _ => channels.ToString(CultureInfo.InvariantCulture) + " ch",
            });
        }

        if (!string.IsNullOrWhiteSpace(track.Codec))
        {
            parts.Add(track.Codec);
        }

        if (track.IsExternal)
        {
            parts.Add("·");
        }

        return parts.Count == 0 ? track.Id : string.Join(" · ", parts);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <summary>
    /// The click on one row of either list.
    /// </summary>
    /// <remarks>
    /// The silence of <c>CanExecuteChanged</c> is safe here and <c>CommandNotificationTests</c> holds
    /// the predicate that makes it so: the question is whether the parameter is an option at all,
    /// and that answer does not move while a row is on screen. Rewriting it to read state fails that
    /// gate in the same change.
    /// </remarks>
    private sealed class ChooseTrackCommand(Action<TrackOption> apply) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is TrackOption;

        public void Execute(object? parameter)
        {
            if (parameter is TrackOption option)
            {
                apply(option);
            }
        }
    }
}
