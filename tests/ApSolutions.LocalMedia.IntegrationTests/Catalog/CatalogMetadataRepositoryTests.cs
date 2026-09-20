// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Catalog;

/// <summary>
/// What a person edited about a title, and what they locked so a refresh cannot take it back.
/// <para>
/// The revision is the whole point: two windows editing the same film must not both win, and the one
/// that loses has to be told rather than silently overwritten.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class CatalogMetadataRepositoryTests
{
    private static readonly TitleId Title = new(new Guid("aaaaaaaa-0000-0000-0000-000000000001"));

    [Fact]
    public async Task A_title_nobody_edited_has_nothing_stored()
    {
        await using var fixture = await MetadataFixture.CreateAsync();

        Assert.Null(await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Everything_written_comes_back_including_the_locks()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var catalog = new CatalogMetadata(
            Title,
            new EditableMetadata(
                "Título",
                "Original Title",
                "Un resumen.",
                2016,
                ["Ciencia ficción", "Drama"],
                "/poster.jpg",
                "/backdrop.jpg",
                "dQw4w9WgXcQ",
                new HashSet<MetadataField> { MetadataField.Title, MetadataField.Genres }),
            Revision: 1);

        var written = await fixture.Repository.TrySaveAsync(
            catalog,
            expectedRevision: 0,
            TestContext.Current.CancellationToken);
        Assert.Equal(MetadataWriteOutcome.Applied, written.Outcome);

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("Título", stored!.Metadata.Title);
        Assert.Equal("Original Title", stored.Metadata.OriginalTitle);
        Assert.Equal("Un resumen.", stored.Metadata.Overview);
        Assert.Equal(2016, stored.Metadata.ReleaseYear);
        Assert.Equal(["Ciencia ficción", "Drama"], stored.Metadata.Genres);
        Assert.Equal("/poster.jpg", stored.Metadata.PosterPath);
        Assert.Equal("/backdrop.jpg", stored.Metadata.BackdropPath);
        Assert.Equal("dQw4w9WgXcQ", stored.Metadata.TrailerKey);
        Assert.Equal(1, stored.Revision);
        Assert.Contains(MetadataField.Title, stored.Metadata.LockedFields);
        Assert.Contains(MetadataField.Genres, stored.Metadata.LockedFields);
        Assert.DoesNotContain(MetadataField.Overview, stored.Metadata.LockedFields);
    }

    [Fact]
    public async Task A_title_with_nothing_but_a_name_comes_back_with_nothing_but_a_name()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var catalog = new CatalogMetadata(
            Title,
            new EditableMetadata("Sólo el título", null, null, null, [], null, null, null, new HashSet<MetadataField>()),
            Revision: 1);

        _ = await fixture.Repository.TrySaveAsync(catalog, 0, TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Null(stored!.Metadata.OriginalTitle);
        Assert.Null(stored.Metadata.Overview);
        Assert.Null(stored.Metadata.ReleaseYear);
        Assert.Null(stored.Metadata.PosterPath);
        Assert.Null(stored.Metadata.BackdropPath);
        Assert.Null(stored.Metadata.TrailerKey);
        Assert.Empty(stored.Metadata.Genres);
        Assert.Empty(stored.Metadata.LockedFields);
    }

    [Fact]
    public async Task A_second_edit_from_a_stale_copy_is_refused_and_told_what_is_stored()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var first = Catalog("Primero", revision: 1);
        _ = await fixture.Repository.TrySaveAsync(first, 0, TestContext.Current.CancellationToken);

        var conflict = await fixture.Repository.TrySaveAsync(
            Catalog("Segundo", revision: 1),
            expectedRevision: 0,
            TestContext.Current.CancellationToken);

        Assert.Equal(MetadataWriteOutcome.Conflict, conflict.Outcome);
        Assert.NotNull(conflict.Catalog);
        Assert.Equal("Primero", conflict.Catalog!.Metadata.Title);
    }

    [Fact]
    public async Task An_edit_that_knows_the_current_revision_replaces_what_was_there()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        _ = await fixture.Repository.TrySaveAsync(Catalog("Primero", 1), 0, TestContext.Current.CancellationToken);

        var applied = await fixture.Repository.TrySaveAsync(
            Catalog("Segundo", 2),
            expectedRevision: 1,
            TestContext.Current.CancellationToken);

        Assert.Equal(MetadataWriteOutcome.Applied, applied.Outcome);
        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.Equal("Segundo", stored!.Metadata.Title);
        Assert.Equal(2, stored.Revision);
    }

    /// <summary>
    /// Locks are stored by name. A field that no longer exists is dropped rather than reinterpreted as
    /// whatever now occupies its position.
    /// </summary>
    [Fact]
    public async Task A_lock_whose_field_no_longer_exists_is_dropped_rather_than_guessed()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        _ = await fixture.Repository.TrySaveAsync(Catalog("Título", 1), 0, TestContext.Current.CancellationToken);
        await using (var connection = await fixture.Factory.OpenAsync(TestContext.Current.CancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE catalog_metadata SET locked_fields = 'SomethingRemoved';";
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Empty(stored!.Metadata.LockedFields);
    }

    /// <summary>
    /// LIB-021: the picked cover has its own column, so writing the provider's poster again — which
    /// is what every refresh does — leaves it where it was.
    /// </summary>
    [Fact]
    public async Task A_hand_picked_cover_survives_a_new_provider_poster()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var chosen = new string('a', 64) + ".png";
        var first = Catalog("Título", 1) with
        {
            Metadata = Catalog("Título", 1).Metadata with { PosterPath = "/a.jpg", PersonalCover = chosen },
        };
        _ = await fixture.Repository.TrySaveAsync(first, 0, TestContext.Current.CancellationToken);
        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        _ = await fixture.Repository.TrySaveAsync(
            stored! with { Metadata = stored.Metadata with { PosterPath = "/b.jpg" } },
            stored.Revision,
            TestContext.Current.CancellationToken);
        var again = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        Assert.Equal(chosen, again!.Metadata.PersonalCover);
        Assert.Equal("/b.jpg", again.Metadata.PosterPath);
    }

    /// <summary>
    /// LIB-021: the order one title overrides the general one with makes the round trip, and a title
    /// that never set one reads back as none rather than as an order of its own.
    /// </summary>
    /// <remarks>
    /// It writes twice on purpose. A test that only stores once and reads it back stays green with
    /// the upsert's assignment removed — measured on the column next door by <c>gate-auditor</c> on
    /// 2026-09-18, where only the five-minute walk noticed.
    /// </remarks>
    [Fact]
    public async Task A_titles_own_cover_order_is_stored_and_can_be_changed_and_removed()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var first = Catalog("Título", 1) with
        {
            Metadata = Catalog("Título", 1).Metadata with { CoverOrder = "Frame,Personal,Provider" },
        };
        _ = await fixture.Repository.TrySaveAsync(first, 0, TestContext.Current.CancellationToken);
        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.Equal("Frame,Personal,Provider", stored!.Metadata.CoverOrder);

        _ = await fixture.Repository.TrySaveAsync(
            stored with { Metadata = stored.Metadata with { CoverOrder = "Provider,Personal,Frame" } },
            stored.Revision,
            TestContext.Current.CancellationToken);
        var changed = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.Equal("Provider,Personal,Frame", changed!.Metadata.CoverOrder);

        _ = await fixture.Repository.TrySaveAsync(
            changed with { Metadata = changed.Metadata with { CoverOrder = null } },
            changed.Revision,
            TestContext.Current.CancellationToken);
        var cleared = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.Null(cleared!.Metadata.CoverOrder);
    }

    /// <summary>
    /// A title stored before the column existed follows the general order, which is what a null in
    /// that column means — and not an order of three origins nobody chose.
    /// </summary>
    [Fact]
    public async Task A_title_that_never_set_an_order_reads_back_as_none()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        _ = await fixture.Repository.TrySaveAsync(Catalog("Título", 1), 0, TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        Assert.Null(stored!.Metadata.CoverOrder);
    }

    /// <summary>
    /// Picking another cover replaces the first one. The test above only shows the field stays; with
    /// the upsert's personal_cover assignment removed it stayed green, and only the five-minute walk
    /// noticed (gate-auditor, 2026-09-18).
    /// </summary>
    [Fact]
    public async Task Picking_another_cover_replaces_the_first()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var first = Catalog("Título", 1);
        _ = await fixture.Repository.TrySaveAsync(
            first with { Metadata = first.Metadata with { PersonalCover = new string('a', 64) + ".png" } },
            0,
            TestContext.Current.CancellationToken);
        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        _ = await fixture.Repository.TrySaveAsync(
            stored! with { Metadata = stored.Metadata with { PersonalCover = new string('b', 64) + ".jpg" } },
            stored.Revision,
            TestContext.Current.CancellationToken);
        var again = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        Assert.Equal(new string('b', 64) + ".jpg", again!.Metadata.PersonalCover);
    }

    /// <summary>
    /// Before LIB-021 a picked cover was stored in <c>poster_path</c> as an absolute path. Such a row
    /// is read as a picked cover and the provider field comes back empty, so the next save writes the
    /// two apart — the move happens on read, with the one rule that knows what a cover name is.
    /// </summary>
    [Fact]
    public async Task A_cover_stored_the_old_way_is_read_as_hand_picked()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var chosen = new string('b', 64) + ".jpg";
        _ = await fixture.Repository.TrySaveAsync(Catalog("Título", 1), 0, TestContext.Current.CancellationToken);
        await using (var connection = await fixture.Factory.OpenAsync(TestContext.Current.CancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE catalog_metadata SET poster_path = $old, personal_cover = NULL;";
            _ = command.Parameters.AddWithValue("$old", Path.Combine("C:", "anywhere", "personal-artwork", chosen));
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        Assert.Equal(chosen, stored!.Metadata.PersonalCover);
        Assert.Null(stored.Metadata.PosterPath);
    }

    /// <summary>The control beside the move: a provider address in the old field stays a provider address.</summary>
    [Fact]
    public async Task A_provider_poster_is_never_moved_to_the_hand_picked_field()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        var catalog = Catalog("Título", 1);
        _ = await fixture.Repository.TrySaveAsync(
            catalog with { Metadata = catalog.Metadata with { PosterPath = "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg" } },
            0,
            TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);

        Assert.Null(stored!.Metadata.PersonalCover);
        Assert.Equal("/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg", stored.Metadata.PosterPath);
    }

    [Fact]
    public async Task Saving_nothing_is_refused_before_it_reaches_the_database()
    {
        await using var fixture = await MetadataFixture.CreateAsync();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Repository.TrySaveAsync(
            null!,
            0,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_repository_without_a_connection_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new CatalogMetadataRepository(null!));
    }

    private static CatalogMetadata Catalog(string title, int revision) => new(
        Title,
        new EditableMetadata(title, null, null, null, [], null, null, null, new HashSet<MetadataField>()),
        revision);

    /// <summary>
    /// Two windows editing the same title from the same copy: the first wins and the second is told
    /// its copy is stale. Measured against the real repository on purpose — the in-memory doubles
    /// this suite's unit counterparts use raise the revision themselves, so they were the only
    /// reason the revision looked like it moved at all.
    /// </summary>
    [Fact]
    public async Task Two_windows_editing_from_the_same_copy_cannot_both_win()
    {
        await using var fixture = await MetadataFixture.CreateAsync();
        _ = await fixture.Repository.TrySaveAsync(
            new CatalogMetadata(
                Title,
                new EditableMetadata("Base", null, null, null, [], null, null, null, new HashSet<MetadataField>()),
                Revision: 1),
            expectedRevision: 0,
            TestContext.Current.CancellationToken);

        var useCase = new UpdateMetadata(fixture.Repository);
        var first = await useCase.ExecuteAsync(
            new UpdateMetadataCommand(
                Title,
                new MetadataFieldChanges(Title: "Primera ventana"),
                new HashSet<MetadataField>(),
                ExpectedRevision: 1),
            TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(
            new UpdateMetadataCommand(
                Title,
                new MetadataFieldChanges(Title: "Segunda ventana, copia vieja"),
                new HashSet<MetadataField>(),
                ExpectedRevision: 1),
            TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.Equal(MetadataWriteOutcome.Applied, first.Outcome);
        Assert.Equal(2, stored!.Revision);
        Assert.Equal(MetadataWriteOutcome.Conflict, second.Outcome);
        Assert.Equal("Primera ventana", stored.Metadata.Title);
    }

    private sealed class MetadataFixture : IAsyncDisposable
    {
        private readonly DatabaseTestDirectory _directory;

        private MetadataFixture(
            DatabaseTestDirectory directory,
            SqliteConnectionFactory factory,
            CatalogMetadataRepository repository)
        {
            _directory = directory;
            Factory = factory;
            Repository = repository;
        }

        public SqliteConnectionFactory Factory { get; }

        public CatalogMetadataRepository Repository { get; }

        public static async Task<MetadataFixture> CreateAsync()
        {
            var directory = new DatabaseTestDirectory();
            var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, CancellationToken.None);

            return new MetadataFixture(directory, factory, new CatalogMetadataRepository(factory));
        }

        public ValueTask DisposeAsync()
        {
            _directory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
