using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>One selectable track, described by its attributes rather than by its position.</summary>
public sealed record TrackOption(MediaTrack? Track, string Display)
{
    public bool IsDisabled => Track is null;
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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TrackOption> AudioTracks { get; } = [];

    public ObservableCollection<TrackOption> SubtitleTracks { get; } = [];

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
            if (SetField(ref _selectedAudio, value) && !_isLoading)
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
            if (SetField(ref _selectedSubtitle, value) && !_isLoading)
            {
                _ = ApplyAsync(MediaTrackKind.Subtitle);
            }
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
}
