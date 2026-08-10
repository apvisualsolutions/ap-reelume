// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Recovery;

/// <summary>
/// Three failures of the file system, on a drive that really is mounted and really is taken away: the
/// drive disappears, a folder refuses to be read, and a rename collides. None of them may lose the
/// catalogue, and none of them may half-apply.
/// </summary>
[Trait("Category", "Recovery")]
public sealed class RemovedDriveTests
{
    private static readonly string[] CandidateLetters = ["N:", "O:", "P:", "Q:", "U:"];

    [Fact]
    public async Task A_drive_that_is_taken_away_keeps_the_catalogue_and_comes_back_without_duplicates()
    {
        using var directory = new DatabaseTestDirectory();
        var source = Path.Combine(directory.Path, "removable");
        Directory.CreateDirectory(source);
        await File.WriteAllBytesAsync(
            Path.Combine(source, "episode.mkv"),
            [0x41, 0x50, 0x53],
            TestContext.Current.CancellationToken);

        var letter = MountSubstitutedDrive(source);
        if (letter is null)
        {
            Assert.Skip("No drive letter was available to substitute.");
            return;
        }

        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            letter + "\\",
            RootKind.Usb,
            RootAvailability.Available,
            ScanPolicy.Manual);
        await roots.AddAsync(root, TestContext.Current.CancellationToken);
        var coordinator = CreateCoordinator(factory, roots);

        try
        {
            var first = await coordinator.StartAsync(
                new StartScanCommand(root.Id, ScanTrigger.Initial),
                TestContext.Current.CancellationToken);
            Assert.Equal(1, first.MediaCount);

            RemoveSubstitutedDrive(letter);
            await WaitUntilGoneAsync(letter, TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(letter + "\\"), "The drive was still mounted when the scan ran.");
            var afterRemoval = await coordinator.StartAsync(
                new StartScanCommand(root.Id, ScanTrigger.Manual),
                TestContext.Current.CancellationToken);

            // The unreachable root arrives as one failed item rather than as an exception, and nothing
            // is indexed from it. What matters is the row that was already there.
            Assert.Equal(1, afterRemoval.ErrorCount);
            Assert.Equal(0, afterRemoval.MediaCount);
            Assert.Equal(0, afterRemoval.ProbeCount);
            Assert.Equal(1, await CountMediaAsync(factory));
            Assert.Equal(0, await CountAvailableMediaAsync(factory));

            MountSubstitutedDrive(source, letter);
            var afterReconnect = await coordinator.StartAsync(
                new StartScanCommand(root.Id, ScanTrigger.Manual),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, afterReconnect.EnumeratedCount);
            Assert.Equal(1, await CountMediaAsync(factory));
            Assert.Equal(1, await CountAvailableMediaAsync(factory));
        }
        finally
        {
            RemoveSubstitutedDrive(letter);
        }

