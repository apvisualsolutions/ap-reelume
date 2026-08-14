// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Backup;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Backup;

/// <summary>
/// The two promises a rotating copy makes: the snapshot is consistent even while the application is
/// writing, and rotation never removes the newest copy that could actually be restored.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RotatingBackupTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_snapshot_taken_while_progress_is_written_opens_and_passes_integrity()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new WatchStateRepository(factory);
        using var writing = new CancellationTokenSource();
        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = Task.Run(
            async () =>
            {
                var written = 0;
                while (!writing.IsCancellationRequested)
                {
                    await repository.SaveAsync(
                        Progress(written++ % 64, TimeSpan.FromSeconds(written)),
                        CancellationToken.None);
                    _ = firstWrite.TrySetResult();
                }

                return written;
            },
            TestContext.Current.CancellationToken);

        // The snapshot has to be taken while the database is being written to, so the writer has to
        // have written. Starting the backup as soon as the task was queued made the test depend on
        // thread-pool scheduling: under load the loop had not run once by the time it was cancelled.
        await firstWrite.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var destination = Path.Combine(directory.Path, "snapshot.db");
        var entry = await new SqliteBackupService(factory).WriteAsync(
            destination,
            TestContext.Current.CancellationToken);
        await writing.CancelAsync();
        var writes = await writer;

        Assert.True(writes > 0, "The concurrent writer never produced a row.");
        Assert.Equal(BackupContentPolicy.DatabaseEntryName, entry.RelativePath);
        Assert.Equal(new FileInfo(destination).Length, entry.Length);
        Assert.Equal(Sha256(destination), entry.Sha256);
        await using var snapshot = new SqliteConnection($"Data Source={destination};Mode=ReadOnly;Pooling=False");
        await snapshot.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(snapshot, "PRAGMA integrity_check;"));
        Assert.Equal(
            DatabaseTestHarness.MigrationCount,
            await SqliteBootstrapTests.ScalarInt64Async(snapshot, "SELECT COUNT(*) FROM schema_history;"));
    }

    [Fact]
    public async Task A_snapshot_never_leaves_a_half_written_file_at_the_destination()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var destination = Path.Combine(directory.Path, "snapshot.db");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqliteBackupService(factory).WriteAsync(destination, cancellation.Token));

        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task The_snapshot_estimates_from_the_database_that_exists_and_from_nothing_when_it_does_not()
    {
        using var directory = new DatabaseTestDirectory();
        var missing = new SqliteBackupService(
            new SqliteConnectionFactory(Path.Combine(directory.Path, "absent.db")));
        Assert.Equal(0, missing.EstimateBytes());

        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new FileInfo(directory.DatabasePath).Length, new SqliteBackupService(factory).EstimateBytes());
    }

    [Fact]
    public async Task A_destination_the_snapshot_cannot_take_leaves_no_temporary_file()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var destination = Path.Combine(directory.Path, "occupied");
        Directory.CreateDirectory(destination);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => new SqliteBackupService(factory).WriteAsync(
                destination,
                TestContext.Current.CancellationToken));

        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
        Assert.Empty(Directory.EnumerateFiles(destination));
    }

    [Fact]
    public async Task The_sixth_copy_prunes_the_oldest_and_leaves_five()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));

        var published = new List<BackupCopy>();
        for (var index = 0; index < 6; index++)
        {
            published.Add(await PublishValidCopyAsync(store, Noon.AddMinutes(index)));
        }

        var pruned = await store.PruneAsync(
            CreateBackup.DefaultRetention,
            TestContext.Current.CancellationToken);

        Assert.Equal(published[0].Path, Assert.Single(pruned).Path);
        Assert.False(Directory.Exists(published[0].Path));
        var remaining = await store.ListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, remaining.Count);
        Assert.All(remaining, copy => Assert.True(copy.IsValid));
        Assert.Equal(
            published.Skip(1).Select(copy => copy.Path).Order(StringComparer.Ordinal),
            remaining.Select(copy => copy.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Rotation_keeps_the_newest_valid_copy_even_when_it_is_the_oldest_on_disk()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));
        var onlyValid = await PublishValidCopyAsync(store, Noon);
        var corrupted = new List<BackupCopy>();
        for (var index = 1; index <= 6; index++)
        {
            var copy = await PublishValidCopyAsync(store, Noon.AddMinutes(index));
            await File.WriteAllTextAsync(
                Path.Combine(copy.Path, BackupContentPolicy.DatabaseEntryName),
                "tampered",
                TestContext.Current.CancellationToken);
            corrupted.Add(copy);
        }

        var pruned = await store.PruneAsync(
            CreateBackup.DefaultRetention,
            TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(onlyValid.Path), "Rotation deleted the last restorable copy.");
        Assert.DoesNotContain(onlyValid.Path, pruned.Select(copy => copy.Path));
        Assert.Contains(corrupted[0].Path, pruned.Select(copy => copy.Path));
        var remaining = await store.ListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(remaining, copy => copy.Path == onlyValid.Path && copy.IsValid);
        Assert.Equal(1, remaining.Count(copy => copy.IsValid));
    }

    [Fact]
    public async Task A_copy_whose_hash_no_longer_matches_its_manifest_is_reported_as_invalid()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));
        var copy = await PublishValidCopyAsync(store, Noon);

        await File.WriteAllTextAsync(
            Path.Combine(copy.Path, BackupContentPolicy.DatabaseEntryName),
            "tampered",
            TestContext.Current.CancellationToken);

        var listed = Assert.Single(await store.ListAsync(TestContext.Current.CancellationToken));
        Assert.False(listed.IsValid);
    }

    [Fact]
    public async Task Publishing_only_shows_the_copy_once_it_is_complete()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));

        var staging = await store.CreateStagingAsync(TestContext.Current.CancellationToken);
        await WritePayloadAsync(staging, Noon);
        Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith(".staging-", Path.GetFileName(staging), StringComparison.Ordinal);

        var copy = await store.PublishAsync(staging, Noon, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(staging));
        Assert.True(Directory.Exists(copy.Path));
        Assert.Single(await store.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_discarded_staging_leaves_nothing_the_store_would_list()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));

        var staging = await store.CreateStagingAsync(TestContext.Current.CancellationToken);
        await WritePayloadAsync(staging, Noon);
        store.DiscardStaging(staging);

        Assert.False(Directory.Exists(staging));
        Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_volume_that_cannot_hold_the_copy_refuses_before_any_write()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(
            Path.Combine(directory.Path, "backups"),
            _ => 1024);

        var failure = Assert.Throws<InsufficientBackupSpaceException>(() => store.EnsureSpace(4096));

        Assert.Equal(4096, failure.RequiredBytes);
        Assert.Equal(1024, failure.AvailableBytes);
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "backups")));
    }

    [Fact]
    public void The_real_volume_answers_how_much_room_it_has()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));

        store.EnsureSpace(1);

        Assert.Throws<InsufficientBackupSpaceException>(() => store.EnsureSpace(long.MaxValue));
    }

    [Fact]
    public async Task An_empty_folder_lists_nothing_and_prunes_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));

        Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await store.PruneAsync(
            CreateBackup.DefaultRetention,
            TestContext.Current.CancellationToken));

        await PublishValidCopyAsync(store, Noon);
        Assert.Empty(await store.PruneAsync(
            CreateBackup.DefaultRetention,
            TestContext.Current.CancellationToken));
        Assert.Single(await store.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_copy_with_no_manifest_or_an_unreadable_one_is_invalid_rather_than_a_failure()
    {
        using var directory = new DatabaseTestDirectory();
        var backups = Path.Combine(directory.Path, "backups");
        var store = new RotatingBackupStore(backups);
        Directory.CreateDirectory(Path.Combine(backups, "20260803T120000Z"));
        var unreadable = Path.Combine(backups, "20260803T130000Z");
        Directory.CreateDirectory(unreadable);
        await File.WriteAllTextAsync(
            Path.Combine(unreadable, BackupManifest.FileName),
            "{ this is not json",
            TestContext.Current.CancellationToken);

        var copies = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, copies.Count);
        Assert.All(copies, copy => Assert.False(copy.IsValid));
    }

    [Fact]
    public async Task A_manifest_from_another_format_or_with_changed_preferences_is_invalid()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));
        var future = await PublishValidCopyAsync(store, Noon);
        await RewriteManifestAsync(future, manifest => manifest with { FormatVersion = 99 });
        var preferences = await PublishValidCopyAsync(store, Noon.AddMinutes(1));
        await RewriteManifestAsync(
            preferences,
            manifest => manifest with { PreferencesSha256 = new string('0', 64) });

        var copies = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, copies.Count);
        Assert.All(copies, copy => Assert.False(copy.IsValid));
    }

    [Fact]
    public async Task Two_copies_taken_in_the_same_second_never_overwrite_each_other()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));

        var first = await PublishValidCopyAsync(store, Noon);
        var second = await PublishValidCopyAsync(store, Noon);

        Assert.NotEqual(first.Path, second.Path);
        Assert.Equal(2, (await store.ListAsync(TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task The_store_refuses_to_publish_or_discard_anything_outside_its_own_folder()
    {
        using var directory = new DatabaseTestDirectory();
        var store = new RotatingBackupStore(Path.Combine(directory.Path, "backups"));
        var outside = Path.Combine(directory.Path, "elsewhere");
        Directory.CreateDirectory(outside);

        Assert.Throws<InvalidOperationException>(() => store.DiscardStaging(outside));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.PublishAsync(outside, Noon, TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(outside));

        var staging = await store.CreateStagingAsync(TestContext.Current.CancellationToken);
        store.DiscardStaging(staging);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => store.PublishAsync(staging, Noon, TestContext.Current.CancellationToken));
        store.DiscardStaging(string.Empty);
    }

    private static async Task RewriteManifestAsync(
        BackupCopy copy,
        Func<BackupManifest, BackupManifest> change)
    {
        var manifestPath = Path.Combine(copy.Path, BackupManifest.FileName);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken),
            BackupSerialization.Options)!;
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(change(manifest), BackupSerialization.Options),
            TestContext.Current.CancellationToken);
    }

    private static async Task<BackupCopy> PublishValidCopyAsync(
        RotatingBackupStore store,
        DateTimeOffset createdUtc)
    {
        var staging = await store.CreateStagingAsync(TestContext.Current.CancellationToken);
        await WritePayloadAsync(staging, createdUtc);
        return await store.PublishAsync(staging, createdUtc, TestContext.Current.CancellationToken);
    }

    private static async Task WritePayloadAsync(string staging, DateTimeOffset createdUtc)
    {
        var databasePath = Path.Combine(staging, BackupContentPolicy.DatabaseEntryName);
        var contents = createdUtc.ToString("O", CultureInfo.InvariantCulture);
        await File.WriteAllTextAsync(databasePath, contents, TestContext.Current.CancellationToken);
        var manifest = new BackupManifest(
            BackupManifest.CurrentFormatVersion,
            "1.0.0",
            createdUtc,
            Sha256(databasePath),
            PreferencesSha256: null,
            PersonalArtwork: [],
            Roots: []);
        await File.WriteAllTextAsync(
            Path.Combine(staging, BackupManifest.FileName),
            JsonSerializer.Serialize(manifest, BackupSerialization.Options),
            TestContext.Current.CancellationToken);
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static WatchState Progress(int index, TimeSpan position) => new()
    {
        Content = ContentKey.ForTitle(new TitleId(CreateGuid(index))),
        Position = position,
        ObservedDuration = TimeSpan.FromMinutes(45),
        SourceMediaFileId = new MediaFileId(CreateGuid(index + 500)),
        Status = WatchStatus.InProgress,
        IsManualOverride = false,
        StartedUtc = Noon,
        UpdatedUtc = Noon.AddSeconds(position.TotalSeconds),
    };

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes, seed);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }
}
