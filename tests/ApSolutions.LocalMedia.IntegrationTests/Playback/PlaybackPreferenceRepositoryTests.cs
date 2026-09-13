// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// Scoped preferences must survive a restart and must never turn an unset field into a stored
/// default, because that would silently override the next scope.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlaybackPreferenceRepositoryTests
{
    [Fact]
    public async Task A_preference_round_trips_through_a_new_repository_instance()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PlaybackPreferenceRepository(factory);
        var preference = new PlaybackPreference
        {
            Scope = PreferenceScope.Series,
            ScopeKey = PlaybackPreference.SeriesKey(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Audio = new TrackSelection("spa", 6, "eac3", PreferExternal: false),
            Subtitle = new TrackSelection("spa", null, "subrip", PreferExternal: true),
            SubtitlesEnabled = true,
            SpeedMultiplier = 1.5,
            VolumePercent = 140,
            AudioOutputDeviceId = "stable-endpoint-id",
            SubtitleStyle = SubtitleStyle.Create(150, "Verdana", "#FFFFFF00", "#80101010", 0.6, 2.5),
            Picture = new PictureAdjustment(0.35, 1.2, 1.6),
        };

        await repository.SaveAsync(preference, TestContext.Current.CancellationToken);
        var restored = await new PlaybackPreferenceRepository(factory).GetAsync(
            preference.Scope,
            preference.ScopeKey,
            TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.Equal(preference.Audio, restored!.Audio);
        Assert.Equal(preference.Subtitle, restored.Subtitle);
        Assert.True(restored.SubtitlesEnabled);
        Assert.Equal(1.5, restored.SpeedMultiplier);
        Assert.Equal(140, restored.VolumePercent);
        Assert.Equal("stable-endpoint-id", restored.AudioOutputDeviceId);
        Assert.Equal(preference.SubtitleStyle, restored.SubtitleStyle);
        Assert.Equal(new PictureAdjustment(0.35, 1.2, 1.6), restored.Picture);
    }

    /// <summary>
    /// Adjusting a second time overwrites the first, which is the branch production almost always
    /// takes: a row usually exists already because a volume or a track was stored in it, so the
    /// INSERT falls through to the conflict clause and never runs as an insert at all.
    /// </summary>
    [Fact]
    public async Task Adjusting_the_picture_twice_keeps_the_second_setting()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PlaybackPreferenceRepository(factory);
        var key = PlaybackPreference.FileKey(Guid.Empty);

        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.File,
                ScopeKey = key,
                VolumePercent = 80,
                Picture = new PictureAdjustment(0.1, 1.1, 1.1),
            },
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.File,
                ScopeKey = key,
                VolumePercent = 80,
                Picture = new PictureAdjustment(0.4, 1.3, 1.6),
            },
            TestContext.Current.CancellationToken);

        var stored = await repository.GetAsync(PreferenceScope.File, key, TestContext.Current.CancellationToken);

        Assert.Equal(new PictureAdjustment(0.4, 1.3, 1.6), stored!.Picture);
    }

    /// <summary>
    /// A row with only some of the three columns filled reads back as «nobody stored one» too. The
    /// out-of-range row below exercises the domain's refusal; this one exercises the NULL guard, and
    /// without it the reader throws <see cref="InvalidOperationException"/> — which the refusal's
    /// own catch does not cover, so the film simply would not open.
    /// </summary>
    [Fact]
    public async Task A_half_written_picture_row_reads_back_as_no_adjustment()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var key = PlaybackPreference.FileKey(Guid.Empty);

        await using (var connection = await factory.OpenAsync(TestContext.Current.CancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO playback_preferences (
                    scope, scope_key, picture_brightness, picture_contrast, picture_gamma, updated_at)
                VALUES ($scope, $key, 0.4, NULL, 1.6, '2026-09-12T00:00:00.0000000+00:00');
                """;
            command.Parameters.AddWithValue("$scope", (int)PreferenceScope.File);
            command.Parameters.AddWithValue("$key", key);
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var stored = await new PlaybackPreferenceRepository(factory).GetAsync(
            PreferenceScope.File,
            key,
            TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Null(stored!.Picture);
    }

    /// <summary>
    /// A row whose picture columns cannot make a valid adjustment reads back as «nobody stored one»
    /// instead of throwing. <see cref="PictureAdjustment"/> rejects out-of-range values rather than
    /// clamping them, so without this the only way to reach a hand-edited database would be to open
    /// a film that refuses to open.
    /// </summary>
    [Fact]
    public async Task A_picture_row_outside_the_allowed_range_reads_back_as_no_adjustment()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var key = PlaybackPreference.FileKey(Guid.Empty);

        await using (var connection = await factory.OpenAsync(TestContext.Current.CancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO playback_preferences (
                    scope, scope_key, picture_brightness, picture_contrast, picture_gamma, updated_at)
                VALUES ($scope, $key, 9.0, 1.0, 1.0, '2026-09-12T00:00:00.0000000+00:00');
                """;
            command.Parameters.AddWithValue("$scope", (int)PreferenceScope.File);
            command.Parameters.AddWithValue("$key", key);
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var stored = await new PlaybackPreferenceRepository(factory).GetAsync(
            PreferenceScope.File,
            key,
            TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Null(stored!.Picture);
        Assert.Equal(PictureAdjustment.Neutral, PreferenceResolutionPolicy.Resolve(stored, null, null).Picture);
    }

    [Fact]
    public async Task An_unset_field_stays_unset_so_the_next_scope_still_answers()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PlaybackPreferenceRepository(factory);

        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.Global,
                ScopeKey = PlaybackPreference.GlobalKey,
                Audio = new TrackSelection("eng", null, null, PreferExternal: false),
                SpeedMultiplier = 2.0,
            },
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.File,
                ScopeKey = PlaybackPreference.FileKey(Guid.Empty),
                Audio = new TrackSelection("spa", null, null, PreferExternal: false),
            },
            TestContext.Current.CancellationToken);

        var global = await repository.GetAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            TestContext.Current.CancellationToken);
        var file = await repository.GetAsync(
            PreferenceScope.File,
            PlaybackPreference.FileKey(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.Null(file!.SpeedMultiplier);
        Assert.Null(file.Subtitle);
        Assert.Null(file.SubtitleStyle);
        // Storing the neutral here instead of nothing would look harmless and would mean this file
        // had been adjusted and set back, so no wider scope could ever reach it again.
        Assert.Null(file.Picture);

        var resolved = PreferenceResolutionPolicy.Resolve(file, null, global);
        Assert.Equal("spa", resolved.Audio.Language);
        Assert.Equal(PreferenceScope.File, resolved.AudioSource);
        Assert.Equal(2.0, resolved.SpeedMultiplier);
        Assert.Equal(PreferenceScope.Global, resolved.SpeedSource);
    }

    [Fact]
    public async Task Saving_the_same_scope_twice_updates_it_instead_of_duplicating_it()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new PlaybackPreferenceRepository(factory);
        var key = PlaybackPreference.FileKey(Guid.Empty);

        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.File,
                ScopeKey = key,
                Audio = new TrackSelection("eng", 2, null, PreferExternal: false),
            },
            TestContext.Current.CancellationToken);
        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.File,
                ScopeKey = key,
                Audio = new TrackSelection("spa", 6, null, PreferExternal: false),
            },
            TestContext.Current.CancellationToken);

        var stored = await repository.GetAsync(PreferenceScope.File, key, TestContext.Current.CancellationToken);
        Assert.Equal("spa", stored!.Audio!.Language);
        Assert.Equal(6, stored.Audio.Channels);

        await repository.RemoveAsync(PreferenceScope.File, key, TestContext.Current.CancellationToken);
        Assert.Null(await repository.GetAsync(PreferenceScope.File, key, TestContext.Current.CancellationToken));
    }
}
