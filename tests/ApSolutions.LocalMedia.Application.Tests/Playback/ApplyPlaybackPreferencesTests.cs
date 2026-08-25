// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Playback;

/// <summary>
/// What a session does with the tracks on its way in. The distinction under test is between a scope
/// that <b>answered</b> and one that stayed silent: applying the resolved value either way is what
/// made every first playback hand the engine <c>-1</c> for subtitles and switch off the track the
/// container had marked as its default — reported by the owner on 2026-08-25, who watched the same
/// episode show subtitles in VLC and none here.
/// </summary>
public sealed class ApplyPlaybackPreferencesTests
{
    private static readonly MediaTrack SpanishAudio =
        new("1", MediaTrackKind.Audio, "spa", "Español", 6, "AC3");

    private static readonly MediaTrack EnglishAudio =
        new("2", MediaTrackKind.Audio, "eng", "English", 2, "AAC");

    private static readonly MediaTrack SpanishSubtitle =
        new("3", MediaTrackKind.Subtitle, "spa", "Español", null, "SUBRIP");

    [Fact]
    public async Task Silence_leaves_the_engines_own_choice_alone()
    {
        var engine = new RecordingEngine(
            [SpanishAudio, EnglishAudio, SpanishSubtitle],
            activeAudioTrackId: "1",
            activeSubtitleTrackId: "3");
        var apply = new ApplyPlaybackPreferences(new EmptyPreferences());

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Empty(engine.Selections);
        Assert.Equal(SpanishAudio, applied.Audio);
        Assert.Equal(SpanishSubtitle, applied.Subtitle);
    }

    [Fact]
    public async Task Silence_reports_subtitles_as_off_when_the_engine_has_none_in_force()
    {
        var engine = new RecordingEngine(
            [SpanishAudio, SpanishSubtitle],
            activeAudioTrackId: "1",
            activeSubtitleTrackId: null);
        var apply = new ApplyPlaybackPreferences(new EmptyPreferences());

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Empty(engine.Selections);
        Assert.Null(applied.Subtitle);
    }

    [Fact]
    public async Task A_stored_choice_to_turn_subtitles_off_is_still_applied()
    {
        var engine = new RecordingEngine(
            [SpanishAudio, SpanishSubtitle],
            activeAudioTrackId: "1",
            activeSubtitleTrackId: "3");
        var apply = new ApplyPlaybackPreferences(new StoredPreferences(new PlaybackPreference
        {
            Scope = PreferenceScope.File,
            ScopeKey = "file",
            SubtitlesEnabled = false,
        }));

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Equal([(MediaTrackKind.Subtitle, null)], engine.Selections);
        Assert.Null(applied.Subtitle);
    }

    [Fact]
    public async Task A_stored_choice_names_the_track_by_its_attributes()
    {
        var engine = new RecordingEngine(
            [EnglishAudio, SpanishAudio, SpanishSubtitle],
            activeAudioTrackId: "2",
            activeSubtitleTrackId: null);
        var apply = new ApplyPlaybackPreferences(new StoredPreferences(new PlaybackPreference
        {
            Scope = PreferenceScope.Series,
            ScopeKey = "series",
            Audio = new TrackSelection("spa", 6, null, PreferExternal: false),
            Subtitle = new TrackSelection("spa", null, null, PreferExternal: false),
            SubtitlesEnabled = true,
        }));

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [(MediaTrackKind.Audio, "1"), (MediaTrackKind.Subtitle, "3")],
            engine.Selections);
        Assert.Equal(SpanishAudio, applied.Audio);
        Assert.Equal(SpanishSubtitle, applied.Subtitle);
    }

    [Fact]
    public async Task The_subtitles_beside_the_media_are_attached_before_anything_is_chosen()
    {
        var engine = new RecordingEngine([SpanishAudio], activeAudioTrackId: "1", activeSubtitleTrackId: null);
        var apply = new ApplyPlaybackPreferences(new EmptyPreferences());

        _ = await apply.ApplyAsync(
            engine,
            new PlaybackPreferenceContext("file", "series", ["beside.srt", "beside.es.srt"]),
            TestContext.Current.CancellationToken);

        Assert.Equal(["beside.srt", "beside.es.srt"], engine.ExternalSubtitles);
    }

    private static PlaybackPreferenceContext Context() => new("file", "series", []);

    /// <summary>Records what the engine was told to do, and answers with a fixed announcement.</summary>
    private sealed class RecordingEngine(
        IReadOnlyList<MediaTrack> tracks,
        string? activeAudioTrackId,
        string? activeSubtitleTrackId) : IMediaPlayerEngine
    {
        public List<(MediaTrackKind Kind, string? TrackId)> Selections { get; } = [];

        public List<string> ExternalSubtitles { get; } = [];

        public PlaybackState State => PlaybackState.Playing;

#pragma warning disable CS0067 // The contract declares these events; this double never raises them.
        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

        public event EventHandler<PlaybackFailureEventArgs>? Failure;
#pragma warning restore CS0067

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(
                State,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(50),
                tracks,
                failure: null,
                activeAudioTrackId,
                activeSubtitleTrackId));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default)
        {
            Selections.Add((kind, trackId));
            return Task.CompletedTask;
        }

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ExternalSubtitles.Add(path);
            return Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle, IsExternal: true));
        }

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A library nobody has expressed a preference in.</summary>
    private sealed class EmptyPreferences : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PlaybackPreference?>(null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>One stored row, answered for its own scope and for nothing else.</summary>
    private sealed class StoredPreferences(PlaybackPreference stored) : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(scope == stored.Scope ? stored : null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
