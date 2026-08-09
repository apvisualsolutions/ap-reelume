using ApSolutions.LocalMedia.Domain.Continuity;
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
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
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
    }

    [Fact]
    public async Task An_unset_field_stays_unset_so_the_next_scope_still_answers()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
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
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
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
