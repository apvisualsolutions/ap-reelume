// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>
/// What the session is about to open. External subtitle paths are supplied by the caller, already
/// confined to the library root; this use case never searches the disk.
/// </summary>
public sealed record PlaybackPreferenceContext(
    string FileScopeKey,
    string? SeriesScopeKey,
    IReadOnlyList<string> ExternalSubtitlePaths);

/// <summary>What was actually applied, so the interface can show the effective values and why.</summary>
public sealed record AppliedPlaybackPreference(
    ResolvedPlaybackPreference Resolved,
    MediaTrack? Audio,
    MediaTrack? Subtitle,
    IReadOnlyList<MediaTrack> AvailableTracks);

/// <summary>
/// Resolves the stored scopes and applies them to the active session. A preference that names an
/// absent track falls back by language and channels instead of failing, so a reordered episode still
/// sounds the way the person chose.
/// </summary>
public sealed class ApplyPlaybackPreferences
{
    private readonly IPlaybackPreferenceRepository _repository;

    public ApplyPlaybackPreferences(IPlaybackPreferenceRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>Reads the three scopes and resolves them field by field.</summary>
    public async Task<ResolvedPlaybackPreference> ResolveAsync(
        PlaybackPreferenceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await _repository
            .GetAsync(PreferenceScope.File, context.FileScopeKey, cancellationToken)
            .ConfigureAwait(false);
        var series = context.SeriesScopeKey is { } seriesKey
            ? await _repository.GetAsync(PreferenceScope.Series, seriesKey, cancellationToken).ConfigureAwait(false)
            : null;
        var global = await _repository
            .GetAsync(PreferenceScope.Global, PlaybackPreference.GlobalKey, cancellationToken)
            .ConfigureAwait(false);

        return PreferenceResolutionPolicy.Resolve(file, series, global);
    }

    public async Task<AppliedPlaybackPreference> ApplyAsync(
        IMediaPlayerEngine engine,
        PlaybackPreferenceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(context);
        var resolved = await ResolveAsync(context, cancellationToken).ConfigureAwait(false);

        foreach (var path in context.ExternalSubtitlePaths)
        {
            _ = await engine.AddExternalSubtitleAsync(path, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = await engine.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        // A scope that answered is a decision somebody took; a scope that did not is silence, and
        // silence is not «off». Applying the resolved value either way is what made a first
        // playback disable subtitles the container had marked as its default — the engine had
        // already selected the Spanish track, and this handed it -1 on the way in. Reported by the
        // owner on 2026-08-25: the same episode showed subtitles in VLC and none here.
        var audio = resolved.AudioSource is null
            ? Find(snapshot.Tracks, snapshot.ActiveAudioTrackId)
            : await SelectAsync(MediaTrackKind.Audio, resolved.Audio).ConfigureAwait(false);
        var subtitle = resolved.SubtitlesEnabledSource is null && resolved.SubtitleSource is null
            ? Find(snapshot.Tracks, snapshot.ActiveSubtitleTrackId)
            : await SelectSubtitleAsync().ConfigureAwait(false);

        return new AppliedPlaybackPreference(resolved, audio, subtitle, snapshot.Tracks);

        async Task<MediaTrack?> SelectAsync(MediaTrackKind kind, TrackSelection selection)
        {
            var track = PreferenceResolutionPolicy.SelectTrack(snapshot.Tracks, selection, kind);
            if (track is not null)
            {
                await engine.SelectTrackAsync(kind, track.Id, cancellationToken).ConfigureAwait(false);
            }

            return track;
        }

        async Task<MediaTrack?> SelectSubtitleAsync()
        {
            var track = resolved.SubtitlesEnabled
                ? PreferenceResolutionPolicy.SelectTrack(
                    snapshot.Tracks,
                    resolved.Subtitle,
                    MediaTrackKind.Subtitle)
                : null;
            await engine
                .SelectTrackAsync(MediaTrackKind.Subtitle, track?.Id, cancellationToken)
                .ConfigureAwait(false);
            return track;
        }
    }

    /// <summary>The announced track carrying an identifier, or null when nothing carries it.</summary>
    private static MediaTrack? Find(IReadOnlyList<MediaTrack> tracks, string? trackId) =>
        trackId is null
            ? null
            : tracks.FirstOrDefault(track => string.Equals(track.Id, trackId, StringComparison.Ordinal));
}
