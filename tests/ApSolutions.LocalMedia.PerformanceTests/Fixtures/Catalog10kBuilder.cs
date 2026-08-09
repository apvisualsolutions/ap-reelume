using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.PerformanceTests.Fixtures;

public sealed class Catalog10kBuilder : IAsyncDisposable
{
    public const int ItemCount = 10_000;
    public const int EpisodeCount = 6_000;
    public const int MovieCount = 4_000;
    public const int UnavailableCount = 1_000;
    public const int DuplicateCount = 500;

    private readonly string _directoryPath;

    private Catalog10kBuilder(
        string directoryPath,
        SqliteConnectionFactory factory,
        LibraryRoot root,
        IReadOnlyList<EnumeratedFile> scanItems)
    {
        _directoryPath = directoryPath;
        Factory = factory;
        Root = root;
        ScanItems = scanItems;
        Catalog = new CatalogRepository(factory);
    }

    public SqliteConnectionFactory Factory { get; }

    public LibraryRoot Root { get; }

    public IReadOnlyList<EnumeratedFile> ScanItems { get; }

    public CatalogRepository Catalog { get; }

    public string DatabasePath => Path.Combine(_directoryPath, "catalog-10k.db");

    public static async Task<Catalog10kBuilder> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "ApSolutions.LocalMedia.Performance",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var databasePath = Path.Combine(directoryPath, "catalog-10k.db");
        var factory = new SqliteConnectionFactory(databasePath);
        await new MigrationRunner(factory).MigrateAsync(cancellationToken).ConfigureAwait(false);
        var root = new LibraryRoot(
            new LibraryRootId(CreateGuid(200_000)),
            @"C:\Performance\Media",
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Startup | ScanPolicy.Manual | ScanPolicy.Continuous);
        await new LibraryRootRepository(factory).AddAsync(root, cancellationToken).ConfigureAwait(false);

