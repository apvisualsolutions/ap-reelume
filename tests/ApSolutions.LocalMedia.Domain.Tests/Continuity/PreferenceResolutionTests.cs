// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Continuity;

/// <summary>
/// Preferences resolve field by field as File &gt; Series &gt; Global &gt; EngineDefault, and a track is
/// chosen by its attributes so a reordered episode keeps the same audible result.
/// </summary>
public sealed class PreferenceResolutionTests
{
    private static readonly MediaTrack SpanishStereo =
        new("1", MediaTrackKind.Audio, "spa", "Español", 2, "aac");

    private static readonly MediaTrack SpanishSurround =
        new("2", MediaTrackKind.Audio, "spa", "Español 5.1", 6, "eac3");

    private static readonly MediaTrack EnglishSurround =
        new("3", MediaTrackKind.Audio, "eng", "English 5.1", 6, "eac3");

    private static readonly MediaTrack SpanishSubtitle =
        new("10", MediaTrackKind.Subtitle, "spa", "Español", null, "subrip");

    private static readonly MediaTrack ExternalSpanishSubtitle =
        new("ext:0", MediaTrackKind.Subtitle, "spa", "Español (externo)", null, "subrip", IsExternal: true);

    [Fact]
    public void The_file_scope_wins_over_the_series_scope_which_wins_over_the_global_scope()
    {
        var global = Preference(PreferenceScope.Global, "global", audioLanguage: "eng", speed: 1.0);
        var series = Preference(PreferenceScope.Series, "series:1", audioLanguage: "spa", speed: 1.25);
        var file = Preference(PreferenceScope.File, "file:1", audioLanguage: "jpn");

        var resolved = PreferenceResolutionPolicy.Resolve(file, series, global);

        Assert.Equal("jpn", resolved.Audio.Language);
        Assert.Equal(PreferenceScope.File, resolved.AudioSource);
        Assert.Equal(1.25, resolved.SpeedMultiplier);
        Assert.Equal(PreferenceScope.Series, resolved.SpeedSource);
    }

    [Fact]
    public void An_absent_scope_falls_through_to_the_next_one_and_finally_to_the_engine_default()
    {
        var resolved = PreferenceResolutionPolicy.Resolve(null, null, null);

        Assert.Null(resolved.Audio.Language);
        Assert.Null(resolved.AudioSource);
        Assert.Equal(PreferenceResolutionPolicy.EngineDefaultSpeed, resolved.SpeedMultiplier);
        Assert.Null(resolved.SpeedSource);
        Assert.False(resolved.SubtitlesEnabled);
        Assert.Equal(SubtitleStyle.EngineDefault, resolved.SubtitleStyle);
    }

    [Fact]
    public void A_series_preference_reapplies_to_the_next_episode_without_a_file_preference()
    {
        var series = Preference(PreferenceScope.Series, "series:1", audioLanguage: "spa", subtitleLanguage: "spa");

        var firstEpisode = PreferenceResolutionPolicy.Resolve(null, series, null);
        var nextEpisode = PreferenceResolutionPolicy.Resolve(null, series, null);

        Assert.Equal(firstEpisode.Audio, nextEpisode.Audio);
        Assert.Equal(firstEpisode.Subtitle, nextEpisode.Subtitle);
        Assert.True(nextEpisode.SubtitlesEnabled);
    }

    [Fact]
    public void A_track_is_matched_by_language_and_channels_not_by_its_position()
    {
        var reordered = new[] { EnglishSurround, SpanishSurround, SpanishStereo };
        var selection = new TrackSelection("spa", 6, null, PreferExternal: false);

        var chosen = PreferenceResolutionPolicy.SelectTrack(reordered, selection, MediaTrackKind.Audio);

        Assert.Equal(SpanishSurround, chosen);
        Assert.Equal(
            SpanishSurround,
            PreferenceResolutionPolicy.SelectTrack(
                [SpanishSurround, EnglishSurround, SpanishStereo],
                selection,
                MediaTrackKind.Audio));
    }

    [Fact]
    public void A_missing_channel_layout_falls_back_to_the_same_language_then_to_the_first_track()
    {
        var onlyStereo = new[] { EnglishSurround, SpanishStereo };
        var noSpanish = new[] { EnglishSurround };

        Assert.Equal(
            SpanishStereo,
            PreferenceResolutionPolicy.SelectTrack(
                onlyStereo,
                new TrackSelection("spa", 6, null, PreferExternal: false),
                MediaTrackKind.Audio));
        Assert.Equal(
            EnglishSurround,
            PreferenceResolutionPolicy.SelectTrack(
                noSpanish,
                new TrackSelection("spa", 6, null, PreferExternal: false),
                MediaTrackKind.Audio));
        Assert.Null(
            PreferenceResolutionPolicy.SelectTrack(
                [],
                new TrackSelection("spa", 6, null, PreferExternal: false),
                MediaTrackKind.Audio));
    }

    [Fact]
    public void An_external_subtitle_is_preferred_only_when_the_preference_asks_for_it()
    {
        var tracks = new[] { SpanishSubtitle, ExternalSpanishSubtitle };

        Assert.Equal(
            ExternalSpanishSubtitle,
            PreferenceResolutionPolicy.SelectTrack(
                tracks,
                new TrackSelection("spa", null, null, PreferExternal: true),
                MediaTrackKind.Subtitle));
        Assert.Equal(
            SpanishSubtitle,
            PreferenceResolutionPolicy.SelectTrack(
                tracks,
                new TrackSelection("spa", null, null, PreferExternal: false),
                MediaTrackKind.Subtitle));
    }

