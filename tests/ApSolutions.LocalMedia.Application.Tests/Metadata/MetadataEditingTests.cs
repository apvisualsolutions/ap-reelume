// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Tests.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Metadata;

public sealed class MetadataEditingTests
{
    [Fact]
    public async Task Edit_can_lock_and_unlock_each_field_with_optimistic_revision()
    {
        var stored = Catalog(revision: 0, locked: []);
        var repository = new MemoryMetadataRepository(stored);
        var useCase = new UpdateMetadata(repository);
        var allFields = Enum.GetValues<MetadataField>().ToHashSet();

        var locked = await useCase.ExecuteAsync(
            new UpdateMetadataCommand(
                stored.TitleId,
                new MetadataFieldChanges(
                    Title: "Mi llegada",
                    OriginalTitle: "Arrival",
                    Overview: "Mi resumen",
                    ReleaseYear: 2016,
                    Genres: ["Drama"],
                    PosterPath: "/manual-poster.jpg",
                    BackdropPath: "/manual-backdrop.jpg"),
                allFields,
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);
        var unlocked = await useCase.ExecuteAsync(
            new UpdateMetadataCommand(
                stored.TitleId,
                new MetadataFieldChanges(Title: "Mi llegada revisada"),
                new HashSet<MetadataField>(),
                ExpectedRevision: 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(MetadataWriteOutcome.Applied, locked.Outcome);
        Assert.Equal(allFields, locked.Catalog?.Metadata.LockedFields);
        Assert.Equal(1, locked.Catalog?.Revision);
        Assert.Equal(MetadataWriteOutcome.Applied, unlocked.Outcome);
        Assert.Empty(unlocked.Catalog?.Metadata.LockedFields ?? allFields);
        Assert.Equal("Mi llegada revisada", unlocked.Catalog?.Metadata.Title);
        Assert.Equal(2, unlocked.Catalog?.Revision);
    }

    [Fact]
    public async Task Remote_refresh_preserves_locks_and_explicit_provider_restore_clears_them()
    {
        var titleId = new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        var repository = new MemoryMetadataRepository(Catalog(
            revision: 4,
            locked: [MetadataField.Title, MetadataField.PosterPath]) with
        {
            Provider = "tmdb",
            ProviderKey = ProviderKey,
        });
        var refresh = Refresh(repository, RemoteDetails(titleId, "La llegada", "/remote-poster.jpg"));

        var preserved = await refresh.ExecuteAsync(
            new RefreshMetadataCommand(titleId, ExpectedRevision: 4, RestoreProviderFields: false),
            TestContext.Current.CancellationToken);
        var restored = await refresh.ExecuteAsync(
            new RefreshMetadataCommand(titleId, ExpectedRevision: 5, RestoreProviderFields: true),
            TestContext.Current.CancellationToken);

        Assert.Equal("Manual title", preserved.Catalog?.Metadata.Title);
        Assert.Equal("/manual-poster.jpg", preserved.Catalog?.Metadata.PosterPath);
        Assert.Equal("Nuevo resumen remoto", preserved.Catalog?.Metadata.Overview);
        Assert.Equal("La llegada", restored.Catalog?.Metadata.Title);
        Assert.Equal("/remote-poster.jpg", restored.Catalog?.Metadata.PosterPath);
        Assert.Empty(restored.Catalog?.Metadata.LockedFields ?? new HashSet<MetadataField> { MetadataField.Title });
    }

    /// <summary>
    /// The defect ADR-0009 closes, reproduced: restoring the provider's fields clears every lock, and
    /// while the picked cover shared the provider's field that erased it and orphaned its file. Apart,
    /// the provider's poster comes back and the picked cover stays.
    /// </summary>
    [Fact]
    public async Task Restoring_the_provider_fields_keeps_the_hand_picked_cover()
    {
        var titleId = new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        var chosen = new string('c', 64) + ".png";
        var stored = Catalog(revision: 4, locked: [MetadataField.PosterPath]);
        var repository = new MemoryMetadataRepository(stored with
        {
            Provider = "tmdb",
            ProviderKey = ProviderKey,
            Metadata = stored.Metadata with { PersonalCover = chosen },
        });
        var refresh = Refresh(repository, RemoteDetails(titleId, "La llegada", "/remote-poster.jpg"));

        var restored = await refresh.ExecuteAsync(
            new RefreshMetadataCommand(titleId, ExpectedRevision: 4, RestoreProviderFields: true),
            TestContext.Current.CancellationToken);

        Assert.Equal("/remote-poster.jpg", restored.Catalog?.Metadata.PosterPath);
        Assert.Equal(chosen, restored.Catalog?.Metadata.PersonalCover);
    }

    /// <summary>
    /// The first edit of a title that has no row yet creates it: what was typed, and nothing else.
    /// This suite never took that path until 2026-09-18, when <c>UpdateMetadata</c> gained a field and
    /// the coverage preview named the lines only a first edit runs.
    /// </summary>
    [Fact]
    public async Task The_first_edit_of_a_title_with_no_row_creates_it_with_only_what_was_typed()
    {
        var elsewhere = Catalog(revision: 0, locked: []);
        var repository = new MemoryMetadataRepository(elsewhere);
        var fresh = new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000ff"));

        var created = await new UpdateMetadata(repository).ExecuteAsync(
            new UpdateMetadataCommand(
                fresh,
                new MetadataFieldChanges(Title: "Sin fila todavía"),
                new HashSet<MetadataField>(),
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(MetadataWriteOutcome.Applied, created.Outcome);
        var metadata = created.Catalog!.Metadata;
        Assert.Equal(fresh, created.Catalog.TitleId);
        Assert.Equal("Sin fila todavía", metadata.Title);
        Assert.Null(metadata.OriginalTitle);
        Assert.Null(metadata.Overview);
        Assert.Null(metadata.ReleaseYear);
        Assert.Empty(metadata.Genres);
        Assert.Null(metadata.PosterPath);
        Assert.Null(metadata.BackdropPath);
        Assert.Null(metadata.PersonalCover);
        Assert.Empty(metadata.LockedFields);
    }

    /// <summary>
    /// A change that names no title keeps the one stored: every field of the changes is «leave it as
    /// it is» when absent, and the title is no exception. Named by the coverage JSON as the one branch
    /// no test took — every other test happened to pass a title.
    /// </summary>
    [Fact]
    public async Task A_change_that_names_no_title_keeps_the_stored_one()
    {
        var stored = Catalog(revision: 0, locked: []);
        var repository = new MemoryMetadataRepository(stored);

        var saved = await new UpdateMetadata(repository).ExecuteAsync(
            new UpdateMetadataCommand(
                stored.TitleId,
                new MetadataFieldChanges(Overview: "Sólo cambia el resumen"),
                new HashSet<MetadataField>(),
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(stored.Metadata.Title, saved.Catalog?.Metadata.Title);
        Assert.Equal("Sólo cambia el resumen", saved.Catalog?.Metadata.Overview);
    }

    /// <summary>Saving a picked cover writes its own field and leaves the provider's poster alone.</summary>
    [Fact]
    public async Task Saving_a_picked_cover_leaves_the_provider_poster_where_it_was()
    {
        var stored = Catalog(revision: 0, locked: []);
        var repository = new MemoryMetadataRepository(stored);
        var chosen = new string('d', 64) + ".jpg";

        var saved = await new UpdateMetadata(repository).ExecuteAsync(
            new UpdateMetadataCommand(
                stored.TitleId,
                new MetadataFieldChanges(Title: "Mi llegada") { PersonalCover = chosen },
                new HashSet<MetadataField>(),
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(chosen, saved.Catalog?.Metadata.PersonalCover);
        Assert.Equal(stored.Metadata.PosterPath, saved.Catalog?.Metadata.PosterPath);
    }

    /// <summary>
    /// LIB-021: an order set for one title is stored as the whole order, so moving the general one
    /// later cannot change what this title was told to do.
    /// </summary>
    [Fact]
    public async Task Setting_an_order_for_one_title_stores_the_whole_order()
    {
        var stored = Catalog(revision: 0, locked: []);
        var repository = new MemoryMetadataRepository(stored);

        var saved = await new UpdateMetadata(repository).ExecuteAsync(
            new UpdateMetadataCommand(
                stored.TitleId,
                new MetadataFieldChanges(Title: "Mi llegada")
                {
                    CoverOrder = [CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal],
                },
                new HashSet<MetadataField>(),
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal("Frame,Provider,Personal", saved.Catalog?.Metadata.CoverOrder);
    }

    /// <summary>
    /// The second sentinel: an empty list removes the override. Without it there would be no way
    /// back to the general order once one was set, which is the hole the picked cover still has.
    /// </summary>
    [Fact]
    public async Task An_empty_order_removes_the_override_and_a_missing_one_leaves_it_alone()
    {
        var stored = Catalog(revision: 0, locked: []) with
        {
            Metadata = Catalog(revision: 0, locked: []).Metadata with { CoverOrder = "Frame,Personal,Provider" },
        };
        var repository = new MemoryMetadataRepository(stored);

        var untouched = await new UpdateMetadata(repository).ExecuteAsync(
            new UpdateMetadataCommand(
                stored.TitleId,
                new MetadataFieldChanges(Title: "Mi llegada"),
                new HashSet<MetadataField>(),
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);
        Assert.Equal("Frame,Personal,Provider", untouched.Catalog?.Metadata.CoverOrder);

        var cleared = await new UpdateMetadata(repository).ExecuteAsync(
            new UpdateMetadataCommand(
                stored.TitleId,
                new MetadataFieldChanges(Title: "Mi llegada") { CoverOrder = [] },
                new HashSet<MetadataField>(),
                ExpectedRevision: untouched.Catalog!.Revision),
            TestContext.Current.CancellationToken);
        Assert.Null(cleared.Catalog?.Metadata.CoverOrder);
    }

    [Fact]
    public async Task Stale_edit_conflicts_and_three_edit_refresh_restart_cycles_keep_manual_fields()
    {
        var repository = new MemoryMetadataRepository(Catalog(revision: 0, locked: []) with
        {
            Provider = "tmdb",
            ProviderKey = ProviderKey,
        });
        var updater = new UpdateMetadata(repository);
        var first = await updater.ExecuteAsync(
            new UpdateMetadataCommand(
                repository.Value.TitleId,
                new MetadataFieldChanges(Title: "Locked title"),
                new HashSet<MetadataField> { MetadataField.Title },
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);
        var stale = await updater.ExecuteAsync(
            new UpdateMetadataCommand(
                repository.Value.TitleId,
                new MetadataFieldChanges(Title: "Lost update"),
                new HashSet<MetadataField>(),
                ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(MetadataWriteOutcome.Applied, first.Outcome);
        Assert.Equal(MetadataWriteOutcome.Conflict, stale.Outcome);
        Assert.Equal("Locked title", repository.Value.Metadata.Title);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var restartedRefresh = Refresh(
                repository,
                RemoteDetails(repository.Value.TitleId, $"Remote {cycle}", $"/poster-{cycle}.jpg"));
            var refreshed = await restartedRefresh.ExecuteAsync(
                new RefreshMetadataCommand(
                    repository.Value.TitleId,
                    repository.Value.Revision,
                    RestoreProviderFields: false),
                TestContext.Current.CancellationToken);
            Assert.Equal(MetadataWriteOutcome.Applied, refreshed.Outcome);
            Assert.Equal("Locked title", repository.Value.Metadata.Title);
        }
    }

    private const string ProviderKey = "movie:6289";

    /// <summary>
    /// The refresh wired the way the composition root wires it: it resolves the provider entry from
    /// the row rather than being handed one, which is what stopped the editor's buttons being inert.
    /// </summary>
    private static RefreshMetadata Refresh(ICatalogMetadataRepository repository, MetadataDetails remote) =>
        new(
            repository,
            new StubMetadataProvider(remote),
            new MetadataMergePolicy(),
            TestIdentification.Language,
            TimeProvider.System);

    private static CatalogMetadata Catalog(int revision, HashSet<MetadataField> locked) => new(
        new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
        new EditableMetadata(
            "Manual title",
            "Arrival",
            "Old overview",
            2016,
            ["Drama"],
            "/manual-poster.jpg",
            "/old-backdrop.jpg",
            null,
            locked),
        revision);

    private static MetadataDetails RemoteDetails(TitleId titleId, string title, string poster) => new(
        new MetadataReference("tmdb", ProviderKey, MetadataContentKind.Movie),
        title,
        "Arrival",
        "Nuevo resumen remoto",
        2016,
        ["Ciencia ficción"],
        poster,
        "/remote-backdrop.jpg",
        TrailerKey: null);

    private sealed class MemoryMetadataRepository(CatalogMetadata initial) : ICatalogMetadataRepository
    {
        public CatalogMetadata Value { get; private set; } = initial;

        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogMetadata?>(Value.TitleId == titleId ? Value : null);

        // Staleness is decided by a SQL statement; a double that reimplemented it would test itself.
        public Task<IReadOnlyList<CatalogMetadata>> ListStaleAsync(
            DateTimeOffset staleBefore,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogMetadata>>([]);

        public Task<MetadataWriteResult> TrySaveAsync(
            CatalogMetadata catalog,
            int expectedRevision,
            CancellationToken cancellationToken = default)
        {
            if (Value.Revision != expectedRevision)
            {
                return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Conflict, Value));
            }

            Value = catalog with { Revision = expectedRevision + 1 };
            return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Applied, Value));
        }
    }
}
