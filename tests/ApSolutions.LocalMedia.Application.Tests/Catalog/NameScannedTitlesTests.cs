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
/// Which files the naming pass writes a name for, and which it leaves alone.
/// </summary>
/// <remarks>
/// What the name itself comes out as belongs to <c>ScannedTitlePolicyTests</c>, over the real parser;
/// what a real scan of a real folder produces belongs to <c>ScanSeriesGroupingTests</c>. What is here
/// is the pass's own arithmetic: the four kinds of scan result it walks past, and the one kind of file
/// it refuses to rename because something else owns the name.
/// </remarks>
public sealed class NameScannedTitlesTests
{
    private static readonly LibraryRootId RootId = new(new Guid("44444444-4444-4444-4444-444444444444"));
    private static readonly LibraryRoot Root = new(
        RootId,
        @"D:\Cine",
        RootKind.Local,
        RootAvailability.Available,
        ScanPolicy.Manual);

    /// <summary>
    /// The films are renamed and the episodes are not, because the series pass owns what an episode
    /// is called.
    /// </summary>
    /// <remarks>
    /// Two reasons, and the second is the one that decides it. The grid does not draw an episode's
    /// scanned row at all — the projection's query hides a file an <c>episode_media</c> link claims.
    /// And where that link is missing, because the episodes were loose in a root and the series
    /// policy refused to group them, cleaning the name would take away the only thing that tells one
    /// from another: «Puerto Sombra S01E01» and «…S01E02» both clean to «Puerto Sombra», and a column
    /// of identically named cards is worse than a column of ugly ones.
    /// </remarks>
    [Fact]
    public async Task Films_are_renamed_and_episodes_are_left_to_the_series_pass()
    {
        var files = new StubMediaFiles();
        var film = files.Add(@"D:\Cine\El Faro de Piedra 2019.mkv");
        var undated = files.Add(@"D:\Cine\Vacaciones en el lago.mp4");
        var episode = files.Add(@"D:\Cine\Puerto Sombra\Temporada 1\S01E01.mkv");

        var result = await Run(files, [
            new ScanItemResult(film.Path, ScanItemOutcome.Added),
            new ScanItemResult(undated.Path, ScanItemOutcome.Updated),
            new ScanItemResult(episode.Path, ScanItemOutcome.Unchanged),
        ]);

        Assert.Equal(new NameScannedTitlesResult(2, 1), result);
        Assert.Equal(
            [(film.Id, "El Faro de Piedra", 2019), (undated.Id, "Vacaciones en el lago", null)],
            files.Written);
    }

    /// <summary>
    /// An unchanged file is renamed like any other, which is what renames a library nobody rescans.
    /// </summary>
    /// <remarks>
    /// This is the whole reason the pass walks the summary instead of a list of what was stored: a
    /// file whose size and date have not moved is never re-stored, so a projection written once would
    /// keep the raw file name for as long as the file sat still. Everything already catalogued is in
    /// exactly that state on the day this ships.
    /// </remarks>
    [Fact]
    public async Task A_file_the_scan_found_unchanged_is_still_renamed()
    {
        var files = new StubMediaFiles();
        var settled = files.Add(@"D:\Cine\Alta.Marea.Baja.2015.720p.avi");

        var result = await Run(files, [new ScanItemResult(settled.Path, ScanItemOutcome.Unchanged)]);

        Assert.Equal(new NameScannedTitlesResult(1, 1), result);
        Assert.Equal([(settled.Id, "Alta Marea Baja", 2015)], files.Written);
    }

    /// <summary>
    /// A result the scan refused and a file the repository has lost are both passed over.
    /// </summary>
    /// <remarks>
    /// The first never reached the catalogue, so there is no projection row to name. The second is
    /// what a file removed between the walk and this pass looks like, and it is an ordinary evening
    /// for anybody with a drive that sleeps.
    /// </remarks>
    [Fact]
    public async Task A_refused_result_and_a_lost_file_are_both_passed_over()
    {
        var files = new StubMediaFiles();
        var kept = files.Add(@"D:\Cine\Vidrio Templado 2024.mkv");

        var result = await Run(files, [
            new ScanItemResult(kept.Path, ScanItemOutcome.Added),
            new ScanItemResult(@"D:\Cine\Ilegible.mkv", ScanItemOutcome.Failed),
            new ScanItemResult(@"D:\Cine\Se fue.mkv", ScanItemOutcome.Added),
        ]);

        Assert.Equal(new NameScannedTitlesResult(1, 1), result);
        Assert.Equal([(kept.Id, "Vidrio Templado", 2024)], files.Written);
    }

    /// <summary>
    /// A cancelled scan and a scan of a root nobody has both write nothing.
    /// </summary>
    /// <remarks>
    /// The two early returns every use case in this chain carries. The second is what a scan of a
    /// folder somebody removed mid-run looks like.
    /// </remarks>
    [Fact]
    public async Task A_cancelled_scan_and_a_missing_root_both_name_nothing()
    {
        var files = new StubMediaFiles();
        var film = files.Add(@"D:\Cine\Los Dias de Ambar 2020.mkv");
        var naming = new NameScannedTitles(new StubRoots(), files, new MediaNameParser());
        var summary = Summary([new ScanItemResult(film.Path, ScanItemOutcome.Added)]);

        Assert.Equal(
            new NameScannedTitlesResult(0, 0),
            await naming.ExecuteAsync(summary with { IsCancelled = true }, TestContext.Current.CancellationToken));
        Assert.Equal(
            new NameScannedTitlesResult(0, 0),
            await naming.ExecuteAsync(
                summary with { RootId = new LibraryRootId(Guid.NewGuid()) },
                TestContext.Current.CancellationToken));
        Assert.Empty(files.Written);
    }

    [Fact]
    public async Task The_use_case_refuses_what_it_cannot_work_without()
    {
        var files = new StubMediaFiles();
        var parser = new MediaNameParser();

        Assert.Throws<ArgumentNullException>(() => new NameScannedTitles(null!, files, parser));
        Assert.Throws<ArgumentNullException>(() => new NameScannedTitles(new StubRoots(), null!, parser));
        Assert.Throws<ArgumentNullException>(() => new NameScannedTitles(new StubRoots(), files, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new NameScannedTitles(new StubRoots(), files, parser).ExecuteAsync(
                null!,
                TestContext.Current.CancellationToken));
    }

    private static Task<NameScannedTitlesResult> Run(StubMediaFiles files, ScanItemResult[] results) =>
        new NameScannedTitles(new StubRoots(), files, new MediaNameParser()).ExecuteAsync(
            Summary(results),
            TestContext.Current.CancellationToken);

    private static ScanSummary Summary(ScanItemResult[] results) =>
        new(RootId, results.Length, results.Length, 0, 0, 0, false, null, results, TimeSpan.Zero);

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

        /// <summary>What was written, in order: the file, the name it got and the year beside it.</summary>
        public List<(MediaFileId Id, string Title, int? Year)> Written { get; } = [];

        public MediaFile Add(string path)
        {
            var file = new MediaFile(
                new MediaFileId(new Guid(++_next, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0])),
                RootId,
                path,
                1024,
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new TechnicalMetadata(TimeSpan.FromMinutes(118), "mkv", ["h264"], ["aac"], 1920, 1080),
                IsAvailable: true);
            _byPath[path] = file;
            return file;
        }

        public Task SetScannedTitleAsync(
            MediaFileId mediaFileId,
            ScannedTitle title,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(title);
            Written.Add((mediaFileId, title.DisplayTitle, title.Year));
            return Task.CompletedTask;
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
}
