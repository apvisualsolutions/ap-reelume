// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Catalog;

/// <summary>
/// The end-to-end walk LIB-008 demands: two real copies of the same film scanned into a real SQLite
/// catalogue form one version group without anybody's intervention, the group is reachable from
/// either copy, a pinned preference survives the next scan, and no file is deleted or hidden.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ScanVersionGroupingTests
{
    [Fact]
    public async Task Two_scanned_copies_form_one_group_on_their_own_and_a_preference_survives_a_rescan()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaPath = Path.Combine(directory.Path, "media", "Movies");
        Directory.CreateDirectory(mediaPath);
        var firstPath = Path.Combine(mediaPath, "Dune.2021.1080p.mkv");
        var secondPath = Path.Combine(mediaPath, "Dune.2021.2160p.mkv");
        await File.WriteAllBytesAsync(firstPath, [0x41, 0x50], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(secondPath, [0x41, 0x50, 0x51], TestContext.Current.CancellationToken);
        var inventoryBefore = Directory.GetFiles(mediaPath).Order(StringComparer.Ordinal).ToArray();

        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var roots = new LibraryRootRepository(factory);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            Path.Combine(directory.Path, "media"),
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);
        await roots.AddAsync(root, TestContext.Current.CancellationToken);
        var mediaFiles = new MediaFileRepository(factory);
        var groups = new MediaVersionGroupRepository(factory);
        var coordinator = new ScanCoordinator(
            roots,
            mediaFiles,
            new MediaFileEnumerator(),
            new StubProbe(),
            new InProcessApplicationEventPublisher());
        var grouping = new GroupScannedVersions(
            roots,
            mediaFiles,
            groups,
            new MediaNameParser(),
            new DuplicateGroupingPolicy(),
            new GroupMediaVersions(groups));

        var summary = await coordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Initial, 16),
            TestContext.Current.CancellationToken);
        var result = await grouping.ExecuteAsync(summary, TestContext.Current.CancellationToken);

        Assert.Equal(new GroupScannedVersionsResult(1, 0), result);
        var first = await mediaFiles.FindByPathAsync(root.Id, firstPath, TestContext.Current.CancellationToken);
        var second = await mediaFiles.FindByPathAsync(root.Id, secondPath, TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(second);

        // Reachable from either copy, not only from the one whose key the group happens to carry.
        var fromFirst = await groups.FindByMemberAsync(first.Id, TestContext.Current.CancellationToken);
        var fromSecond = await groups.FindByMemberAsync(second.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(fromFirst);
        Assert.NotNull(fromSecond);
        Assert.Equal(fromFirst.Id, fromSecond.Id);
        Assert.Equal(2, fromFirst.Versions.Count);

        // A person pins a version; the next scan must keep the pin instead of regrouping it away.
        _ = await new SetPreferredVersion(groups).ExecuteAsync(
            new SetPreferredVersionCommand(fromFirst.Id, second.Id),
            TestContext.Current.CancellationToken);
        var rescan = await coordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Manual, 16),
            TestContext.Current.CancellationToken);
        _ = await grouping.ExecuteAsync(rescan, TestContext.Current.CancellationToken);

        var regrouped = await groups.FindByMemberAsync(first.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(regrouped);
        Assert.Equal(second.Id, regrouped.PreferredMediaFileId);
        Assert.Equal(2, regrouped.Versions.Count);

        // No file was deleted, hidden, or rewritten by any of it.
        Assert.Equal(inventoryBefore, Directory.GetFiles(mediaPath).Order(StringComparer.Ordinal).ToArray());
    }

    private sealed class StubProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(155),
                "mkv",
                ["h264"],
                ["aac"],
                path.Contains("2160", StringComparison.Ordinal) ? 3840 : 1920,
                path.Contains("2160", StringComparison.Ordinal) ? 2160 : 1080));
        }
    }
}
