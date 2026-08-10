// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Discovery;

/// <summary>
/// The decisions reconciliation takes, seen from outside. The happy path — a moved file that keeps
/// its entity, a copy that stays a copy, an ambiguous match a person settles — belongs to the
/// assembled scans in the integration suite, which is why the lines were already covered and the
/// branches were not. What is here is what those runs never reach: the refusals, the counters, and
/// the failure that must not cost the scan.
/// </summary>
public sealed class ReconcileScannedFilesTests
{
    [Fact]
    public void Every_collaborator_the_use_case_leans_on_is_required()
    {
        var catalogue = new InMemoryMediaFiles();
        var policy = new FileReconciliationPolicy();
        var reconcile = new ReconcileScanResults(catalogue, policy);
        var identities = new StubIdentityProvider();
        var pending = new PendingReassignments();

        Assert.Throws<ArgumentNullException>(() =>
            new ReconcileScannedFiles(null!, reconcile, identities, policy, pending));
        Assert.Throws<ArgumentNullException>(() =>
            new ReconcileScannedFiles(catalogue, null!, identities, policy, pending));
        Assert.Throws<ArgumentNullException>(() =>
            new ReconcileScannedFiles(catalogue, reconcile, null!, policy, pending));
        Assert.Throws<ArgumentNullException>(() =>
            new ReconcileScannedFiles(catalogue, reconcile, identities, null!, pending));
        Assert.Throws<ArgumentNullException>(() =>
            new ReconcileScannedFiles(catalogue, reconcile, identities, policy, null!));
    }

    [Fact]
    public async Task A_cancelled_scan_is_left_alone()
    {
        var fixture = new Fixture();
        _ = fixture.Scanned(@"Movies\Dune.2021.mkv", ScanItemOutcome.Added, onDisk: Printed("dune"));

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(isCancelled: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReconcileScannedFilesResult(0, 0, 0, 0), result);
        Assert.Empty(fixture.Catalogue.IdentitiesSaved);
    }

