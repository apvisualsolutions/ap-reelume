// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

/// <summary>
/// Which titles need a frame for a cover, and from which video (LIB-021). Seeded with SQL straight
/// into the migrated schema, because what is under test is a query across five tables and the rows
/// it must and must not answer are clearer written out than built through five repositories.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TitleFrameSourceRepositoryTests
{
    private const string Film = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string Show = "aaaaaaaa-0000-0000-0000-000000000002";
    private const string Special = "bbbbbbbb-0000-0000-0000-000000000001";
    private const string FirstEpisode = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string SecondEpisode = "bbbbbbbb-0000-0000-0000-000000000003";
    private const string Loose = "cccccccc-0000-0000-0000-000000000001";

    [Fact]
    public async Task A_film_with_no_cover_answers_with_its_own_video_and_its_state()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ExecuteAsync(MediaFile(Film, @"D:\films\arrival.mkv", size: 1234, available: 1, durationTicks: TimeSpan.FromMinutes(116).Ticks));
        await fixture.ExecuteAsync(Title(Film, kind: 0));

        var source = Assert.Single(await fixture.Sources.ListWithoutCoverAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Film, source.TitleId.Value.ToString("D"));
        Assert.Equal(@"D:\films\arrival.mkv", source.VideoPath);
        Assert.Equal(TimeSpan.FromMinutes(116), source.Duration);
        Assert.Equal(1234, source.Stamp.Length);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero), source.Stamp.ModifiedUtc);
    }

    [Theory]
    [InlineData("poster_path", "'/provider.jpg'")]
    [InlineData("personal_cover", "'" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png'")]
    public async Task A_title_with_a_cover_of_either_kind_needs_no_frame(string column, string value)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ExecuteAsync(MediaFile(Film, @"D:\films\arrival.mkv", size: 1, available: 1));
        await fixture.ExecuteAsync(Title(Film, kind: 0));
        await fixture.ExecuteAsync(
            $"INSERT INTO catalog_metadata (title_id, title, genres, locked_fields, revision, {column}) "
            + $"VALUES ('{Film}', 'Arrival', '', '', 1, {value});");

        Assert.Empty(await fixture.Sources.ListWithoutCoverAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_series_answers_with_its_first_episode_outside_the_specials()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ExecuteAsync(MediaFile(Special, @"D:\shows\s00e01.mkv", size: 1, available: 1));
        await fixture.ExecuteAsync(MediaFile(FirstEpisode, @"D:\shows\s01e01.mkv", size: 2, available: 1));
        await fixture.ExecuteAsync(MediaFile(SecondEpisode, @"D:\shows\s01e02.mkv", size: 3, available: 1));
        await fixture.ExecuteAsync(Title(Show, kind: 1));
        await fixture.ExecuteAsync($"INSERT INTO seasons VALUES ('{Show}', 0, 'Especiales'), ('{Show}', 1, 'Temporada 1');");
        await fixture.ExecuteAsync(Episode(Special, season: 0, number: 1));
        await fixture.ExecuteAsync(Episode(SecondEpisode, season: 1, number: 2));
        await fixture.ExecuteAsync(Episode(FirstEpisode, season: 1, number: 1));

        var source = Assert.Single(await fixture.Sources.ListWithoutCoverAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Show, source.TitleId.Value.ToString("D"));
        Assert.Equal(@"D:\shows\s01e01.mkv", source.VideoPath);
    }

    [Fact]
    public async Task A_series_whose_first_episode_is_on_a_missing_disk_answers_with_the_next()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ExecuteAsync(MediaFile(FirstEpisode, @"D:\shows\s01e01.mkv", size: 2, available: 0));
        await fixture.ExecuteAsync(MediaFile(SecondEpisode, @"D:\shows\s01e02.mkv", size: 3, available: 1));
        await fixture.ExecuteAsync(Title(Show, kind: 1));
        await fixture.ExecuteAsync($"INSERT INTO seasons VALUES ('{Show}', 1, 'Temporada 1');");
        await fixture.ExecuteAsync(Episode(FirstEpisode, season: 1, number: 1));
        await fixture.ExecuteAsync(Episode(SecondEpisode, season: 1, number: 2));

        var source = Assert.Single(await fixture.Sources.ListWithoutCoverAsync(TestContext.Current.CancellationToken));

        Assert.Equal(@"D:\shows\s01e02.mkv", source.VideoPath);
    }

    [Fact]
    public async Task An_unidentified_file_answers_and_one_already_filed_as_an_episode_does_not()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ExecuteAsync(MediaFile(Loose, @"D:\loose\clip.mp4", size: 5, available: 1));
        await fixture.ExecuteAsync(ScannedTitle(Loose));
        await fixture.ExecuteAsync(MediaFile(FirstEpisode, @"D:\shows\s01e01.mkv", size: 2, available: 1));
        await fixture.ExecuteAsync(ScannedTitle(FirstEpisode));
        await fixture.ExecuteAsync(Title(Show, kind: 1));
        await fixture.ExecuteAsync($"INSERT INTO seasons VALUES ('{Show}', 1, 'Temporada 1');");
        await fixture.ExecuteAsync(Episode(FirstEpisode, season: 1, number: 1));

        var sources = await fixture.Sources.ListWithoutCoverAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [@"D:\loose\clip.mp4", @"D:\shows\s01e01.mkv"],
            sources.Select(source => source.VideoPath).Order(StringComparer.Ordinal));
        Assert.Single(sources, source => source.VideoPath == @"D:\shows\s01e01.mkv");
    }

    [Fact]
    public async Task A_film_on_a_missing_disk_is_not_asked_for()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ExecuteAsync(MediaFile(Film, @"E:\away\arrival.mkv", size: 1, available: 0));
        await fixture.ExecuteAsync(Title(Film, kind: 0));

        Assert.Empty(await fixture.Sources.ListWithoutCoverAsync(TestContext.Current.CancellationToken));
    }

    private static string MediaFile(string id, string path, long size, int available, long? durationTicks = null) =>
        "INSERT INTO media_files (id, library_root_id, normalized_path, size_bytes, last_write_utc, duration_ticks, "
        + "container, video_codecs, audio_codecs, is_available) VALUES "
        + $"('{id}', 'root', '{path}', {size}, '2026-09-01T10:00:00.0000000+00:00', "
        + $"{(durationTicks is { } ticks ? ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NULL")}, "
        + $"'mkv', '', '', {available});";

    private static string Title(string id, int kind) =>
        "INSERT INTO titles (id, kind, primary_title, sort_title, added_utc, has_progress, is_personal, is_available) "
        + $"VALUES ('{id}', {kind}, 'T', 'T', '2026-09-01T10:00:00.0000000+00:00', 0, 0, 1);";

    private static string Episode(string id, int season, int number) =>
        "INSERT INTO episodes (id, show_id, season_number, episode_number, title, sort_order, is_available) "
        + $"VALUES ('{id}', '{Show}', {season}, {number}, 'E', {(season * 100) + number}, 1); "
        + $"INSERT INTO episode_media (episode_id, media_file_id) VALUES ('{id}', '{id}');";

    private static string ScannedTitle(string id) =>
        "INSERT INTO scanned_titles (media_file_id, display_title, sort_title, added_utc) "
        + $"VALUES ('{id}', 'S', 'S', '2026-09-01T10:00:00.0000000+00:00');";

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DatabaseTestDirectory _directory;
        private readonly SqliteConnectionFactory _factory;

        private Fixture(DatabaseTestDirectory directory, SqliteConnectionFactory factory)
        {
            _directory = directory;
            _factory = factory;
            Sources = new TitleFrameSourceRepository(factory);
        }

        public TitleFrameSourceRepository Sources { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var directory = new DatabaseTestDirectory();
            var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, CancellationToken.None);
            return new Fixture(directory, factory);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = await _factory.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _directory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
