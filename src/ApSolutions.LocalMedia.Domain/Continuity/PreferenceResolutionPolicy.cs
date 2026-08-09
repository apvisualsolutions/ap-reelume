using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>
/// What the session will actually use, with the scope each field came from so the interface can show
/// why a value is in force. A null source means the engine default answered.
/// </summary>
public sealed record ResolvedPlaybackPreference
{
    public required TrackSelection Audio { get; init; }

    public PreferenceScope? AudioSource { get; init; }

    public required TrackSelection Subtitle { get; init; }

    public PreferenceScope? SubtitleSource { get; init; }

    public required bool SubtitlesEnabled { get; init; }

    public PreferenceScope? SubtitlesEnabledSource { get; init; }

    public required double SpeedMultiplier { get; init; }

    public PreferenceScope? SpeedSource { get; init; }

    public required int VolumePercent { get; init; }

    public PreferenceScope? VolumeSource { get; init; }

    public string? AudioOutputDeviceId { get; init; }

    public PreferenceScope? AudioOutputSource { get; init; }

    public required SubtitleStyle SubtitleStyle { get; init; }

    public PreferenceScope? SubtitleStyleSource { get; init; }
}

/// <summary>
/// Resolves preferences field by field as File &gt; Series &gt; Global &gt; EngineDefault, and picks a
/// track from its attributes rather than from a volatile index.
/// </summary>
public static class PreferenceResolutionPolicy
{
    public const double EngineDefaultSpeed = 1.0;
    public const int EngineDefaultVolumePercent = 100;

    private static readonly TrackSelection EngineDefaultSelection = new(null, null, null, PreferExternal: false);

    /// <summary>The scopes consulted in order, most specific first.</summary>
    public static IReadOnlyList<PreferenceScope> PrecedenceOrder { get; } =
        [PreferenceScope.File, PreferenceScope.Series, PreferenceScope.Global];

    public static ResolvedPlaybackPreference Resolve(
        PlaybackPreference? file,
        PlaybackPreference? series,
        PlaybackPreference? global)
    {
        var (audio, audioSource) = Pick(file, series, global, preference => preference.Audio);
        var (subtitle, subtitleSource) = Pick(file, series, global, preference => preference.Subtitle);
        var (enabled, enabledSource) = Pick(file, series, global, preference => preference.SubtitlesEnabled);
        var (speed, speedSource) = Pick(file, series, global, preference => preference.SpeedMultiplier);
        var (volume, volumeSource) = Pick(file, series, global, preference => preference.VolumePercent);
        var (output, outputSource) = Pick(file, series, global, preference => preference.AudioOutputDeviceId);
        var (style, styleSource) = Pick(file, series, global, preference => preference.SubtitleStyle);

        return new ResolvedPlaybackPreference
        {
            Audio = audio ?? EngineDefaultSelection,
            AudioSource = audioSource,
            Subtitle = subtitle ?? EngineDefaultSelection,
            SubtitleSource = subtitleSource,
            SubtitlesEnabled = enabled ?? false,
            SubtitlesEnabledSource = enabledSource,
            SpeedMultiplier = speed ?? EngineDefaultSpeed,
            SpeedSource = speedSource,
            VolumePercent = volume ?? EngineDefaultVolumePercent,
            VolumeSource = volumeSource,
            AudioOutputDeviceId = output,
            AudioOutputSource = outputSource,
            SubtitleStyle = style ?? SubtitleStyle.EngineDefault,
            SubtitleStyleSource = styleSource,
        };
    }

    /// <summary>
    /// Chooses the track that best matches the stored attributes: language and channels first, then
    /// language alone, then channels alone, and finally the first track of that kind.
    /// </summary>
    public static MediaTrack? SelectTrack(
        IReadOnlyList<MediaTrack> tracks,
        TrackSelection selection,
        MediaTrackKind kind)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(selection);
        var candidates = tracks.Where(track => track.Kind == kind).ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var ordered = selection.PreferExternal
            ? candidates.OrderByDescending(track => track.IsExternal).ToArray()
            : candidates.OrderBy(track => track.IsExternal).ToArray();

        return FirstMatch(ordered, track => Matches(track, selection.Language) && Matches(track, selection.Channels))
            ?? FirstMatch(ordered, track => Matches(track, selection.Language))
            ?? FirstMatch(ordered, track => Matches(track, selection.Channels))
            ?? ordered[0];
    }

    private static MediaTrack? FirstMatch(IReadOnlyList<MediaTrack> tracks, Func<MediaTrack, bool> predicate) =>
        tracks.FirstOrDefault(predicate);

    private static bool Matches(MediaTrack track, string? language) =>
        language is not null
        && track.Language is not null
        && track.Language.Equals(language, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(MediaTrack track, int? channels) =>
        channels is not null && track.Channels == channels;

    private static (T? Value, PreferenceScope? Source) Pick<T>(
        PlaybackPreference? file,
        PlaybackPreference? series,
        PlaybackPreference? global,
        Func<PlaybackPreference, T?> read)
    {
        foreach (var candidate in new[] { file, series, global })
        {
            if (candidate is null)
            {
                continue;
            }

            if (read(candidate) is { } value)
            {
                return (value, candidate.Scope);
            }
        }

        return (default, null);
    }
}
