// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Catalog;

/// <summary>
/// The decisions the series assembler makes that a folder on a disk cannot easily be made to ask.
/// </summary>
/// <remarks>
/// The end-to-end proof lives in <c>ScanSeriesGroupingTests</c>, over a real scan of a real folder
/// tree. What is here is the handful of answers that need a file the scan will not produce: a result
/// the scan refused, a file the repository has lost, a disconnected copy, and two files claiming the
/// same episode. Every one of those is an ordinary evening for somebody with a backup drive.
/// </remarks>
public sealed class GroupScannedEpisodesTests
{
    private static readonly LibraryRootId RootId = new(new Guid("33333333-3333-3333-3333-333333333333"));
    private static readonly LibraryRoot Root = new(
        RootId,
        @"D:\Series",
        RootKind.Local,
        RootAvailability.Available,
        ScanPolicy.Manual);

    [Fact]
    public async Task A_result_the_scan_refused_and_a_file_it_lost_are_both_passed_over()
    {
        var catalog = new RecordingCatalog();
        var files = new StubMediaFiles();
        files.Add(@"D:\Series\Puerto Sombra\Temporada 1\S01E01.mkv");

        var result = await Run(catalog, files, [
            new ScanItemResult(@"D:\Series\Puerto Sombra\Temporada 1\S01E01.mkv", ScanItemOutcome.Added),

            // Refused by the scan: it never reached the catalogue, so there is nothing to place.
            new ScanItemResult(@"D:\Series\Puerto Sombra\Temporada 1\S01E02.mkv", ScanItemOutcome.Failed),

            // Accepted by the scan and absent from the repository, which is what a file removed
            // between the walk and this pass looks like.
            new ScanItemResult(@"D:\Series\Puerto Sombra\Temporada 1\S01E03.mkv", ScanItemOutcome.Added),
        ]);

        Assert.Equal(new GroupScannedEpisodesResult(1, 1), result);
        Assert.Single(catalog.Episodes);
        Assert.Equal(1, catalog.Episodes[0].EpisodeNumber);
    }

    /// <summary>
    /// Two files claiming one episode: one row, and the copy behind it is the one that can be played.
    /// </summary>
    /// <remarks>
    /// Which of two duplicates is the better copy is the version grouping's question and not this
    /// one's. What this has to get right is narrower and matters more: an episode whose row points at
    /// a disconnected backup is an episode the card offers to play and cannot.
    /// </remarks>
    [Fact]
    public async Task Two_copies_of_one_episode_leave_one_row_pointing_at_the_reachable_one()
    {
        var catalog = new RecordingCatalog();
        var files = new StubMediaFiles();
        var offline = files.Add(@"D:\Series\Puerto Sombra\Temporada 1\S01E01.copia.mkv", isAvailable: false);
        var online = files.Add(@"D:\Series\Puerto Sombra\Temporada 1\S01E01.mkv");

        var result = await Run(catalog, files, [
            new ScanItemResult(offline.Path, ScanItemOutcome.Unchanged),
            new ScanItemResult(online.Path, ScanItemOutcome.Added),
        ]);

        Assert.Equal(new GroupScannedEpisodesResult(1, 1), result);
        Assert.Single(catalog.Links);
        Assert.Equal(online.Id, catalog.Links[0].MediaFileId);
        Assert.True(catalog.Episodes[0].IsAvailable);
    }

    /// <summary>
    /// A show whose every copy is disconnected is still a show, and it says it is out of reach.
    /// </summary>
    /// <remarks>
    /// Hiding it would be the catalogue forgetting a series because a drive was unplugged, which is
    /// the one thing this application promises never to do. The card is drawn and wears the
    /// unavailable badge, which is what the grid already does for a film on a disconnected root.
    /// </remarks>
    [Fact]
    public async Task A_show_with_no_reachable_copy_is_still_a_show_and_says_so()
    {
        var catalog = new RecordingCatalog();
        var files = new StubMediaFiles();
        var only = files.Add(@"D:\Series\Astillero\Temporada 1\S01E01.mkv", isAvailable: false);

        var result = await Run(catalog, files, [new ScanItemResult(only.Path, ScanItemOutcome.Unchanged)]);

        Assert.Equal(new GroupScannedEpisodesResult(1, 1), result);
        Assert.Single(catalog.Titles);
        Assert.False(catalog.Titles[0].IsAvailable);
        Assert.False(catalog.Episodes[0].IsAvailable);
        Assert.Equal(CatalogTitleKind.Show, catalog.Titles[0].Kind);
    }

