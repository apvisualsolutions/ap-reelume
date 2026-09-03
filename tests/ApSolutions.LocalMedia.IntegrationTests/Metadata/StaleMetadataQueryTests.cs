// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

/// <summary>
/// LIB-016. The order is the policy: with a cap on the pass, whatever sorts last is what does not
/// get asked about, so it is measured against the real statement rather than against a double that
/// would only be repeating it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class StaleMetadataQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The stale window has to close before the retention ceiling does: a copy the cache may no
    /// longer be keeping cannot be the copy an automatic refresh is deciding about. The two numbers
    /// live in different layers, so nothing but this would notice them crossing.
    /// </summary>
    [Fact]
    public void Staleness_is_decided_well_inside_the_retention_ceiling()
    {
        Assert.True(
            MetadataRefreshPolicy.StaleAfter < TmdbOptions.RetentionLimit,
            $"Stale at {MetadataRefreshPolicy.StaleAfter.TotalDays} d must stay under the "
            + $"{TmdbOptions.RetentionLimit.TotalDays} d the provider's terms allow keeping anything.");
        Assert.Equal(90, MetadataRefreshPolicy.StaleAfter.TotalDays);
    }

    [Fact]
    public async Task The_stalest_identified_entries_come_first_and_never_more_than_the_cap()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new CatalogMetadataRepository(factory);

        // Thirty identified entries past ninety days, one of them with no date at all, plus two that
        // must not be asked about: a fresh one, and one nobody identified.
        var neverRefreshed = await StoreAsync(repository, "never refreshed", "movie/1", refreshedUtc: null);
        var ages = new List<(TitleId Id, int DaysAgo)>();
        for (var index = 0; index < 29; index++)
        {
            var daysAgo = 91 + index;
            ages.Add((
                await StoreAsync(repository, $"stale {index}", $"movie/{100 + index}", Now.AddDays(-daysAgo)),
                daysAgo));
        }

        var fresh = await StoreAsync(repository, "fresh", "movie/999", Now.AddDays(-1));
        var unidentified = await StoreAsync(repository, "unidentified", providerKey: null, refreshedUtc: null);

        var stale = await repository.ListStaleAsync(
            MetadataRefreshPolicy.StaleBefore(Now),
            MetadataRefreshPolicy.MaximumPerPass,
            TestContext.Current.CancellationToken);

        Assert.Equal(MetadataRefreshPolicy.MaximumPerPass, stale.Count);
        Assert.Equal(neverRefreshed, stale[0].TitleId);
        Assert.DoesNotContain(stale, entry => entry.TitleId == fresh);
        Assert.DoesNotContain(stale, entry => entry.TitleId == unidentified);

        // Oldest first, so a capped pass spends its requests on the entries that have waited longest.
        var expected = ages
            .OrderByDescending(entry => entry.DaysAgo)
            .Take(MetadataRefreshPolicy.MaximumPerPass - 1)
            .Select(entry => entry.Id)
            .ToArray();
        Assert.Equal(expected, stale.Skip(1).Select(entry => entry.TitleId).ToArray());
    }

    [Fact]
    public async Task An_entry_refreshed_within_the_window_is_not_asked_about_again()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);
        var repository = new CatalogMetadataRepository(factory);
        _ = await StoreAsync(repository, "just inside", "movie/1", Now.AddDays(-89));

        Assert.Empty(await repository.ListStaleAsync(
            MetadataRefreshPolicy.StaleBefore(Now),
            MetadataRefreshPolicy.MaximumPerPass,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_pass_of_no_entries_is_not_an_error()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, TestContext.Current.CancellationToken);

        Assert.Empty(await new CatalogMetadataRepository(factory).ListStaleAsync(
            MetadataRefreshPolicy.StaleBefore(Now),
            MetadataRefreshPolicy.MaximumPerPass,
            TestContext.Current.CancellationToken));
    }

    private static async Task<TitleId> StoreAsync(
        CatalogMetadataRepository repository,
        string title,
        string? providerKey,
        DateTimeOffset? refreshedUtc)
    {
        var titleId = new TitleId(Guid.NewGuid());
        var write = await repository.TrySaveAsync(
            new CatalogMetadata(
                titleId,
                new EditableMetadata(
                    title,
                    OriginalTitle: null,
                    Overview: null,
                    ReleaseYear: null,
                    Genres: [],
                    PosterPath: null,
                    BackdropPath: null,
                    TrailerKey: null,
                    LockedFields: new HashSet<MetadataField>()),
                Revision: 0,
                providerKey is null ? null : "tmdb",
                providerKey,
                refreshedUtc),
            expectedRevision: 0,
            TestContext.Current.CancellationToken);
        Assert.Equal(MetadataWriteOutcome.Applied, write.Outcome);
        return titleId;
    }
}
