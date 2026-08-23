// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

/// <summary>
/// The duplicates destination's list, read from the real store: groups of two or more, named by the
/// catalogue, and nothing else — a single-member group is not a duplicate, and a group whose key
/// parses to no title cannot be opened, so neither is offered.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DuplicateOverviewReaderTests
{
    [Fact]
    public async Task Groups_of_two_or_more_are_listed_with_their_catalogue_names()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using (var runner = new MigrationRunner(factory))
        {
            await runner.MigrateAsync(CancellationToken.None);
        }

        var pair = Guid.NewGuid();
        var single = Guid.NewGuid();
        await new CatalogRepository(factory).UpsertTitleAsync(
            new CatalogTitle(
                new TitleId(pair),
                CatalogTitleKind.Movie,
                "Arrival",
                "Arrival",
                2016,
                [],
                [],
                [],
                DateTimeOffset.UnixEpoch,
                LastPlayedUtc: null,
                HasProgress: false,
                IsPersonal: false,
                IsAvailable: true),
            TestContext.Current.CancellationToken);

        var groups = new MediaVersionGroupRepository(factory);
        await groups.SaveAsync(
            Group(pair, Version("a"), Version("b")),
            TestContext.Current.CancellationToken);
        await groups.SaveAsync(
            Group(single, Version("c")),
            TestContext.Current.CancellationToken);

        var entries = await new DuplicateOverviewReader(factory)
            .ListAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(entries);
        Assert.Equal(new TitleId(pair), entry.TitleId);
        Assert.Equal("Arrival", entry.Title);
        Assert.Equal(2, entry.VersionCount);
    }

    private static MediaVersionGroup Group(Guid titleId, params MediaVersion[] versions) => new(
        new MediaVersionId(Guid.NewGuid()),
        $"title:{titleId:D}",
        [.. versions],
        PreferredMediaFileId: null);

    private static MediaVersion Version(string name) => new(
        new MediaFileId(Guid.NewGuid()),
        $@"D:\media\{name}.mkv",
        IsAvailable: true,
        Duration: TimeSpan.FromMinutes(100),
        Width: 1920,
        Height: 1080,
        IsHdr: false,
        VideoCodec: "H264",
        SizeBytes: 1_000);
}