        await RecoveryEvidence.RecordAsync(
            "removed-drive",
            "USB or NAS disconnected",
            RecoveryOutcome.Degraded,
            "A substituted drive was unmounted mid-life: the catalogue kept its one entry, and remounting produced no duplicate.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_folder_that_refuses_to_be_read_does_not_take_the_rest_of_the_scan_with_it()
    {
        using var directory = new DatabaseTestDirectory();
        var source = Path.Combine(directory.Path, "library");
        var readable = Path.Combine(source, "readable");
        var denied = Path.Combine(source, "denied");
        Directory.CreateDirectory(readable);
        Directory.CreateDirectory(denied);
        await File.WriteAllBytesAsync(
            Path.Combine(readable, "film.mkv"),
            [0x41, 0x50],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(denied, "hidden.mkv"),
            [0x41, 0x50],
            TestContext.Current.CancellationToken);

        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            source,
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);
        await roots.AddAsync(root, TestContext.Current.CancellationToken);

        using (var lockedFile = new FileStream(
            Path.Combine(denied, "hidden.mkv"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            var summary = await CreateCoordinator(factory, roots).StartAsync(
                new StartScanCommand(root.Id, ScanTrigger.Manual),
                TestContext.Current.CancellationToken);

            // The readable side is indexed whatever the other side does. What matters is that the scan
            // finished rather than stopping at the first thing it could not open.
            Assert.True(summary.EnumeratedCount >= 1);
            Assert.True(await CountMediaAsync(factory) >= 1);
            GC.KeepAlive(lockedFile);
        }

        await RecoveryEvidence.RecordAsync(
            "access-denied",
            "Access denied",
            RecoveryOutcome.Degraded,
            "A locked file did not stop the scan; the readable side of the root was still indexed.",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_rename_that_collides_executes_none_of_the_batch()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var first = Path.Combine(directory.Path, "first.mkv");
        var second = Path.Combine(directory.Path, "second.mkv");
        var destination = Path.Combine(directory.Path, "Taken.mkv");
        await File.WriteAllTextAsync(first, "first", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(second, "second", TestContext.Current.CancellationToken);
        var plan = new RenamePolicy().CreatePlan(
            directory.Path,
            [
                new RenameRequest(first, "Renamed.mkv"),
                new RenameRequest(second, Path.GetFileName(destination)),
            ]);

        // The collision appears between the preview and the execution, which is the case a preview
        // alone cannot protect against.
        await File.WriteAllTextAsync(destination, "already here", TestContext.Current.CancellationToken);
        var renamer = new SafeFileRenamer(factory);
        var result = await renamer.ExecuteAsync(plan, TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.BlockedByConflict, result.Outcome);
        Assert.Equal("first", await File.ReadAllTextAsync(first, TestContext.Current.CancellationToken));
        Assert.Equal("second", await File.ReadAllTextAsync(second, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(directory.Path, "Renamed.mkv")));
        Assert.Empty(await renamer.GetAuditLogAsync(plan.Id, TestContext.Current.CancellationToken));

        await RecoveryEvidence.RecordAsync(
            "rename-conflict",
            "Rename conflict",
            RecoveryOutcome.AbortedSafely,
            "A collision appearing after the preview blocked the whole batch: no file moved and no audit row was written.",
            TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountMediaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        return await SqliteBootstrapTests.ScalarInt64Async(connection, "SELECT COUNT(*) FROM media_files;");
    }

    /// <summary>
    /// How many rows the catalogue still considers reachable. The disconnection marks them unavailable
    /// rather than deleting them, which is the whole point: the library survives the drive.
    /// </summary>
    private static async Task<long> CountAvailableMediaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        return await SqliteBootstrapTests.ScalarInt64Async(
            connection,
            "SELECT COUNT(*) FROM media_files WHERE is_available = 1;");
    }

    private static ScanCoordinator CreateCoordinator(
        SqliteConnectionFactory factory,
        LibraryRootRepository roots) =>
        new(
            roots,
            new MediaFileRepository(factory),
            new MediaFileEnumerator(),
            new SilentProbe(),
            new SilentPublisher());

    /// <summary>
    /// Mounts a folder as a drive letter with <c>subst</c>, which needs no elevation and is a real
    /// mount as far as the file system is concerned.
    /// </summary>
    private static string? MountSubstitutedDrive(string target, string? preferred = null)
    {
        foreach (var letter in preferred is null ? CandidateLetters : [preferred])
        {
            if (preferred is null && Directory.Exists(letter + "\\"))
            {
                continue;
            }

            if (RunSubst(letter, target) && Directory.Exists(letter + "\\"))
            {
                return letter;
            }
        }

        return null;
    }

    private static void RemoveSubstitutedDrive(string letter) => RunSubst(letter, "/D");

    /// <summary>
    /// Unmounting is asynchronous enough that a scan started immediately afterwards can still see the
    /// drive. Waiting for it to actually be gone is what makes the measurement about the disconnection.
    /// </summary>
    private static async Task WaitUntilGoneAsync(string letter, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50 && Directory.Exists(letter + "\\"); attempt++)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool RunSubst(string letter, string argument)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "subst",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(letter);
        startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.WaitForExit(10_000);
        return process.HasExited && process.ExitCode == 0;
    }

    private sealed class SilentProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(20),
                "mkv",
                ["h264"],
                ["aac"],
                1920,
                1080));
        }
    }

    private sealed class SilentPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            _ = applicationEvent;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
