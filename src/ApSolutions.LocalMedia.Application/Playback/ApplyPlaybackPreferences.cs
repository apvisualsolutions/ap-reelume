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
        var audio = PreferenceResolutionPolicy.SelectTrack(
            snapshot.Tracks,
            resolved.Audio,
            MediaTrackKind.Audio);
        if (audio is not null)
        {
            await engine.SelectTrackAsync(MediaTrackKind.Audio, audio.Id, cancellationToken).ConfigureAwait(false);
        }

        MediaTrack? subtitle = null;
        if (resolved.SubtitlesEnabled)
        {
            subtitle = PreferenceResolutionPolicy.SelectTrack(
                snapshot.Tracks,
                resolved.Subtitle,
                MediaTrackKind.Subtitle);
        }

        await engine
            .SelectTrackAsync(MediaTrackKind.Subtitle, subtitle?.Id, cancellationToken)
            .ConfigureAwait(false);

        return new AppliedPlaybackPreference(resolved, audio, subtitle, snapshot.Tracks);
    }
}