    [Fact]
    public async Task What_the_scan_could_not_catalogue_is_never_reconciled()
    {
        var fixture = new Fixture();
        _ = fixture.Scanned(@"Movies\Locked.mkv", ScanItemOutcome.Skipped, onDisk: Printed("locked"));
        _ = fixture.Scanned(@"Movies\Broken.mkv", ScanItemOutcome.Failed, onDisk: Printed("broken"));

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReconcileScannedFilesResult(0, 0, 0, 0), result);
        Assert.Empty(fixture.Catalogue.IdentitiesSaved);
    }

    [Fact]
    public async Task A_path_the_catalogue_does_not_know_is_left_alone()
    {
        var fixture = new Fixture();
        fixture.ScannedWithoutRow(@"Movies\Ghost.mkv", ScanItemOutcome.Added);

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReconcileScannedFilesResult(0, 0, 0, 0), result);
        Assert.Empty(fixture.Catalogue.IdentitiesSaved);
    }

    [Fact]
    public async Task A_file_whose_identity_cannot_be_read_counts_as_failed_and_the_scan_goes_on()
    {
        var fixture = new Fixture();
        _ = fixture.Scanned(
            @"Movies\Unreadable.mkv",
            ScanItemOutcome.Added,
            onDisk: new FileIdentity(VolumeId: null, FileId: null, Fingerprint: null));
        var healthy = fixture.Scanned(@"Movies\Dune.2021.mkv", ScanItemOutcome.Added, onDisk: Printed("dune"));

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(0, result.ReassignedCount);
        Assert.Equal(0, result.HeldCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(healthy, Assert.Single(fixture.Catalogue.IdentitiesSaved));
    }

    [Fact]
    public async Task Updated_content_refreshes_the_identity_its_old_bytes_left_behind()
    {
        var fixture = new Fixture();
        var file = fixture.Scanned(
            @"Movies\Dune.2021.mkv",
            ScanItemOutcome.Updated,
            onDisk: Printed("the-bytes-that-are-there-now"),
            stored: Printed("the-bytes-that-are-gone"));

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReconcileScannedFilesResult(1, 0, 0, 0), result);
        Assert.Equal("the-bytes-that-are-there-now", fixture.Catalogue.StoredIdentity(file)?.Fingerprint);
    }

    [Fact]
    public async Task A_stable_identity_nothing_else_carries_makes_the_row_its_own_entity()
    {
        var fixture = new Fixture();
        var identity = new FileIdentity("volume-1", "file-1", Fingerprint: null);
        var file = fixture.Scanned(@"Movies\Dune.2021.mkv", ScanItemOutcome.Added, onDisk: identity);

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReconcileScannedFilesResult(1, 0, 0, 0), result);
        Assert.Equal(identity, fixture.Catalogue.StoredIdentity(file));
    }

    [Fact]
    public async Task A_catalogue_that_throws_costs_one_file_and_not_the_scan()
    {
        var fixture = new Fixture();
        _ = fixture.Scanned(@"Movies\Explosive.mkv", ScanItemOutcome.Added, onDisk: Printed("explosive"));
        var healthy = fixture.Scanned(@"Movies\Dune.2021.mkv", ScanItemOutcome.Added, onDisk: Printed("dune"));
        fixture.Catalogue.OnFindByPath = path =>
        {
            if (path.EndsWith("Explosive.mkv", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The catalogue is not answering for this row.");
            }
        };

        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Summary(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReconcileScannedFilesResult(1, 0, 0, 1), result);
        Assert.Equal(healthy, Assert.Single(fixture.Catalogue.IdentitiesSaved));
    }

    [Fact]
    public async Task A_cancellation_stops_the_scan_instead_of_being_counted_as_a_failure()
    {
        var fixture = new Fixture();
        _ = fixture.Scanned(@"Movies\Dune.2021.mkv", ScanItemOutcome.Added, onDisk: Printed("dune"));
        using var cancellation = new CancellationTokenSource();
        fixture.Catalogue.OnFindByPath = _ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.UseCase.ExecuteAsync(fixture.Summary(), cancellation.Token));

        Assert.Empty(fixture.Catalogue.IdentitiesSaved);
    }

    private static FileIdentity Printed(string fingerprint) => new(null, null, fingerprint);

    private sealed class Fixture
    {
        private const string RootPath = @"C:\library";
        private static readonly LibraryRootId RootId = new(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        private readonly List<ScanItemResult> _items = [];

        public Fixture()
        {
            var policy = new FileReconciliationPolicy();
            UseCase = new ReconcileScannedFiles(
                Catalogue,
                new ReconcileScanResults(Catalogue, policy),
                Identities,
                policy,
                Pending);
        }

        public InMemoryMediaFiles Catalogue { get; } = new();

        public StubIdentityProvider Identities { get; } = new();

        public PendingReassignments Pending { get; } = new();

        public ReconcileScannedFiles UseCase { get; }

        /// <summary>Catalogues one row and lists its path in the scan with that outcome.</summary>
        public MediaFileId Scanned(
            string relativePath,
            ScanItemOutcome outcome,
            FileIdentity? onDisk = null,
            FileIdentity? stored = null)
        {
            var path = System.IO.Path.Combine(RootPath, relativePath);
            var id = Catalogue.Add(RootId, path);
            if (onDisk is not null)
            {
                Identities.Set(path, onDisk);
            }

            if (stored is not null)
            {
                Catalogue.SetIdentity(id, stored);
            }

            _items.Add(new ScanItemResult(path, outcome));
            return id;
        }

        /// <summary>A scan result whose path the catalogue never got a row for.</summary>
        public void ScannedWithoutRow(string relativePath, ScanItemOutcome outcome) =>
            _items.Add(new ScanItemResult(System.IO.Path.Combine(RootPath, relativePath), outcome));

        public ScanSummary Summary(bool isCancelled = false) => new(
            RootId,
            _items.Count,
            _items.Count,
            _items.Count,
            UnchangedCount: 0,
            ErrorCount: 0,
            isCancelled,
            ResumeAfterPath: null,
            [.. _items],
            TimeSpan.Zero);
    }

    private sealed class InMemoryMediaFiles : IMediaFileRepository
    {
        private static readonly TechnicalMetadata Metadata =
            new(TimeSpan.FromMinutes(1), "mkv", ["h264"], ["aac"], 1920, 1080);

        private readonly Dictionary<string, MediaFile> _byPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<MediaFileId, FileIdentity> _identities = [];

        /// <summary>What a catalogue that is having a bad day does when it is asked for a path.</summary>
        public Action<string>? OnFindByPath { get; set; }

        public List<MediaFileId> IdentitiesSaved { get; } = [];

        public MediaFileId Add(LibraryRootId rootId, string path)
        {
            var file = new MediaFile(
                new MediaFileId(Guid.NewGuid()),
                rootId,
                path,
                SizeBytes: 2,
                DateTimeOffset.UnixEpoch,
                Metadata);
            _byPath[path] = file;
            return file.Id;
        }

        public void SetIdentity(MediaFileId id, FileIdentity identity) => _identities[id] = identity;

        public FileIdentity? StoredIdentity(MediaFileId id) => _identities.GetValueOrDefault(id);

        public Task<MediaFile?> FindByPathAsync(
            LibraryRootId rootId,
            string path,
            CancellationToken cancellationToken = default)
        {
            OnFindByPath?.Invoke(path);
            return Task.FromResult(_byPath.TryGetValue(path, out var file) && file.LibraryRootId == rootId
                ? file
                : null);
        }

        public Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
            FileIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Identified().FirstOrDefault(candidate =>
                candidate.Identity.HasStableFileId &&
                string.Equals(candidate.Identity.VolumeId, identity.VolumeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.Identity.FileId, identity.FileId, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
            string fingerprint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IdentifiedMediaFile>>(
            [
                .. Identified().Where(candidate =>
                    string.Equals(candidate.Identity.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase)),
            ]);

        public Task SaveIdentityAsync(
            MediaFileId mediaFileId,
            FileIdentity identity,
            CancellationToken cancellationToken = default)
        {
            _identities[mediaFileId] = identity;
            IdentitiesSaved.Add(mediaFileId);
            return Task.CompletedTask;
        }

        public Task<FileIdentity?> GetIdentityAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_identities.GetValueOrDefault(mediaFileId));

        public Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default)
        {
            foreach (var path in _byPath.Where(entry => entry.Value.Id == mediaFileId).Select(entry => entry.Key))
            {
                _byPath.Remove(path);
            }

            _identities.Remove(mediaFileId);
            return Task.CompletedTask;
        }

        public Task ReassignAsync(
            MediaFileId mediaFileId,
            LibraryRootId libraryRootId,
            string newPath,
            FileIdentity identity,
            CancellationToken cancellationToken = default)
        {
            var moved = _byPath.Values.Single(file => file.Id == mediaFileId) with
            {
                LibraryRootId = libraryRootId,
                Path = newPath,
            };
            _byPath[moved.Path] = moved;
            _identities[mediaFileId] = identity;
            return Task.CompletedTask;
        }

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

        private IEnumerable<IdentifiedMediaFile> Identified() => _byPath.Values
            .Where(file => _identities.ContainsKey(file.Id))
            .Select(file => new IdentifiedMediaFile(file, _identities[file.Id]));
    }

    private sealed class StubIdentityProvider : IFileIdentityProvider
    {
        private readonly Dictionary<string, FileIdentity> _byPath = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string path, FileIdentity identity) => _byPath[path] = identity;

        public Task<FileIdentity> GetAsync(
            string path,
            TechnicalMetadata technicalMetadata,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_byPath.TryGetValue(path, out var identity)
                ? identity
                : new FileIdentity(null, null, null));
    }
}
