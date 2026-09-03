// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

/// <summary>
/// Finding the file behind an identifier. A surface holds an identifier and the engine needs the path
/// it stands for, so without this lookup a title card can name a film it cannot open.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MediaFileLookupTests
{
    private static readonly LibraryRootId RootId = new(new Guid("cccccccc-0000-0000-0000-000000000001"));
    private static readonly MediaFileId FileId = new(new Guid("cccccccc-0000-0000-0000-000000000002"));

    [Fact]
    public async Task An_identifier_the_catalogue_never_knew_answers_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var repository = await CreateAsync(directory);

        Assert.Null(await repository.FindByIdAsync(FileId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_stored_file_comes_back_with_the_path_the_engine_would_open()
    {
        using var directory = new DatabaseTestDirectory();
        var repository = await CreateAsync(directory);
        await SeedRootAsync(directory);
        await repository.UpsertAsync(
            new MediaFile(
                FileId,
                RootId,
                "R:\\media\\a.mkv",
                1024,
                DateTimeOffset.UnixEpoch,
                new TechnicalMetadata(TimeSpan.FromMinutes(116), "mkv", ["HEVC"], ["EAC3"], 3840, 2160)),
            TestContext.Current.CancellationToken);

        var found = await repository.FindByIdAsync(FileId, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal("R:\\media\\a.mkv", found!.Path);
        Assert.Equal(RootId, found.LibraryRootId);
        Assert.Equal(TimeSpan.FromMinutes(116), found.TechnicalMetadata.Duration);
        Assert.True(found.IsAvailable);
    }

    private static async Task<MediaFileRepository> CreateAsync(DatabaseTestDirectory directory)
    {
        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(directory.DatabasePath, CancellationToken.None);

        return new MediaFileRepository(factory);
    }

    private static async Task SeedRootAsync(DatabaseTestDirectory directory)
    {
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new LibraryRootRepository(factory).AddAsync(
            new LibraryRoot(RootId, "R:\\media", RootKind.Local, RootAvailability.Available, ScanPolicy.Manual),
            TestContext.Current.CancellationToken);
    }
}
