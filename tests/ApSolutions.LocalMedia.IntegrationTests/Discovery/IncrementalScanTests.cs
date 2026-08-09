using System.Reflection;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

public sealed class IncrementalScanTests
{
    private static readonly string[] SupportedExtensions =
        [".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".ts", ".m2ts"];

    [Fact]
    public async Task Scan_schema_and_adapters_are_owned_by_the_incremental_scan_slice()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = DatabaseTestHarness.CreateFactory(directory.DatabasePath);
        await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));
        var repository = RequireType(
            "ApSolutions.LocalMedia.Infrastructure.Data.Repositories.MediaFileRepository");
        var enumerator = RequireType(
            "ApSolutions.LocalMedia.Infrastructure.FileSystem.MediaFileEnumerator");
        var probe = RequireType(
            "ApSolutions.LocalMedia.Infrastructure.Media.LibVlcMediaProbe");

        Assert.NotNull(repository.GetConstructor([factory.GetType()]));
        Assert.NotNull(enumerator.GetConstructor(Type.EmptyTypes));
        Assert.NotNull(probe.GetConstructor(Type.EmptyTypes));

        await using var connection = await DatabaseTestHarness.OpenAsync(factory);
        var tables = await ReadNamesAsync(connection);
        Assert.Contains("media_files", tables);
        Assert.Contains("scan_checkpoints", tables);
    }

    [Fact]
    public async Task Real_file_system_scan_filters_extensions_and_skips_unchanged_media()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaPath = Path.Combine(directory.Path, "media");
        Directory.CreateDirectory(mediaPath);
        foreach (var extension in SupportedExtensions)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(mediaPath, $"sample{extension}"),
                [0x41, 0x50],
                TestContext.Current.CancellationToken);
        }

        await File.WriteAllTextAsync(
            Path.Combine(mediaPath, "ignored.txt"),
            "must not be indexed",
            TestContext.Current.CancellationToken);
        var before = await ReadInventoryAsync(mediaPath);
        var fixture = await CreateFixtureAsync(directory.DatabasePath, mediaPath);

        var first = await fixture.Coordinator.StartAsync(
            new StartScanCommand(fixture.Root.Id, ScanTrigger.Initial, 3),
            TestContext.Current.CancellationToken);
        var second = await fixture.Coordinator.StartAsync(
            new StartScanCommand(fixture.Root.Id, ScanTrigger.Manual, 3),
            TestContext.Current.CancellationToken);

        Assert.Equal(8, first.EnumeratedCount);
        Assert.Equal(8, first.ProbeCount);
        Assert.Equal(8, first.MediaCount);
        Assert.Equal(8, second.EnumeratedCount);
        Assert.Equal(0, second.ProbeCount);
        Assert.Equal(8, second.UnchangedCount);
        Assert.Equal(8, await CountMediaAsync(fixture.Factory));
        Assert.Equal(before, await ReadInventoryAsync(mediaPath));
    }

    [Fact]
    public async Task Scan_handles_a_ten_thousand_item_corpus_in_bounded_batches()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaPath = Path.Combine(directory.Path, "corpus");
        Directory.CreateDirectory(mediaPath);
        for (var index = 0; index < 10_000; index++)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(mediaPath, $"item-{index:D5}.mkv"),
                [0x41],
                TestContext.Current.CancellationToken);
        }

        var fixture = await CreateFixtureAsync(directory.DatabasePath, mediaPath);
        var summary = await fixture.Coordinator.StartAsync(
            new StartScanCommand(fixture.Root.Id, ScanTrigger.Initial, 128),
            TestContext.Current.CancellationToken);

        Assert.Equal(10_000, summary.MediaCount);
        Assert.Equal(10_000, summary.ProbeCount);
        Assert.Equal(10_000, await CountMediaAsync(fixture.Factory));
        Assert.True(fixture.Publisher.Progress.Count >= 79);
    }

    [Fact]
    public async Task Real_probe_rejects_an_invalid_local_media_file_without_falling_back_to_a_stub()
    {
        using var directory = new DatabaseTestDirectory();
        var invalidMediaPath = Path.Combine(directory.Path, "invalid.mkv");
        await File.WriteAllTextAsync(
            invalidMediaPath,
            "not a media container",
            TestContext.Current.CancellationToken);
        var probe = new LibVlcMediaProbe();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => probe.ProbeAsync(invalidMediaPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Real_probe_reads_valid_media_consecutively_and_releases_quiesced_sessions()
    {
        using var directory = new DatabaseTestDirectory();
        var firstPath = Path.Combine(directory.Path, "first.wav");
        var secondPath = Path.Combine(directory.Path, "second.wav");
        var thirdPath = Path.Combine(directory.Path, "third.wav");
        await CreatePcmWaveAsync(firstPath);
        await CreatePcmWaveAsync(secondPath);
        await CreatePcmWaveAsync(thirdPath);
        var probe = new LibVlcMediaProbe();

        var first = await probe.ProbeAsync(firstPath, TestContext.Current.CancellationToken);
        var second = await probe.ProbeAsync(secondPath, TestContext.Current.CancellationToken);
        await Task.Delay(1_100, TestContext.Current.CancellationToken);
        var third = await probe.ProbeAsync(thirdPath, TestContext.Current.CancellationToken);
        await Task.Delay(1_100, TestContext.Current.CancellationToken);

        Assert.Equal("wav", first.Container);
        Assert.Equal("wav", second.Container);
        Assert.Equal("wav", third.Container);
        Assert.NotEmpty(first.AudioCodecs);
        Assert.NotEmpty(second.AudioCodecs);
        Assert.NotEmpty(third.AudioCodecs);
    }

    private static async Task CreatePcmWaveAsync(string path)
    {
        const int sampleRate = 8_000;
        const short channels = 1;
        const short bitsPerSample = 16;
        var dataLength = sampleRate * channels * (bitsPerSample / 8);
        using var stream = new MemoryStream(44 + dataLength);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8));
            writer.Write((short)(channels * (bitsPerSample / 8)));
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
            writer.Write(new byte[dataLength]);
        }

        await File.WriteAllBytesAsync(path, stream.ToArray(), TestContext.Current.CancellationToken);
    }

    private static async Task<ScanFixture> CreateFixtureAsync(string databasePath, string mediaPath)
    {
        var factory = new SqliteConnectionFactory(databasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            mediaPath,
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);
        var rootRepository = new LibraryRootRepository(factory);
        await rootRepository.AddAsync(root, TestContext.Current.CancellationToken);
        var publisher = new RecordingPublisher();
        var coordinator = new ScanCoordinator(
            rootRepository,
            new MediaFileRepository(factory),
            new MediaFileEnumerator(),
            new FakeProbe(),
            publisher);
        return new ScanFixture(factory, root, coordinator, publisher);
    }

    private static async Task<long> CountMediaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM media_files;";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private static async Task<string[]> ReadInventoryAsync(string root)
    {
        var values = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(path);
            var hash = await System.Security.Cryptography.SHA256.HashDataAsync(
                stream,
                TestContext.Current.CancellationToken);
            values.Add($"{Path.GetRelativePath(root, path)}|{Convert.ToHexString(hash)}");
        }

        return [.. values];
    }

    private static Type RequireType(string fullName)
    {
        var type = Assembly.Load("ApSolutions.LocalMedia.Infrastructure").GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type;
    }

    private static async Task<IReadOnlyList<string>> ReadNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_schema
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private sealed record ScanFixture(
        SqliteConnectionFactory Factory,
        LibraryRoot Root,
        ScanCoordinator Coordinator,
        RecordingPublisher Publisher);

    private sealed class FakeProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(1),
                "fake",
                ["h264"],
                ["aac"],
                1920,
                1080));
        }
    }

    private sealed class RecordingPublisher : IApplicationEventPublisher
    {
        public List<ScanProgressChanged> Progress { get; } = [];

        public Task PublishAsync<TEvent>(
            TEvent applicationEvent,
            CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (applicationEvent is ScanProgressChanged progress)
            {
                Progress.Add(progress);
            }

            return Task.CompletedTask;
        }
    }
}
