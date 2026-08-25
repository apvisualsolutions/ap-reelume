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

        // The members are media files, and the destination draws what they are made of since
        // 2026-08-25 — resolution, codecs, size, running time, where they live and whether they are
        // reachable. So the files exist here as well as the group: a member with no file behind it
        // is a row the catalogue cannot describe, and it is not what a real library holds.
        var kept = Version("a", 3840, 2160, "HEVC", "E-AC-3", 19_756_431_155, isAvailable: true);
        var backup = Version("b", 1920, 1080, "H264", "AAC", 4_509_715_660, isAvailable: false);
        await SeedFileAsync(factory, kept);
        await SeedFileAsync(factory, backup);
        await SeedFileAsync(factory, Version("c", 1920, 1080, "H264", "AAC", 1_000, isAvailable: true));

        var groups = new MediaVersionGroupRepository(factory);
        await groups.SaveAsync(
            Group(pair, kept, backup) with { PreferredMediaFileId = kept.MediaFileId },
            TestContext.Current.CancellationToken);
        await groups.SaveAsync(
            Group(single, Version("c", 1920, 1080, "H264", "AAC", 1_000, isAvailable: true)),
            TestContext.Current.CancellationToken);

        var entries = await new DuplicateOverviewReader(factory)
            .ListAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(entries);
        Assert.Equal(new TitleId(pair), entry.TitleId);
        Assert.Equal("Arrival", entry.Title);
        Assert.Equal(2, entry.VersionCount);

        // And the table itself: two rows, one of them the stored preference, each carrying the eight
        // facts the columns are named after.
        Assert.NotNull(entry.Files);
        Assert.Equal(2, entry.Files!.Count);
        var preferred = Assert.Single(entry.Files, file => file.IsPreferred);
        Assert.Equal(kept.MediaFileId, preferred.MediaFileId);
        Assert.Equal(3840, preferred.Width);
        Assert.Equal(2160, preferred.Height);
        Assert.Equal("HEVC", preferred.VideoCodec);
        Assert.Equal("E-AC-3", preferred.AudioCodec);
        Assert.Equal(19_756_431_155, preferred.SizeBytes);
        Assert.Equal(TimeSpan.FromMinutes(100), preferred.Duration);
        Assert.True(preferred.IsAvailable);

        var other = Assert.Single(entry.Files, file => !file.IsPreferred);
        Assert.False(other.IsAvailable);
        Assert.Equal("H264", other.VideoCodec);
    }

    [Fact]
    public void A_reader_over_no_store_refuses_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new DuplicateOverviewReader(null!));
    }

    private static MediaVersionGroup Group(Guid titleId, params MediaVersion[] versions) => new(
        new MediaVersionId(Guid.NewGuid()),
        $"title:{titleId:D}",
        [.. versions],
        PreferredMediaFileId: null);

    private static MediaVersion Version(
        string name,
        int width,
        int height,
        string videoCodec,
        string audioCodec,
        long sizeBytes,
        bool isAvailable) => new(
        new MediaFileId(Guid.NewGuid()),
        $@"D:\media\{name}.mkv",
        isAvailable,
        Duration: TimeSpan.FromMinutes(100),
        width,
        height,
        IsHdr: false,
        videoCodec,
        sizeBytes);

    /// <summary>The file behind one member, as the scan would have written it.</summary>
    private static async Task SeedFileAsync(SqliteConnectionFactory factory, MediaVersion version)
    {
        await new MediaFileRepository(factory).UpsertAsync(
            new MediaFile(
                version.MediaFileId,
                new LibraryRootId(Guid.Parse("f1000000-0000-4000-8000-000000000001")),
                version.Path,
                version.SizeBytes,
                DateTimeOffset.UnixEpoch,
                new TechnicalMetadata(
                    version.Duration,
                    "matroska",
                    [version.VideoCodec],
                    [AudioCodecOf(version)],
                    version.Width,
                    version.Height),
                version.IsAvailable),
            CancellationToken.None);
    }

    /// <summary>
    /// Which audio codec a seeded copy carries. The 4K one is the one with the surround track, which
    /// is the ordinary shape of a duplicate pair and the reason the column exists at all.
    /// </summary>
    private static string AudioCodecOf(MediaVersion version) =>
        version.Width >= 3840 ? "E-AC-3" : "AAC";
}
