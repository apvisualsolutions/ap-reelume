// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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

    /// <summary>
    /// ENG-011. The speed was stored, resolved and written to SQLite, and <b>nobody applied it</b>:
    /// `resolved.SpeedMultiplier` was read in exactly zero places across `src/`.
    /// </summary>
    [Fact]
    public async Task The_stored_speed_reaches_the_engine_as_the_file_opens()
    {
        var engine = new RecordingEngine([SpanishAudio], activeAudioTrackId: "1", activeSubtitleTrackId: null);
        var apply = new ApplyPlaybackPreferences(new StoredPreferences(new PlaybackPreference
        {
            Scope = PreferenceScope.Global,
            ScopeKey = PlaybackPreference.GlobalKey,
            SpeedMultiplier = 1.5,
        }));

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Equal(1.5, Assert.Single(engine.Speeds));
        Assert.Equal(PreferenceScope.Global, applied.Resolved.SpeedSource);
    }

    /// <summary>
    /// <b>The half that is a defect today and not only a forgotten setting.</b> The engine and the
    /// transport both outlive the file — they are singletons — so a film left at 1.5× hands the next
    /// film 1.5× without anybody asking, and the screen reads it back as if it had been chosen. The
    /// comment beside the picture adjustment already said this in writing about itself; the speed
    /// has the same shape and nobody joined the two. So silence is not «leave whatever is running»:
    /// silence is the engine's own normal speed, written every time.
    /// </summary>
    [Fact]
    public async Task Silence_puts_the_speed_back_to_normal_instead_of_carrying_the_last_film_over()
    {
        var engine = new RecordingEngine([SpanishAudio], activeAudioTrackId: "1", activeSubtitleTrackId: null);
        var apply = new ApplyPlaybackPreferences(new EmptyPreferences());

        _ = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Equal(1.0, Assert.Single(engine.Speeds));
    }

    [Fact]
    public async Task The_stored_picture_adjustment_reaches_the_engine_as_the_file_opens()
    {
        var engine = new RecordingEngine([SpanishAudio], activeAudioTrackId: "1", activeSubtitleTrackId: null);
        var apply = new ApplyPlaybackPreferences(new StoredPreferences(new PlaybackPreference
        {
            Scope = PreferenceScope.Series,
            ScopeKey = "series",
            Picture = new PictureAdjustment(0.3, 1.2, 1.6),
        }));

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Equal(new PictureAdjustment(0.3, 1.2, 1.6), engine.PictureAdjustment);
        Assert.Equal(PreferenceScope.Series, applied.Resolved.PictureSource);
    }

    /// <summary>
    /// Here silence <b>is</b> a value, which is the opposite of the subtitle rule above and worth the
    /// words. The engine outlives the file: it is a singleton, so an adjustment left over from the
    /// last film would carry into the next one unless opening writes the resolved value either way.
    /// </summary>
    [Fact]
    public async Task A_file_nobody_adjusted_opens_neutral_instead_of_keeping_the_last_ones()
    {
        var engine = new RecordingEngine([SpanishAudio], activeAudioTrackId: "1", activeSubtitleTrackId: null)
        {
            PictureAdjustment = new PictureAdjustment(0.5, 1.4, 2.0),
        };
        var apply = new ApplyPlaybackPreferences(new EmptyPreferences());

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Equal(PictureAdjustment.Neutral, engine.PictureAdjustment);
        Assert.Null(applied.Resolved.PictureSource);
    }

    /// <summary>
    /// An engine with no picture to adjust is opened without complaint, and this is the half of the
    /// test that the double above cannot give: it implements the interface by construction, so the
    /// filter around the assignment has no failing branch anywhere in the suite. An engine that
    /// draws into a window of its own has no pixels here, which is why the two interfaces are
    /// separate at all.
    /// </summary>
    [Fact]
    public async Task An_engine_with_no_picture_to_adjust_opens_without_complaint()
    {
        var inner = new RecordingEngine([SpanishAudio], activeAudioTrackId: "1", activeSubtitleTrackId: null);
        var engine = new EngineWithoutAPicture(inner);
        var apply = new ApplyPlaybackPreferences(new StoredPreferences(new PlaybackPreference
        {
            Scope = PreferenceScope.Series,
            ScopeKey = "series",
            Picture = new PictureAdjustment(0.3, 1.2, 1.6),
        }));

        var applied = await apply.ApplyAsync(engine, Context(), TestContext.Current.CancellationToken);

        Assert.Equal(new PictureAdjustment(0.3, 1.2, 1.6), applied.Resolved.Picture);
        Assert.Equal(PictureAdjustment.Neutral, inner.PictureAdjustment);
        Assert.Equal(SpanishAudio, applied.Audio);
    }

    private static PlaybackPreferenceContext Context() => new("file", "series", []);

    /// <summary>Records what the engine was told to do, and answers with a fixed announcement.</summary>
    private sealed class RecordingEngine(
        IReadOnlyList<MediaTrack> tracks,
        string? activeAudioTrackId,
        string? activeSubtitleTrackId) : IMediaPlayerEngine, IPictureAdjustable
    {
        public PictureAdjustment PictureAdjustment { get; set; } = PictureAdjustment.Neutral;

        /// <summary>Every speed this engine was told to run at, which nobody used to tell it.</summary>
        public List<double> Speeds { get; } = [];

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

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default)
        {
            Speeds.Add(multiplier);
            return Task.CompletedTask;
        }

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// An engine seen only as one: everything forwarded, and <see cref="IPictureAdjustable"/> left
    /// behind, which is what a decorator around the real engine would do by accident.
    /// </summary>
    private sealed class EngineWithoutAPicture(RecordingEngine inner) : IMediaPlayerEngine
    {
        public PlaybackState State => inner.State;

#pragma warning disable CS0067 // The contract declares these events; this double never raises them.
        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

        public event EventHandler<PlaybackFailureEventArgs>? Failure;
#pragma warning restore CS0067

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            inner.InitializeAsync(cancellationToken);

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            inner.OpenAsync(request, cancellationToken);

        public Task PlayAsync(CancellationToken cancellationToken = default) => inner.PlayAsync(cancellationToken);

        public Task PauseAsync(CancellationToken cancellationToken = default) => inner.PauseAsync(cancellationToken);

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            inner.SeekAsync(position, cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default) => inner.StopAsync(cancellationToken);

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            inner.GetSnapshotAsync(cancellationToken);

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) =>
            inner.SelectTrackAsync(kind, trackId, cancellationToken);

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            inner.AddExternalSubtitleAsync(path, cancellationToken);

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            inner.SetSpeedAsync(multiplier, cancellationToken);

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            inner.SetAudioOutputDeviceAsync(deviceId, cancellationToken);

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            inner.ApplyVolumeAsync(decision, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
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
