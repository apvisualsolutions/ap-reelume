using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>
/// A manual track change. The scope decides how long it lasts: this file only, the whole series, or
/// everything. The stored value is the track's attributes, never its position.
/// </summary>
public sealed record SelectTrackCommand(
    MediaTrackKind Kind,
    MediaTrack? Track,
    PreferenceScope Scope,
    string ScopeKey);

/// <summary>
/// Applies a manual track change to the active session and remembers it for the chosen scope, so the
/// next episode of the same series starts the same way.
/// </summary>
public sealed class SelectTrack
{
    private readonly IMediaPlayerEngine _engine;
    private readonly IPlaybackPreferenceRepository _repository;

    public SelectTrack(IMediaPlayerEngine engine, IPlaybackPreferenceRepository repository)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task ExecuteAsync(SelectTrackCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Kind == MediaTrackKind.Video)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Video tracks are not switchable.");
        }

        await _engine
            .SelectTrackAsync(command.Kind, command.Track?.Id, cancellationToken)
            .ConfigureAwait(false);

        var stored = await _repository
            .GetAsync(command.Scope, command.ScopeKey, cancellationToken)
            .ConfigureAwait(false)
            ?? new PlaybackPreference { Scope = command.Scope, ScopeKey = command.ScopeKey };
        var selection = command.Track is { } track
            ? new TrackSelection(track.Language, track.Channels, track.Codec, track.IsExternal)
            : null;

        var updated = command.Kind == MediaTrackKind.Audio
            ? stored with { Audio = selection }
            : stored with { Subtitle = selection, SubtitlesEnabled = selection is not null };

        await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
    }
}