    [Fact]
    public void Selection_never_crosses_the_track_kind()
    {
        var mixed = new[] { SpanishSurround, SpanishSubtitle };

        Assert.Equal(
            SpanishSubtitle,
            PreferenceResolutionPolicy.SelectTrack(
                mixed,
                new TrackSelection("spa", null, null, PreferExternal: false),
                MediaTrackKind.Subtitle));
        Assert.Equal(
            SpanishSurround,
            PreferenceResolutionPolicy.SelectTrack(
                mixed,
                new TrackSelection("spa", null, null, PreferExternal: false),
                MediaTrackKind.Audio));
    }

    [Fact]
    public void The_subtitle_style_stays_inside_its_accessible_range()
    {
        var clamped = SubtitleStyle.Create(
            fontSizePercent: 900,
            fontFamily: "  ",
            foregroundHex: "#FFFFFF",
            backgroundHex: "#000000",
            backgroundOpacity: 4,
            outlineThickness: -3);

        Assert.Equal(SubtitleStyle.MaximumFontSizePercent, clamped.FontSizePercent);
        Assert.Equal(SubtitleStyle.EngineDefault.FontFamily, clamped.FontFamily);
        Assert.Equal(1.0, clamped.BackgroundOpacity);
        Assert.Equal(0.0, clamped.OutlineThickness);

        var small = SubtitleStyle.Create(10, "Segoe UI", "#FFFFFF", "#000000", 0.5, 2);
        Assert.Equal(SubtitleStyle.MinimumFontSizePercent, small.FontSizePercent);
        Assert.Throws<ArgumentException>(() => SubtitleStyle.Create(100, "Segoe UI", "red", "#000000", 1, 1));
    }

    [Fact]
    public void Matching_tolerates_tracks_that_declare_neither_language_nor_channels()
    {
        var anonymous = new MediaTrack("9", MediaTrackKind.Audio, Language: null, Channels: null, Codec: "aac");
        var tracks = new[] { anonymous, SpanishStereo };

        Assert.Equal(
            anonymous,
            PreferenceResolutionPolicy.SelectTrack(
                [anonymous],
                new TrackSelection("spa", 2, null, PreferExternal: false),
                MediaTrackKind.Audio));
        Assert.Equal(
            SpanishStereo,
            PreferenceResolutionPolicy.SelectTrack(
                tracks,
                new TrackSelection(null, 2, null, PreferExternal: false),
                MediaTrackKind.Audio));
        Assert.Equal(
            anonymous,
            PreferenceResolutionPolicy.SelectTrack(
                tracks,
                new TrackSelection(null, null, null, PreferExternal: false),
                MediaTrackKind.Audio));
        Assert.Throws<ArgumentNullException>(() => PreferenceResolutionPolicy.SelectTrack(
            null!,
            new TrackSelection(null, null, null, false),
            MediaTrackKind.Audio));
        Assert.Throws<ArgumentNullException>(() => PreferenceResolutionPolicy.SelectTrack(
            [],
            null!,
            MediaTrackKind.Audio));
    }

    [Fact]
    public void An_external_preference_still_answers_when_no_external_track_exists()
    {
        var chosen = PreferenceResolutionPolicy.SelectTrack(
            [SpanishSubtitle],
            new TrackSelection("spa", null, null, PreferExternal: true),
            MediaTrackKind.Subtitle);

        Assert.Equal(SpanishSubtitle, chosen);
    }

    [Fact]
    public void The_style_accepts_both_hexadecimal_lengths_and_rejects_anything_else()
    {
        var opaque = SubtitleStyle.Create(100, "Arial", "#ffffff", "#000000", 1, 1);
        var withAlpha = SubtitleStyle.Create(100, "Arial", "#80FFFFFF", "#FF000000", 1, 1);

        Assert.Equal("#FFFFFF", opaque.ForegroundHex);
        Assert.Equal("#80FFFFFF", withAlpha.ForegroundHex);
        Assert.Throws<ArgumentException>(() => SubtitleStyle.Create(100, "Arial", "#FFFF", "#000000", 1, 1));
        Assert.Throws<ArgumentException>(() => SubtitleStyle.Create(100, "Arial", "#GGGGGG", "#000000", 1, 1));
        Assert.Throws<ArgumentException>(() => SubtitleStyle.Create(100, "Arial", "FFFFFF", "#000000", 1, 1));
        Assert.Throws<ArgumentException>(() => SubtitleStyle.Create(100, "Arial", "#FFFFFF", "   ", 1, 1));
    }

    [Fact]
    public void Scope_keys_are_stable_and_distinguish_the_three_scopes()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal("global", PlaybackPreference.GlobalKey);
        Assert.Equal("series:22222222-2222-2222-2222-222222222222", PlaybackPreference.SeriesKey(id));
        Assert.Equal("file:22222222-2222-2222-2222-222222222222", PlaybackPreference.FileKey(id));
    }

    [Fact]
    public void Scopes_are_ordered_from_the_most_specific_to_the_least_specific()
    {
        Assert.Equal(
            [PreferenceScope.File, PreferenceScope.Series, PreferenceScope.Global],
            PreferenceResolutionPolicy.PrecedenceOrder);
    }

    private static PlaybackPreference Preference(
        PreferenceScope scope,
        string key,
        string? audioLanguage = null,
        string? subtitleLanguage = null,
        double? speed = null) =>
        new()
        {
            Scope = scope,
            ScopeKey = key,
            Audio = audioLanguage is null ? null : new TrackSelection(audioLanguage, null, null, false),
            Subtitle = subtitleLanguage is null ? null : new TrackSelection(subtitleLanguage, null, null, false),
            SubtitlesEnabled = subtitleLanguage is not null,
            SpeedMultiplier = speed,
        };
}
