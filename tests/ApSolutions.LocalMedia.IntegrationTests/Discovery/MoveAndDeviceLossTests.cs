using System.Reflection;
using ApSolutions.LocalMedia.Application.Discovery;
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

public sealed class MoveAndDeviceLossTests
{
    [Fact]
    public async Task Identity_slice_owns_Windows_and_fallback_providers_transaction_and_UI()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));

        Assert.NotNull(RequireType(
            "ApSolutions.LocalMedia.Infrastructure.FileSystem.NtfsFileIdentityProvider",
            "ApSolutions.LocalMedia.Infrastructure"));
        Assert.NotNull(RequireType(
            "ApSolutions.LocalMedia.Infrastructure.FileSystem.LightweightFingerprintProvider",
            "ApSolutions.LocalMedia.Infrastructure"));
        Assert.NotNull(RequireType(
            "ApSolutions.LocalMedia.Application.Discovery.ReconcileScanResults",
            "ApSolutions.LocalMedia.Application"));
        Assert.NotNull(RequireType(
            "ApSolutions.LocalMedia.Presentation.Library.ManualReassignmentViewModel",
            "ApSolutions.LocalMedia.Presentation"));

        await using var connection = await DatabaseTestHarness.OpenAsync(factory);
        Assert.True(await TableExistsAsync(connection, "media_file_identities"));
    }

    [Fact]
    public async Task NTFS_move_preserves_volume_and_file_identity()
    {
        Assert.True(OperatingSystem.IsWindows());
        using var directory = new DatabaseTestDirectory();
        var originalPath = Path.Combine(directory.Path, "before.mkv");
        var movedPath = Path.Combine(directory.Path, "after.mkv");
        await File.WriteAllBytesAsync(originalPath, [0x41, 0x50], TestContext.Current.CancellationToken);
        var provider = new NtfsFileIdentityProvider();

        var before = await provider.GetAsync(originalPath, Metadata, TestContext.Current.CancellationToken);
        File.Move(originalPath, movedPath);
        var after = await provider.GetAsync(movedPath, Metadata, TestContext.Current.CancellationToken);

        Assert.True(before.HasStableFileId);
        Assert.Equal(before.VolumeId, after.VolumeId);
        Assert.Equal(before.FileId, after.FileId);
    }

    [Fact]
    public async Task Lightweight_fingerprint_reads_at_most_three_bounded_samples()
    {
        using var directory = new DatabaseTestDirectory();
        var originalPath = Path.Combine(directory.Path, "large.mkv");
        var movedPath = Path.Combine(directory.Path, "moved.mkv");
        var bytes = Enumerable.Range(0, 1024 * 1024).Select(index => (byte)(index % 251)).ToArray();
        await File.WriteAllBytesAsync(originalPath, bytes, TestContext.Current.CancellationToken);
        var provider = new LightweightFingerprintProvider();

        var before = await provider.GetAsync(originalPath, Metadata, TestContext.Current.CancellationToken);
        File.Move(originalPath, movedPath);
        var afterMove = await provider.GetAsync(movedPath, Metadata, TestContext.Current.CancellationToken);
        await using (var stream = new FileStream(
                         movedPath,
                         FileMode.Open,
                         FileAccess.Write,
                         FileShare.Read,
                         bufferSize: 1,
                         useAsync: true))
        {
            stream.Position = stream.Length / 2;
            await stream.WriteAsync(new byte[] { 0xFF }, TestContext.Current.CancellationToken);
        }

        var afterChange = await provider.GetAsync(movedPath, Metadata, TestContext.Current.CancellationToken);

        Assert.True(before.HasFingerprint);
        Assert.StartsWith("sha256:v1:", before.Fingerprint, StringComparison.Ordinal);
        Assert.Equal(before.Fingerprint, afterMove.Fingerprint);
        Assert.NotEqual(before.Fingerprint, afterChange.Fingerprint);
        Assert.InRange(provider.LastSampledByteCount, 1, LightweightFingerprintProvider.MaximumSampledBytes);
    }

    [Fact]
    public async Task Fingerprint_collision_keeps_the_old_path_until_manual_commit()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var rootId = new LibraryRootId(Guid.NewGuid());
        var first = Media(rootId, @"R:\Media\first.mkv", 1);
        var second = Media(rootId, @"R:\Media\second.mkv", 2);
        var collision = new FileIdentity(null, null, "sha256:v1:COLLISION");
        await fixture.Repository.UpsertBatchAsync([first, second], TestContext.Current.CancellationToken);
        await fixture.Repository.SaveIdentityAsync(first.Id, collision, TestContext.Current.CancellationToken);
        await fixture.Repository.SaveIdentityAsync(second.Id, collision, TestContext.Current.CancellationToken);
        var command = new ReconcileFileCommand(rootId, @"S:\Moved\first.mkv", collision);

        var preview = await fixture.Reconciliation.ReconcileAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(ReconciliationDecision.Probable, preview.Decision);
        Assert.True(preview.RequiresConfirmation);
        Assert.NotNull(await fixture.Repository.FindByPathAsync(
            rootId,
            first.Path,
            TestContext.Current.CancellationToken));
        Assert.Null(await fixture.Repository.FindByPathAsync(
            rootId,
            command.NewPath,
            TestContext.Current.CancellationToken));

        var viewModel = new ManualReassignmentViewModel(fixture.Reconciliation);
        viewModel.Review(command, first.Id);
        var committed = await viewModel.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ReconciliationDecision.Exact, committed.Decision);
        Assert.Equal(first.Id, committed.MediaFileId);
        Assert.True(committed.PreservesProgress);
        Assert.Null(await fixture.Repository.FindByPathAsync(
            rootId,
            first.Path,
            TestContext.Current.CancellationToken));
        var reassigned = await fixture.Repository.FindByPathAsync(
            rootId,
            command.NewPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(first.Id, reassigned?.Id);
        Assert.Equal(2, await CountMediaAsync(fixture.Factory));
    }

    [Fact]
    public async Task Persisted_stable_identity_reconciles_exactly_without_a_fingerprint()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var rootId = new LibraryRootId(Guid.NewGuid());
        var media = Media(rootId, @"C:\Media\before.mkv", 20);
        var identity = new FileIdentity("A1B2C3D4", "0000000000000042", null);
        await fixture.Repository.UpsertAsync(media, TestContext.Current.CancellationToken);
        await fixture.Repository.SaveIdentityAsync(media.Id, identity, TestContext.Current.CancellationToken);

        var recovered = await fixture.Reconciliation.ReconcileAsync(
            new ReconcileFileCommand(rootId, @"C:\Media\after.mkv", identity),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReconciliationDecision.Exact, recovered.Decision);
        Assert.Equal(media.Id, recovered.MediaFileId);
        Assert.True(recovered.PreservesProgress);
        Assert.Equal(1, await CountMediaAsync(fixture.Factory));
    }

    [Fact]
    public async Task Unknown_manual_candidate_is_rejected_without_changing_the_old_path()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var rootId = new LibraryRootId(Guid.NewGuid());
        var media = Media(rootId, @"R:\Media\before.mkv", 21);
        var identity = new FileIdentity(null, null, "sha256:v1:KNOWN");
        await fixture.Repository.UpsertAsync(media, TestContext.Current.CancellationToken);
        await fixture.Repository.SaveIdentityAsync(media.Id, identity, TestContext.Current.CancellationToken);
        var command = new ReconcileFileCommand(rootId, @"T:\Media\after.mkv", identity);

        var viewModel = new ManualReassignmentViewModel(fixture.Reconciliation);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.ConfirmAsync(TestContext.Current.CancellationToken));
        viewModel.Review(command, new MediaFileId(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.ConfirmAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(await fixture.Repository.FindByPathAsync(
            rootId,
            media.Path,
            TestContext.Current.CancellationToken));
        Assert.Null(await fixture.Repository.FindByPathAsync(
            rootId,
            command.NewPath,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Device_loss_and_reconnect_under_a_new_path_preserves_one_available_entity()
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var rootId = new LibraryRootId(Guid.NewGuid());
        var media = Media(rootId, @"R:\Shows\episode.mkv", 3);
        var identity = new FileIdentity(null, null, "sha256:v1:EPISODE");
        await fixture.Repository.UpsertAsync(media, TestContext.Current.CancellationToken);
        await fixture.Repository.SaveIdentityAsync(media.Id, identity, TestContext.Current.CancellationToken);

        await fixture.Reconciliation.MarkRootUnavailableAsync(rootId, TestContext.Current.CancellationToken);
        var unavailable = await fixture.Repository.FindByPathAsync(
            rootId,
            media.Path,
            TestContext.Current.CancellationToken);
        Assert.False(unavailable?.IsAvailable);

        var recovered = await fixture.Reconciliation.ReconcileAsync(
            new ReconcileFileCommand(rootId, @"T:\Shows\episode.mkv", identity),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReconciliationDecision.Exact, recovered.Decision);
        Assert.Equal(media.Id, recovered.MediaFileId);
        Assert.True(recovered.PreservesProgress);
        var available = await fixture.Repository.FindByPathAsync(
            rootId,
            @"T:\Shows\episode.mkv",
            TestContext.Current.CancellationToken);
        Assert.True(available?.IsAvailable);
        Assert.Equal(1, await CountMediaAsync(fixture.Factory));
    }

    [Theory]
    [InlineData(RootKind.Local, @"C:\Media\film.mkv", @"C:\Archive\film.mkv")]
    [InlineData(RootKind.Usb, @"R:\Media\film.mkv", @"T:\Media\film.mkv")]
    [InlineData(RootKind.Unc, @"\\nas-old\Media\film.mkv", @"\\nas-new\Media\film.mkv")]
    public async Task Local_USB_and_UNC_reconnection_matrix_preserves_one_entity(
        RootKind rootKind,
        string oldPath,
        string newPath)
    {
        using var directory = new DatabaseTestDirectory();
        var fixture = await CreateFixtureAsync(directory.DatabasePath);
        var rootId = new LibraryRootId(Guid.NewGuid());
        var media = Media(rootId, oldPath, (int)rootKind + 10);
        var identity = new FileIdentity(null, null, $"sha256:v1:{rootKind}");
        await fixture.Repository.UpsertAsync(media, TestContext.Current.CancellationToken);
        await fixture.Repository.SaveIdentityAsync(media.Id, identity, TestContext.Current.CancellationToken);

        await fixture.Reconciliation.MarkRootUnavailableAsync(rootId, TestContext.Current.CancellationToken);
        var recovered = await fixture.Reconciliation.ReconcileAsync(
            new ReconcileFileCommand(rootId, newPath, identity),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReconciliationDecision.Exact, recovered.Decision);
        Assert.Equal(media.Id, recovered.MediaFileId);
        Assert.Null(await fixture.Repository.FindByPathAsync(
            rootId,
            oldPath,
            TestContext.Current.CancellationToken));
        Assert.True((await fixture.Repository.FindByPathAsync(
            rootId,
            newPath,
            TestContext.Current.CancellationToken))?.IsAvailable);
        Assert.Equal(1, await CountMediaAsync(fixture.Factory));
    }

    private static readonly TechnicalMetadata Metadata = new(
        TimeSpan.FromMinutes(42),
        "matroska",
        ["h264"],
        ["aac"],
        1920,
        1080);

    private static async Task<ReconciliationFixture> CreateFixtureAsync(string databasePath)
    {
        var factory = new SqliteConnectionFactory(databasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new MediaFileRepository(factory);
        return new ReconciliationFixture(
            factory,
            repository,
            new ReconcileScanResults(repository, new FileReconciliationPolicy()));
    }

    private static MediaFile Media(LibraryRootId rootId, string path, int seed) => new(
        new MediaFileId(CreateGuid(seed)),
        rootId,
        path,
        1_000 + seed,
        DateTimeOffset.UnixEpoch.AddDays(seed),
        Metadata,
        IsAvailable: true);

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static async Task<long> CountMediaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM media_files;";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private sealed record ReconciliationFixture(
        SqliteConnectionFactory Factory,
        MediaFileRepository Repository,
        ReconcileScanResults Reconciliation);

    private static Type? RequireType(string fullName, string assemblyName) =>
        Assembly.Load(assemblyName).GetType(fullName, throwOnError: false);

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L) == 1L;
    }
}
