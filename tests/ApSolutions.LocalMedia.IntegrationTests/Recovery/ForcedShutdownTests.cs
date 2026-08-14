// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using ApSolutions.LocalMedia.TestSupport;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Recovery;

/// <summary>
/// The first of the nine failures: the application is killed while it is playing. Nothing may be lost
/// beyond the last interval, and what survives has to be readable — a database that opens but has
/// forgotten the position is not recovery.
/// </summary>
[Trait("Category", "Recovery")]
[Collection(ChildProcessSuites.Name)]
public sealed class ForcedShutdownTests
{
    private const string DatabaseVariable = "AP_LOCALMEDIA_SHUTDOWN_DB";
    private const string SignalVariable = "AP_LOCALMEDIA_SHUTDOWN_SIGNAL";

    private static readonly TitleId Movie = new(Guid.Parse("cc000000-0000-4000-8000-000000000001"));
    private static readonly MediaFileId File1 = new(Guid.Parse("cc000000-0000-4000-8000-000000000002"));

    private static readonly string[] ProfilerVariables =
    [
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "CORECLR_PROFILER_PATH",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "COR_PROFILER_PATH",
    ];

    /// <summary>
    /// The identifier the writer signalled, or nothing while the signal cannot be read yet — because
    /// it is absent, still held open by the process writing it, or written but not yet complete.
    /// </summary>
    private static async Task<int?> TryReadSignalAsync(string signalPath)
    {
        try
        {
            var text = await File.ReadAllTextAsync(signalPath, TestContext.Current.CancellationToken);
            return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var identifier)
                ? identifier
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    [Fact]
    public async Task A_process_killed_mid_playback_keeps_the_position_it_had_written()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var signalPath = Path.Combine(directory.Path, "written.signal");

        var child = StartProgressWriter(directory.DatabasePath, signalPath);
        try
        {
            // The signal is waited for by reading it, not by watching it appear. A file exists from
            // the moment it is created and is finished some time later, and reading in between fails
            // with a sharing violation — which made this suite fail about one run in two.
            var deadline = DateTime.UtcNow.AddSeconds(60);
            int? signalled = null;
            while (signalled is null && DateTime.UtcNow < deadline && !child.HasExited)
            {
                signalled = await TryReadSignalAsync(signalPath);
                if (signalled is null)
                {
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                }
            }

            signalled ??= await TryReadSignalAsync(signalPath);
            if (signalled is null)
            {
                var output = await child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                var error = await child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                Assert.Fail($"The progress writer never wrote. stdout={output}; stderr={error}");
            }

            using var writer = Process.GetProcessById(signalled.Value);
            writer.Kill(entireProcessTree: true);
            await writer.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            child.Dispose();
        }

        SqliteConnection.ClearAllPools();
        var reopened = new SqliteConnectionFactory(directory.DatabasePath);
        await using (var connection = await reopened.OpenAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        }

        var restored = await new WatchStateRepository(reopened).GetAsync(
            ContentKey.ForTitle(Movie),
            TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.InRange(
            restored.Position,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(30));
        await RecoveryEvidence.RecordAsync(
            "forced-shutdown",
            "Unexpected shutdown while playing",
            RecoveryOutcome.Recoverable,
            $"Killed mid-playback; the database reopened clean and resumed at {restored.Position}.",
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Writes progress the way the application does and then waits to be killed. Only the child started
    /// above enters here.
    /// </summary>
    [Fact]
    public async Task Progress_writer_process_fixture()
    {
        var databasePath = Environment.GetEnvironmentVariable(DatabaseVariable);
        var signalPath = Environment.GetEnvironmentVariable(SignalVariable);
        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(signalPath))
        {
            return;
        }

        var factory = new SqliteConnectionFactory(databasePath);
        await using var tracker = new PlaybackProgressTracker(
            new WatchStateRepository(factory),
            new FixedClock(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)),
            interval: TimeSpan.FromMilliseconds(50));
        await tracker.BeginAsync(
            ContentKey.ForTitle(Movie),
            File1,
            TestContext.Current.CancellationToken);

        for (var second = 1; second <= 20; second++)
        {
            tracker.Observe(TimeSpan.FromSeconds(second * 30), TimeSpan.FromMinutes(45));
            await tracker.FlushAsync(PersistenceTrigger.Tick, TestContext.Current.CancellationToken);
        }

        await File.WriteAllTextAsync(
            signalPath,
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            TestContext.Current.CancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
    }

    private static Process StartProgressWriter(string databasePath, string signalPath)
    {
        var projectPath = Path.Combine(
            RepositoryLayout.Root,
            "tests",
            "ApSolutions.LocalMedia.IntegrationTests",
            "ApSolutions.LocalMedia.IntegrationTests.csproj");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add("FullyQualifiedName~Progress_writer_process_fixture");
        startInfo.Environment[DatabaseVariable] = databasePath;
        startInfo.Environment[SignalVariable] = signalPath;
        foreach (var profiling in ProfilerVariables)
        {
            startInfo.Environment[profiling] = string.Empty;
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the progress writer.");
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
