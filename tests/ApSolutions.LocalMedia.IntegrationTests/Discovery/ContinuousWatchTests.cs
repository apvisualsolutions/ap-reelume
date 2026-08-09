using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Time;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

/// <summary>
/// The end-to-end walk LIB-002/003 demand: the background host starts watching with the
/// application, the startup pass catalogues what is already there, and a file dropped afterwards is
/// catalogued by the watcher without anybody pressing anything. Before WP-2 the whole slice was
/// registered and never started.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ContinuousWatchTests
{
    [Fact]
    public async Task A_file_dropped_into_a_watched_root_is_catalogued_without_anybody_asking()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaPath = Path.Combine(directory.Path, "watched");
        Directory.CreateDirectory(mediaPath);
        await File.WriteAllBytesAsync(
            Path.Combine(mediaPath, "already-there.mkv"),
            [0x41],
            TestContext.Current.CancellationToken);

        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            mediaPath,
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Startup | ScanPolicy.Continuous);
        await roots.AddAsync(root, TestContext.Current.CancellationToken);
        var mediaFiles = new MediaFileRepository(factory);
        var coordinator = new RootWatchCoordinator(
            new DebouncedFileWatcher(new SystemClock(), TimeSpan.FromMilliseconds(150)),
            new FallbackScanScheduler(new SystemClock()),
            new ScanCoordinator(
                roots,
                mediaFiles,
                new MediaFileEnumerator(),
                new StubProbe(),
                new InProcessApplicationEventPublisher()));
        using var background = new RootWatchBackground(roots, coordinator);

        background.Start();
        await WaitForAsync(
            () => CountMediaAsync(factory),
            expected: 1,
            "the startup scan never catalogued the file that was already there");

        await File.WriteAllBytesAsync(
            Path.Combine(mediaPath, "dropped-later.mkv"),
            [0x50],
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => CountMediaAsync(factory),
            expected: 2,
            "the watcher never catalogued the file dropped after watching began");

        background.Stop();
    }

    private static async Task WaitForAsync(Func<Task<long>> read, long expected, string complaint)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        long observed = -1;
        while (DateTimeOffset.UtcNow < deadline)
        {
            observed = await read();
            if (observed == expected)
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"{complaint} (saw {observed}, wanted {expected})");
    }

    private static async Task<long> CountMediaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM media_files;";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private sealed class StubProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(1),
                "mkv",
                ["h264"],
                ["aac"],
                1920,
                1080));
        }
    }
}