        var scanItems = await PopulateAsync(factory, root, cancellationToken).ConfigureAwait(false);
        return new Catalog10kBuilder(directoryPath, factory, root, scanItems);
    }

    public IScanCoordinator CreateUnchangedScanCoordinator() =>
        CreateScanCoordinator(new BufferedMediaFileEnumerator(ScanItems));

    /// <summary>
    /// The same coordinator with a different way of listing files, so a suite can measure what a slow
    /// or hostile source does to a scan without rebuilding the corpus.
    /// </summary>
    public IScanCoordinator CreateScanCoordinator(IMediaFileEnumerator enumerator) => new ScanCoordinator(
        new LibraryRootRepository(Factory),
        new MediaFileRepository(Factory),
        enumerator,
        new UnexpectedProbe(),
        new NullPublisher());

    public async Task<CorpusStatistics> ReadStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await Factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new CorpusStatistics(
            await ScalarAsync(connection, "SELECT COUNT(*) FROM media_files;", cancellationToken),
            await ScalarAsync(connection, "SELECT COUNT(*) FROM episodes;", cancellationToken),
            await ScalarAsync(connection, "SELECT COUNT(*) FROM titles WHERE kind = 0;", cancellationToken),
            await ScalarAsync(connection, "SELECT COUNT(*) FROM media_files WHERE is_available = 0;", cancellationToken),
            await ScalarAsync(
                connection,
                "SELECT COALESCE(SUM(copies - 1), 0) FROM (SELECT COUNT(*) copies FROM titles GROUP BY primary_title HAVING copies > 1);",
                cancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Factory.Dispose();
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private static async Task<IReadOnlyList<EnumeratedFile>> PopulateAsync(
        SqliteConnectionFactory factory,
        LibraryRoot root,
        CancellationToken cancellationToken)
    {
        await using var connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await using var title = CreateTitleCommand(connection, transaction);
        await using var media = CreateMediaCommand(connection, transaction);
        await using var season = CreateSeasonCommand(connection, transaction);
        await using var episode = CreateEpisodeCommand(connection, transaction);
        var items = new EnumeratedFile[ItemCount];
        for (var index = 0; index < ItemCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isEpisode = index < EpisodeCount;
            var isAvailable = index % 10 != 0;
            var titleId = CreateGuid(index + 1);
            var displayTitle = TitleFor(index, isEpisode);
            var addedUtc = DateTimeOffset.UnixEpoch.AddMinutes(index);
            SetTitleParameters(title, titleId, isEpisode, displayTitle, addedUtc, isAvailable, index);
            await title.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            var path = $@"C:\Performance\Media\item-{index:D5}{(index % 20 == 19 ? "-duplicate" : string.Empty)}.mkv";
            var size = 1_000_000L + index;
            var lastWriteUtc = DateTimeOffset.UnixEpoch.AddSeconds(index);
            SetMediaParameters(media, index, root.Id, path, size, lastWriteUtc, isAvailable);
            await media.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            items[index] = new EnumeratedFile(path, size, lastWriteUtc);

            if (isEpisode)
            {
                SetSeasonParameters(season, titleId, index);
                await season.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                SetEpisodeParameters(episode, titleId, index, displayTitle, isAvailable);
                await episode.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return items;
    }

    private static SqliteCommand CreateTitleCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO titles (
                id, kind, primary_title, sort_title, release_year, added_utc,
                last_played_utc, has_progress, is_personal, is_available)
            VALUES ($id, $kind, $title, $sortTitle, $year, $addedUtc, $lastPlayedUtc, $hasProgress, $isPersonal, $isAvailable);
            """;
        AddParameters(command, "$id", "$kind", "$title", "$sortTitle", "$year", "$addedUtc",
            "$lastPlayedUtc", "$hasProgress", "$isPersonal", "$isAvailable");
        return command;
    }

    private static SqliteCommand CreateMediaCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO media_files (
                id, library_root_id, normalized_path, size_bytes, last_write_utc,
                duration_ticks, container, video_codecs, audio_codecs, width, height, is_available)
            VALUES (
                $id, $rootId, $path, $size, $lastWriteUtc,
                $duration, 'matroska', '["h264"]', '["aac"]', 1920, 1080, $isAvailable);
            """;
        AddParameters(command, "$id", "$rootId", "$path", "$size", "$lastWriteUtc", "$duration", "$isAvailable");
        return command;
    }

    private static SqliteCommand CreateSeasonCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO seasons (show_id, season_number, title) VALUES ($showId, $season, $title);";
        AddParameters(command, "$showId", "$season", "$title");
        return command;
    }

    private static SqliteCommand CreateEpisodeCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO episodes (
                id, show_id, season_number, episode_number, absolute_number,
                title, sort_order, is_available)
            VALUES ($id, $showId, $season, 1, $absolute, $title, $sortOrder, $isAvailable);
            """;
        AddParameters(command, "$id", "$showId", "$season", "$absolute", "$title", "$sortOrder", "$isAvailable");
        return command;
    }

    private static void SetTitleParameters(
        SqliteCommand command,
        Guid titleId,
        bool isEpisode,
        string title,
        DateTimeOffset addedUtc,
        bool isAvailable,
        int index)
    {
        command.Parameters["$id"].Value = titleId.ToString("D");
        command.Parameters["$kind"].Value = isEpisode ? 1 : 0;
        command.Parameters["$title"].Value = title;
        command.Parameters["$sortTitle"].Value = title;
        command.Parameters["$year"].Value = 1980 + (index % 46);
        command.Parameters["$addedUtc"].Value = FormatDate(addedUtc);
        command.Parameters["$lastPlayedUtc"].Value = index % 4 == 0
            ? FormatDate(addedUtc.AddDays(1))
            : DBNull.Value;
        command.Parameters["$hasProgress"].Value = index % 5 == 0 ? 1 : 0;
        command.Parameters["$isPersonal"].Value = index % 7 == 0 ? 1 : 0;
        command.Parameters["$isAvailable"].Value = isAvailable ? 1 : 0;
    }

    private static void SetMediaParameters(
        SqliteCommand command,
        int index,
        LibraryRootId rootId,
        string path,
        long size,
        DateTimeOffset lastWriteUtc,
        bool isAvailable)
    {
        command.Parameters["$id"].Value = CreateGuid(index + 100_001).ToString("D");
        command.Parameters["$rootId"].Value = rootId.Value.ToString("D");
        command.Parameters["$path"].Value = path;
        command.Parameters["$size"].Value = size;
        command.Parameters["$lastWriteUtc"].Value = FormatDate(lastWriteUtc);
        command.Parameters["$duration"].Value = TimeSpan.FromMinutes(20 + (index % 100)).Ticks;
        command.Parameters["$isAvailable"].Value = isAvailable ? 1 : 0;
    }

    private static void SetSeasonParameters(SqliteCommand command, Guid showId, int index)
    {
        command.Parameters["$showId"].Value = showId.ToString("D");
        command.Parameters["$season"].Value = 1;
        command.Parameters["$title"].Value = $"Temporada {(index % 20) + 1}";
    }

    private static void SetEpisodeParameters(
        SqliteCommand command,
        Guid showId,
        int index,
        string title,
        bool isAvailable)
    {
        command.Parameters["$id"].Value = CreateGuid(index + 300_001).ToString("D");
        command.Parameters["$showId"].Value = showId.ToString("D");
        command.Parameters["$season"].Value = 1;
        command.Parameters["$absolute"].Value = index + 1;
        command.Parameters["$title"].Value = title;
        command.Parameters["$sortOrder"].Value = index;
        command.Parameters["$isAvailable"].Value = isAvailable ? 1 : 0;
    }

    private static void AddParameters(SqliteCommand command, params string[] names)
    {
        foreach (var name in names)
        {
            command.Parameters.Add(new SqliteParameter(name, DBNull.Value));
        }
    }

    private static string TitleFor(int index, bool isEpisode)
    {
        var identityIndex = index % 20 == 19 ? index - 1 : index;
        if (identityIndex % 97 == 0)
        {
            return $"Amélie Ñandú 東京 {identityIndex:D5}";
        }

        return isEpisode
            ? $"Crónicas episodio {identityIndex:D5}"
            : $"Película {identityIndex:D5}";
    }

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static async Task<int> ScalarAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private sealed class BufferedMediaFileEnumerator(IReadOnlyList<EnumeratedFile> items)
        : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
            LibraryRoot root,
            string? afterPath,
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            var visible = items
                .Where(item => afterPath is null ||
                               string.Compare(item.Path, afterPath, StringComparison.OrdinalIgnoreCase) > 0)
                .ToArray();
            for (var offset = 0; offset < visible.Length; offset += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return visible.Skip(offset).Take(batchSize).ToArray();
            }
        }
    }

    private sealed class UnexpectedProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Unchanged item was unexpectedly probed: {path}");
    }

    private sealed class NullPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(
            TEvent applicationEvent,
            CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            _ = applicationEvent;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}

public sealed record CorpusStatistics(
    int MediaFiles,
    int Episodes,
    int Movies,
    int Unavailable,
    int Duplicates);

public sealed record PerformanceSampleSet(
    IReadOnlyList<double> SamplesMilliseconds,
    double MedianMilliseconds,
    double P95Milliseconds,
    double MaximumMilliseconds);

public static class PerformanceMeasurement
{
    public static async Task<PerformanceSampleSet> MeasureAsync(
        Func<Task> action,
        int repetitions = 5)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetitions);
        await action().ConfigureAwait(false);
        var samples = new double[repetitions];
        for (var index = 0; index < repetitions; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            await action().ConfigureAwait(false);
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        return Create(samples);
    }

    public static PerformanceSampleSet Measure(Action action, int repetitions, int warmups = 1)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetitions);
        ArgumentOutOfRangeException.ThrowIfNegative(warmups);
        for (var index = 0; index < warmups; index++)
        {
            action();
        }

        var samples = new double[repetitions];
        for (var index = 0; index < repetitions; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        return Create(samples);
    }

    private static PerformanceSampleSet Create(double[] samples)
    {
        var ordered = samples.Order().ToArray();
        var median = ordered[ordered.Length / 2];
        var p95Index = Math.Max(0, (int)Math.Ceiling(ordered.Length * 0.95) - 1);
        return new PerformanceSampleSet(samples, median, ordered[p95Index], ordered[^1]);
    }
}

public static class UiFrameBudgetProbe
{
    public static PerformanceSampleSet Measure(Action frame, int repetitions = 120) =>
        PerformanceMeasurement.Measure(frame, repetitions, warmups: 10);
}

public static class PerformanceEvidence
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static async Task WriteAsync(
        string metric,
        PerformanceSampleSet samples,
        double budgetMilliseconds,
        CancellationToken cancellationToken = default)
    {
        var directory = Environment.GetEnvironmentVariable("APSOLUTIONS_LOCALMEDIA_PERF_RESULTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var run = Environment.GetEnvironmentVariable("APSOLUTIONS_LOCALMEDIA_PERF_RUN") ?? "manual";
        var result = new
        {
            metric,
            run,
            unit = "ms",
            median = samples.MedianMilliseconds,
            p95 = samples.P95Milliseconds,
            maximum = samples.MaximumMilliseconds,
            budget = budgetMilliseconds,
            passed = samples.P95Milliseconds < budgetMilliseconds,
            samples = samples.SamplesMilliseconds,
        };
        var path = Path.Combine(directory, $"{run}-{metric}.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(result, SerializerOptions),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteExactAsync(
        string metric,
        double value,
        double expected,
        string unit,
        CancellationToken cancellationToken = default)
    {
        var directory = Environment.GetEnvironmentVariable("APSOLUTIONS_LOCALMEDIA_PERF_RESULTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var run = Environment.GetEnvironmentVariable("APSOLUTIONS_LOCALMEDIA_PERF_RUN") ?? "manual";
        var result = new
        {
            metric,
            run,
            unit,
            median = value,
            p95 = value,
            maximum = value,
            budget = expected,
            passed = value.Equals(expected),
            samples = new[] { value },
        };
        var path = Path.Combine(directory, $"{run}-{metric}.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(result, SerializerOptions),
            cancellationToken).ConfigureAwait(false);
    }
}
