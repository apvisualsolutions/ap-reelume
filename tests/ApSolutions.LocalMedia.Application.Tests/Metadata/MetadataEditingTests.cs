// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
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
            locked: [MetadataField.Title, MetadataField.PosterPath]));
        var refresh = new RefreshMetadata(repository, new MetadataMergePolicy());
        var remote = RemoteDetails(titleId, "La llegada", "/remote-poster.jpg");

        var preserved = await refresh.ExecuteAsync(
            new RefreshMetadataCommand(titleId, remote, ExpectedRevision: 4, RestoreProviderFields: false),
            TestContext.Current.CancellationToken);
        var restored = await refresh.ExecuteAsync(
            new RefreshMetadataCommand(titleId, remote, ExpectedRevision: 5, RestoreProviderFields: true),
            TestContext.Current.CancellationToken);

        Assert.Equal("Manual title", preserved.Catalog?.Metadata.Title);
        Assert.Equal("/manual-poster.jpg", preserved.Catalog?.Metadata.PosterPath);
        Assert.Equal("Nuevo resumen remoto", preserved.Catalog?.Metadata.Overview);
        Assert.Equal("La llegada", restored.Catalog?.Metadata.Title);
        Assert.Equal("/remote-poster.jpg", restored.Catalog?.Metadata.PosterPath);
        Assert.Empty(restored.Catalog?.Metadata.LockedFields ?? new HashSet<MetadataField> { MetadataField.Title });
    }

    [Fact]
    public async Task Stale_edit_conflicts_and_three_edit_refresh_restart_cycles_keep_manual_fields()
    {
        var repository = new MemoryMetadataRepository(Catalog(revision: 0, locked: []));
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
            var restartedRefresh = new RefreshMetadata(repository, new MetadataMergePolicy());
            var refreshed = await restartedRefresh.ExecuteAsync(
                new RefreshMetadataCommand(
                    repository.Value.TitleId,
                    RemoteDetails(repository.Value.TitleId, $"Remote {cycle}", $"/poster-{cycle}.jpg"),
                    repository.Value.Revision,
                    RestoreProviderFields: false),
                TestContext.Current.CancellationToken);
            Assert.Equal(MetadataWriteOutcome.Applied, refreshed.Outcome);
            Assert.Equal("Locked title", repository.Value.Metadata.Title);
        }
    }

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
            locked),
        revision);

    private static MetadataDetails RemoteDetails(TitleId titleId, string title, string poster) => new(
        new MetadataReference("tmdb", $"movie:{titleId.Value:N}", MetadataContentKind.Movie),
        "es-ES",
        title,
        "Arrival",
        "Nuevo resumen remoto",
        2016,
        ["Ciencia ficción"],
        poster,
        "/remote-backdrop.jpg");

    private sealed class MemoryMetadataRepository(CatalogMetadata initial) : ICatalogMetadataRepository
    {
        public CatalogMetadata Value { get; private set; } = initial;

        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogMetadata?>(Value.TitleId == titleId ? Value : null);

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
