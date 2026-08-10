// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using ApSolutions.LocalMedia.Presentation.Library;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

/// <summary>
/// The moved-file half of LIB-002/003, walked for real: real files on a real disk, scanned by the
/// real coordinator into real SQLite, with reconciliation running in the shared pipeline. A moved
/// file keeps being the entity it was; a copy stays a copy; an ambiguous match waits for a person
/// in the review inbox and survives a rescan.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ScanReconciliationTests
{
    [Fact]
    public async Task A_file_moved_between_scans_keeps_its_entity_and_leaves_no_stranger_behind()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory);
        var oldPath = Path.Combine(harness.MediaRoot, "Movies", "film.mkv");
        await WriteContentAsync(oldPath, seed: 1);
        await harness.ScanAndReconcileAsync();
        var original = await harness.MediaFiles.FindByPathAsync(
            harness.Root.Id,
            oldPath,
            TestContext.Current.CancellationToken);
        Assert.NotNull(original);

        var newPath = Path.Combine(harness.MediaRoot, "Archive", "film.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.Move(oldPath, newPath);
        var result = await harness.ScanAndReconcileAsync();

        Assert.Equal(1, result.ReassignedCount);
        var reassigned = await harness.MediaFiles.FindByPathAsync(
            harness.Root.Id,
            newPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(original!.Id, reassigned?.Id);
        Assert.Equal(1, await CountMediaAsync(harness.Factory));
        Assert.Empty(harness.Pending.List());
    }

    [Fact]
    public async Task A_byte_identical_copy_stays_a_copy_and_is_never_reassigned()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory);
        var firstPath = Path.Combine(harness.MediaRoot, "Movies", "film.mkv");
        await WriteContentAsync(firstPath, seed: 2);
        await harness.ScanAndReconcileAsync();

        var copyPath = Path.Combine(harness.MediaRoot, "Movies", "film-copy.mkv");
        File.Copy(firstPath, copyPath);
        var result = await harness.ScanAndReconcileAsync();

        Assert.Equal(0, result.ReassignedCount);
        Assert.Equal(0, result.HeldCount);
        Assert.Equal(2, await CountMediaAsync(harness.Factory));
        Assert.Empty(harness.Pending.List());
    }

    [Fact]
    public async Task An_ambiguous_match_waits_for_a_person_survives_a_rescan_and_confirms_by_hand()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory);
        var firstPath = Path.Combine(harness.MediaRoot, "Movies", "film.mkv");
        var secondPath = Path.Combine(harness.MediaRoot, "Movies", "film-copy.mkv");
        await WriteContentAsync(firstPath, seed: 3);
        File.Copy(firstPath, secondPath);
        await harness.ScanAndReconcileAsync();
        var first = await harness.MediaFiles.FindByPathAsync(
            harness.Root.Id,
            firstPath,
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);

        // Both known copies leave; a fresh copy of the same bytes arrives somewhere new. Two
        // candidates share the fingerprint, so nobody may guess which entity this is.
        var newPath = Path.Combine(harness.MediaRoot, "Archive", "film.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.Copy(firstPath, newPath);
        File.Delete(firstPath);
        File.Delete(secondPath);
        var held = await harness.ScanAndReconcileAsync();

        Assert.Equal(1, held.HeldCount);
        var offer = Assert.Single(harness.Pending.List());
        Assert.Equal(newPath, offer.Command.NewPath);
        Assert.Equal(2, offer.Candidates.Count);

        // A rescan re-derives the same offer: the held file kept no identity on purpose, so a
        // restart loses the list but never the decision.
        var again = await harness.ScanAndReconcileAsync();
        Assert.Equal(1, again.HeldCount);
        Assert.Single(harness.Pending.List());

        // The person says which one it is, the way the inbox does: the old entity takes the new
        // path and the stranger row the scan created is absorbed.
        var reassignment = new ManualReassignmentViewModel(harness.Reconcile);
        reassignment.Review(offer.Command, first!.Id);
        var confirmed = await reassignment.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, confirmed.MediaFileId);
        var settled = await harness.MediaFiles.FindByPathAsync(
            harness.Root.Id,
            newPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(first.Id, settled?.Id);
        Assert.Equal(2, await CountMediaAsync(harness.Factory));
    }

    [Fact]
    public async Task Keeping_a_held_file_as_new_stops_the_offer_for_good()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory);
        var firstPath = Path.Combine(harness.MediaRoot, "Movies", "film.mkv");
        var secondPath = Path.Combine(harness.MediaRoot, "Movies", "film-copy.mkv");
        await WriteContentAsync(firstPath, seed: 4);
        File.Copy(firstPath, secondPath);
        await harness.ScanAndReconcileAsync();
        var newPath = Path.Combine(harness.MediaRoot, "Archive", "film.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.Copy(firstPath, newPath);
        File.Delete(firstPath);
        File.Delete(secondPath);
        _ = await harness.ScanAndReconcileAsync();
        var offer = Assert.Single(harness.Pending.List());

        await harness.Reconciliation.KeepAsNewAsync(offer, TestContext.Current.CancellationToken);
        var after = await harness.ScanAndReconcileAsync();

        Assert.Empty(harness.Pending.List());
        Assert.Equal(0, after.HeldCount);
        Assert.Equal(3, await CountMediaAsync(harness.Factory));
    }

    private static async Task WriteContentAsync(string path, int seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = Enumerable.Range(0, 256 * 1024).Select(index => (byte)((index * seed) % 251)).ToArray();
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
    }

    private static async Task<ReconciliationHarness> HarnessAsync(DatabaseTestDirectory directory)
    {
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var mediaRoot = Path.Combine(directory.Path, "media");
        Directory.CreateDirectory(mediaRoot);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            mediaRoot,
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);
        await roots.AddAsync(root, TestContext.Current.CancellationToken);
        var mediaFiles = new MediaFileRepository(factory);
        var reconcile = new ReconcileScanResults(mediaFiles, new FileReconciliationPolicy());
        var pending = new PendingReassignments();
        var reconciliation = new ReconcileScannedFiles(
            mediaFiles,
            reconcile,
            new CompositeFileIdentityProvider(
                new NtfsFileIdentityProvider(),
                new LightweightFingerprintProvider()),
            new FileReconciliationPolicy(),
            pending);
        var coordinator = new ScanCoordinator(
            roots,
            mediaFiles,
            new MediaFileEnumerator(),
            new StubProbe(),
            new InProcessApplicationEventPublisher());
        return new ReconciliationHarness(
            factory,
            mediaFiles,
            root,
            mediaRoot,
            coordinator,
            reconciliation,
            reconcile,
            pending);
    }

    private static async Task<long> CountMediaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM media_files;";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private sealed record ReconciliationHarness(
        SqliteConnectionFactory Factory,
        MediaFileRepository MediaFiles,
        LibraryRoot Root,
        string MediaRoot,
        ScanCoordinator Coordinator,
        ReconcileScannedFiles Reconciliation,
        ReconcileScanResults Reconcile,
        PendingReassignments Pending)
    {
        public async Task<ReconcileScannedFilesResult> ScanAndReconcileAsync()
        {
            var summary = await Coordinator.StartAsync(
                new StartScanCommand(Root.Id, ScanTrigger.Manual, 16),
                TestContext.Current.CancellationToken);
            return await Reconciliation.ExecuteAsync(summary, TestContext.Current.CancellationToken);
        }
    }

    private sealed class StubProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(90),
                "matroska",
                ["h264"],
                ["aac"],
                1920,
                1080));
    }
}