    /// <summary>
    /// A show is as old as its first episode, which is what a rail sorted by "recently added" needs.
    /// </summary>
    /// <remarks>
    /// A season that arrives one episode a week would otherwise climb to the top of that rail every
    /// week, pushing everything somebody actually added out of the way.
    /// </remarks>
    [Fact]
    public async Task A_show_is_as_old_as_its_earliest_episode()
    {
        var catalog = new RecordingCatalog();
        var files = new StubMediaFiles();
        var first = files.Add(
            @"D:\Series\Astillero\Temporada 1\S01E01.mkv",
            lastWrite: new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero));
        var later = files.Add(
            @"D:\Series\Astillero\Temporada 1\S01E02.mkv",
            lastWrite: new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero));

        _ = await Run(catalog, files, [
            new ScanItemResult(later.Path, ScanItemOutcome.Added),
            new ScanItemResult(first.Path, ScanItemOutcome.Added),
        ]);

        Assert.Equal(first.LastWriteUtc, catalog.Titles[0].AddedUtc);
    }

    [Fact]
    public async Task The_use_case_refuses_what_it_cannot_work_without()
    {
        var files = new StubMediaFiles();
        var catalog = new RecordingCatalog();
        var parser = new MediaNameParser();
        Assert.Throws<ArgumentNullException>(() =>
            new GroupScannedEpisodes(null!, files, catalog, parser));
        Assert.Throws<ArgumentNullException>(() =>
            new GroupScannedEpisodes(new StubRoots(), null!, catalog, parser));
        Assert.Throws<ArgumentNullException>(() =>
            new GroupScannedEpisodes(new StubRoots(), files, null!, parser));
        Assert.Throws<ArgumentNullException>(() =>
            new GroupScannedEpisodes(new StubRoots(), files, catalog, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new GroupScannedEpisodes(new StubRoots(), files, catalog, parser).ExecuteAsync(
                null!,
                TestContext.Current.CancellationToken));
    }

    private static Task<GroupScannedEpisodesResult> Run(
        RecordingCatalog catalog,
        StubMediaFiles files,
        ScanItemResult[] results) =>
        new GroupScannedEpisodes(new StubRoots(), files, catalog, new MediaNameParser()).ExecuteAsync(
            new ScanSummary(RootId, results.Length, results.Length, 0, 0, 0, false, null, results, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

    private sealed class StubRoots : ILibraryRootRepository
    {
        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>([Root]);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<LibraryRoot?>(id == RootId ? Root : null);

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubMediaFiles : IMediaFileRepository
    {
        private readonly Dictionary<string, MediaFile> _byPath = new(StringComparer.OrdinalIgnoreCase);
        private int _next;

        public MediaFile Add(string path, bool isAvailable = true, DateTimeOffset? lastWrite = null)
        {
            var file = new MediaFile(
                new MediaFileId(new Guid(++_next, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0])),
                RootId,
                path,
                1024,
                lastWrite ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new TechnicalMetadata(TimeSpan.FromMinutes(53), "mkv", ["h264"], ["aac"], 1920, 1080),
                isAvailable);
            _byPath[path] = file;
            return file;
        }

        public Task<MediaFile?> FindByPathAsync(
            LibraryRootId rootId,
            string normalizedPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(rootId == RootId && _byPath.TryGetValue(normalizedPath, out var file)
                ? file
                : null);


        public Task<MediaFile?> FindByIdAsync(MediaFileId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, MediaFile>> FindByPathsAsync(
            LibraryRootId rootId,
            IReadOnlyCollection<string> paths,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpsertBatchAsync(
            IReadOnlyCollection<MediaFile> mediaFiles,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
            FileIdentity identity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
            string fingerprint,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveIdentityAsync(
            MediaFileId mediaFileId,
            FileIdentity identity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FileIdentity?> GetIdentityAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReassignAsync(
            MediaFileId mediaFileId,
            LibraryRootId libraryRootId,
            string newPath,
            FileIdentity identity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetRootAvailabilityAsync(
            LibraryRootId libraryRootId,
            bool isAvailable,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> GetScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveScanCheckpointAsync(
            LibraryRootId rootId,
            string resumeAfterPath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ClearScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingCatalog : ICatalogRepository
    {
        public List<CatalogTitle> Titles { get; } = [];

        public List<CatalogSeason> Seasons { get; } = [];

        public List<CatalogEpisode> Episodes { get; } = [];

        public List<(EpisodeId EpisodeId, MediaFileId MediaFileId)> Links { get; } = [];

        public Task UpsertTitleAsync(CatalogTitle title, CancellationToken cancellationToken = default)
        {
            Titles.Add(title);
            return Task.CompletedTask;
        }

        public Task UpsertSeasonAsync(CatalogSeason season, CancellationToken cancellationToken = default)
        {
            Seasons.Add(season);
            return Task.CompletedTask;
        }

        public Task UpsertEpisodeAsync(CatalogEpisode episode, CancellationToken cancellationToken = default)
        {
            Episodes.Add(episode);
            return Task.CompletedTask;
        }

        public Task LinkEpisodeMediaAsync(
            EpisodeId episodeId,
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default)
        {
            Links.Add((episodeId, mediaFileId));
            return Task.CompletedTask;
        }
    }
}
