// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Track and subtitle behaviour against real media. Two samples announce the same two languages in
/// the opposite order, which is exactly the case an index-based selection gets wrong.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class TrackAndSubtitleTests
{
    private const string SpanishFirst = "mkv-dual-audio-spanish-first";
    private const string EnglishFirst = "mkv-dual-audio-english-first";

    [Theory]
    [InlineData(SpanishFirst)]
    [InlineData(EnglishFirst)]
    public async Task The_same_stored_attributes_choose_the_same_language_whatever_the_track_order(string id)
    {
        var sample = MediaManifest.Require(id);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);

        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var audio = snapshot.Tracks.Where(track => track.Kind == MediaTrackKind.Audio).ToArray();
        Assert.Equal(2, audio.Length);

        var chosen = PreferenceResolutionPolicy.SelectTrack(
            snapshot.Tracks,
            new TrackSelection("spa", 6, null, PreferExternal: false),
            MediaTrackKind.Audio);

        Assert.NotNull(chosen);
        Assert.Equal("spa", chosen!.Language);
        Assert.Equal(6, chosen.Channels);
        await engine.SelectTrackAsync(MediaTrackKind.Audio, chosen.Id, TestContext.Current.CancellationToken);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The engine with nothing open answers about its tracks rather than inventing an answer.
    /// </summary>
    /// <remarks>
    /// A snapshot names which audio and which subtitle are <b>in force</b>, and there is a state in
    /// which neither is: before a media is open. That state is what a session's panel is built in,
    /// so the answer for it has to be «none» rather than a number nobody can select.
    /// </remarks>
    [Fact]
    public async Task An_engine_with_no_media_reports_no_track_in_force()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);

        var beforeInitialise = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Null(beforeInitialise.ActiveAudioTrackId);
        Assert.Null(beforeInitialise.ActiveSubtitleTrackId);
        Assert.Empty(beforeInitialise.Tracks);

        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        var afterInitialise = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.Null(afterInitialise.ActiveAudioTrackId);
        Assert.Null(afterInitialise.ActiveSubtitleTrackId);
    }

    [Fact]
    public async Task An_internal_subtitle_track_can_be_selected_and_switched_off()
    {
        var sample = MediaManifest.Require(SpanishFirst);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);

        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var subtitle = snapshot.Tracks.SingleOrDefault(track => track.Kind == MediaTrackKind.Subtitle);

        Assert.NotNull(subtitle);
        Assert.False(subtitle!.IsExternal);
        await engine.SelectTrackAsync(MediaTrackKind.Subtitle, subtitle.Id, TestContext.Current.CancellationToken);
        await engine.SelectTrackAsync(MediaTrackKind.Subtitle, null, TestContext.Current.CancellationToken);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(".srt", "UTF-8")]
    [InlineData(".ass", "UTF-8")]
    [InlineData(".vtt", "UTF-16LE")]
    public async Task External_subtitles_load_from_beside_the_media_in_both_encodings(
        string extension,
        string expectedEncoding)
    {
        var sample = MediaManifest.Require(SpanishFirst);
        var media = await CodecMatrixTests.RequireSampleAsync(sample);
        var root = Path.Combine(MediaToolchain.OutputRoot, "T20");
        var subtitle = WriteSubtitle(media, extension, expectedEncoding.StartsWith("UTF-16", StringComparison.Ordinal));

        var discovered = ExternalSubtitleDiscovery.Discover(media, root);

        Assert.Contains(discovered, item => item.Path.Equals(subtitle, StringComparison.OrdinalIgnoreCase));
        var entry = discovered.Single(item => item.Path.Equals(subtitle, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedEncoding, entry.Encoding);
        Assert.Equal(extension.TrimStart('.').ToUpperInvariant(), entry.Format);
        Assert.Contains("Subtitulo", ExternalSubtitleDiscovery.ReadText(subtitle), StringComparison.Ordinal);

        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), media),
            TestContext.Current.CancellationToken);

        var track = await engine.AddExternalSubtitleAsync(subtitle, TestContext.Current.CancellationToken);

        Assert.True(track.IsExternal);
        Assert.Equal(MediaTrackKind.Subtitle, track.Kind);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_subtitle_outside_the_library_root_is_never_discovered()
    {
        var sample = MediaManifest.Require(SpanishFirst);
        var media = await CodecMatrixTests.RequireSampleAsync(sample);
        var outsideRoot = Path.Combine(MediaToolchain.OutputRoot, "T20-outside");
        Directory.CreateDirectory(outsideRoot);
        var intruder = Path.Combine(
            outsideRoot,
            Path.GetFileNameWithoutExtension(media) + ".srt");
        await File.WriteAllTextAsync(
            intruder,
            "1\n00:00:00,000 --> 00:00:01,000\nIntruso\n",
            TestContext.Current.CancellationToken);

        var confined = ExternalSubtitleDiscovery.Discover(media, Path.Combine(MediaToolchain.OutputRoot, "T20"));
        var refused = ExternalSubtitleDiscovery.Discover(media, outsideRoot);

        Assert.DoesNotContain(confined, item => item.Path.Equals(intruder, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(refused);
    }

    [Fact]
    public async Task A_series_preference_is_reapplied_to_the_next_episode_without_touching_it()
    {
        var repository = new InMemoryPreferenceRepository();
        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.Series,
                ScopeKey = "series:test",
                Audio = new TrackSelection("spa", 6, null, PreferExternal: false),
                SubtitlesEnabled = false,
            },
            TestContext.Current.CancellationToken);
        var apply = new ApplyPlaybackPreferences(repository);

        foreach (var id in new[] { SpanishFirst, EnglishFirst })
        {
            var sample = MediaManifest.Require(id);
            var path = await CodecMatrixTests.RequireSampleAsync(sample);
            await using var factory = LibVlcFactory.CreateHeadless();
            await using var engine = new LibVlcMediaPlayerEngine(factory);
            await engine.InitializeAsync(TestContext.Current.CancellationToken);
            await engine.OpenAsync(
                new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
                TestContext.Current.CancellationToken);

            var applied = await apply.ApplyAsync(
                engine,
                new PlaybackPreferenceContext($"file:{id}", "series:test", []),
                TestContext.Current.CancellationToken);

            Assert.NotNull(applied.Audio);
            Assert.Equal("spa", applied.Audio!.Language);
            Assert.Equal(6, applied.Audio.Channels);
            Assert.Null(applied.Subtitle);
            Assert.Equal(PreferenceScope.Series, applied.Resolved.AudioSource);
            await engine.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static string WriteSubtitle(string mediaPath, string extension, bool utf16)
    {
        var destination = Path.GetFullPath(Path.ChangeExtension(mediaPath, extension));
        var body = extension switch
        {
            ".ass" => "[Script Info]\nScriptType: v4.00+\n\n[Events]\nFormat: Start, End, Text\n"
                + "Dialogue: 0:00:00.50,0:00:02.50,Subtitulo externo\n",
            ".vtt" => "WEBVTT\n\n00:00:00.500 --> 00:00:02.500\nSubtitulo externo\n",
            _ => "1\n00:00:00,500 --> 00:00:02,500\nSubtitulo externo\n",
        };

        Encoding encoding = utf16
            ? new UnicodeEncoding(bigEndian: false, byteOrderMark: true)
            : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(destination, body, encoding);
        return destination;
    }

    private sealed class InMemoryPreferenceRepository : IPlaybackPreferenceRepository
    {
        private readonly Dictionary<(PreferenceScope, string), PlaybackPreference> _stored = [];

        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_stored.TryGetValue((scope, scopeKey), out var value) ? value : null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(preference);
            _stored[(preference.Scope, preference.ScopeKey)] = preference;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            _ = _stored.Remove((scope, scopeKey));
            return Task.CompletedTask;
        }
    }
}
