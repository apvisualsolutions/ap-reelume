// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
            new EditableMetadata("Sólo el título", null, null, null, [], null, null, new HashSet<MetadataField>()),
            Revision: 1);

        _ = await fixture.Repository.TrySaveAsync(catalog, 0, TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.GetAsync(Title, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Null(stored!.Metadata.OriginalTitle);
        Assert.Null(stored.Metadata.Overview);
        Assert.Null(stored.Metadata.ReleaseYear);
        Assert.Null(stored.Metadata.PosterPath);
        Assert.Null(stored.Metadata.BackdropPath);
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
        new EditableMetadata(title, null, null, null, [], null, null, new HashSet<MetadataField>()),
        revision);

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
            var factory = new SqliteConnectionFactory(directory.DatabasePath);
            using (var runner = new MigrationRunner(factory))
            {
                await runner.MigrateAsync(CancellationToken.None);
            }

            return new MetadataFixture(directory, factory, new CatalogMetadataRepository(factory));
        }

        public ValueTask DisposeAsync()
        {
            _directory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
