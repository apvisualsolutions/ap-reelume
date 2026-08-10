// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Catalog;

public sealed class GroupMediaVersionsTests
{
    [Fact]
    public async Task Two_5x10_files_group_idempotently_and_keep_both_relationships()
    {
        var repository = new MemoryVersionRepository();
        var useCase = new GroupMediaVersions(repository);
        var versions = new[]
        {
            Version(1, TimeSpan.FromMinutes(50)),
            Version(2, TimeSpan.FromMinutes(50).Add(TimeSpan.FromSeconds(2))),
        };
        var command = new GroupMediaVersionsCommand("tv:show:s05e10", versions, ConfirmDifferentEditions: false);

        var first = await useCase.ExecuteAsync(command, TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(MediaVersionGroupingOutcome.Grouped, first.Outcome);
        Assert.Equal(first.Group?.Id, second.Group?.Id);
        Assert.Equal(2, Assert.Single(repository.Groups).Versions.Count);
        Assert.Equal(2, Assert.Single(repository.Groups).Versions.Select(version => version.MediaFileId).Distinct().Count());
    }

    [Fact]
    public async Task Materially_different_editions_require_confirmation_before_grouping()
    {
        var repository = new MemoryVersionRepository();
        var useCase = new GroupMediaVersions(repository);
        var versions = new[]
        {
            Version(1, TimeSpan.FromMinutes(50)),
            Version(2, TimeSpan.FromMinutes(110)),
        };

        var pending = await useCase.ExecuteAsync(
            new GroupMediaVersionsCommand("movie:cut", versions, ConfirmDifferentEditions: false),
            TestContext.Current.CancellationToken);
        Assert.Equal(MediaVersionGroupingOutcome.ConfirmationRequired, pending.Outcome);
        Assert.Null(pending.Group);
        Assert.Empty(repository.Groups);

        var confirmed = await useCase.ExecuteAsync(
            new GroupMediaVersionsCommand("movie:cut", versions, ConfirmDifferentEditions: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(MediaVersionGroupingOutcome.Grouped, confirmed.Outcome);
        Assert.Equal(2, confirmed.Group?.Versions.Count);
    }

    [Fact]
    public async Task Preferred_version_is_persisted_only_when_it_belongs_to_the_group()
    {
        var repository = new MemoryVersionRepository();
        var grouping = await new GroupMediaVersions(repository).ExecuteAsync(
            new GroupMediaVersionsCommand(
                "movie:arrival",
                [Version(1, TimeSpan.FromMinutes(116)), Version(2, TimeSpan.FromMinutes(116))],
                ConfirmDifferentEditions: false),
            TestContext.Current.CancellationToken);
        var group = Assert.IsType<MediaVersionGroup>(grouping.Group);
        var preferred = group.Versions[1].MediaFileId;
        var useCase = new SetPreferredVersion(repository);

        var updated = await useCase.ExecuteAsync(
            new SetPreferredVersionCommand(group.Id, preferred),
            TestContext.Current.CancellationToken);

        Assert.Equal(preferred, updated.PreferredMediaFileId);
        Assert.Equal(preferred, Assert.Single(repository.Groups).PreferredMediaFileId);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            new SetPreferredVersionCommand(group.Id, new MediaFileId(Guid.NewGuid())),
            TestContext.Current.CancellationToken));
    }

    private static MediaVersion Version(int seed, TimeSpan duration)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new MediaVersion(
            new MediaFileId(new Guid(bytes)),
            $"C:\\Media\\Show.5x10.{seed}.mkv",
            IsAvailable: true,
            duration,
            Width: 1920,
            Height: 1080,
            IsHdr: false,
            VideoCodec: "H264",
            SizeBytes: seed * 1000L);
    }

    private sealed class MemoryVersionRepository : IMediaVersionGroupRepository
    {
        public List<MediaVersionGroup> Groups { get; } = [];

        public Task<MediaVersionGroup?> FindByContentKeyAsync(string contentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Groups.SingleOrDefault(group => group.ContentKey == contentKey));

        public Task<MediaVersionGroup?> FindByIdAsync(MediaVersionId groupId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Groups.SingleOrDefault(group => group.Id == groupId));

        public Task<MediaVersionGroup?> FindByMemberAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Groups.SingleOrDefault(group =>
                group.Versions.Any(version => version.MediaFileId == mediaFileId)));

        public Task SaveAsync(MediaVersionGroup group, CancellationToken cancellationToken = default)
        {
            Groups.RemoveAll(existing => existing.Id == group.Id || existing.ContentKey == group.ContentKey);
            Groups.Add(group);
            return Task.CompletedTask;
        }
    }
}
