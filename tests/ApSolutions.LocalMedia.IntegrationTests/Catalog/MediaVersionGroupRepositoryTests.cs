// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Catalog;

/// <summary>
/// Two copies of the same film, and which one a person pinned.
/// <para>
/// No version is ever deleted or hidden here; a group only records that they belong together and
/// which one the selection policy should prefer when both are available.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class MediaVersionGroupRepositoryTests
{
    private static readonly MediaVersionId GroupId = new(new Guid("bbbbbbbb-0000-0000-0000-000000000001"));
    private static readonly MediaFileId Uhd = new(new Guid("bbbbbbbb-0000-0000-0000-000000000002"));
    private static readonly MediaFileId Hd = new(new Guid("bbbbbbbb-0000-0000-0000-000000000003"));
    private const string ContentKeyValue = "title:aaaaaaaa-0000-0000-0000-000000000001";

    [Fact]
    public async Task An_unknown_content_key_and_an_unknown_group_both_answer_nothing()
    {
        await using var fixture = await GroupFixture.CreateAsync();

        Assert.Null(await fixture.Repository.FindByContentKeyAsync(
            ContentKeyValue,
            TestContext.Current.CancellationToken));
        Assert.Null(await fixture.Repository.FindByIdAsync(GroupId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Every_version_comes_back_in_the_order_it_was_stored()
    {
        await using var fixture = await GroupFixture.CreateAsync();
        await fixture.Repository.SaveAsync(Group(preferred: Uhd), TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.FindByContentKeyAsync(
            ContentKeyValue,
            TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Equal(GroupId, stored!.Id);
        Assert.Equal(ContentKeyValue, stored.ContentKey);
        Assert.Equal(Uhd, stored.PreferredMediaFileId);
        Assert.Equal([Uhd, Hd], stored.Versions.Select(version => version.MediaFileId));
        var first = stored.Versions[0];
        Assert.Equal("R:\\media\\a.mkv", first.Path);
        Assert.True(first.IsAvailable);
        Assert.Equal(TimeSpan.FromMinutes(116), first.Duration);
        Assert.Equal(3840, first.Width);
        Assert.Equal(2160, first.Height);
        Assert.True(first.IsHdr);
        Assert.Equal("HEVC", first.VideoCodec);
        Assert.Equal(90, first.SizeBytes);
    }

    [Fact]
    public async Task A_version_the_probe_could_not_measure_keeps_its_absences()
    {
        await using var fixture = await GroupFixture.CreateAsync();
        var group = new MediaVersionGroup(
            GroupId,
            ContentKeyValue,
            [new MediaVersion(Uhd, "R:\\media\\a.mkv", false, null, null, null, false, "unknown", 0)],
            PreferredMediaFileId: null);

        await fixture.Repository.SaveAsync(group, TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.FindByIdAsync(GroupId, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        var only = Assert.Single(stored!.Versions);
        Assert.Null(only.Duration);
        Assert.Null(only.Width);
        Assert.Null(only.Height);
        Assert.False(only.IsAvailable);
        Assert.Null(stored.PreferredMediaFileId);
    }

    /// <summary>
    /// Saving again replaces the members as a set. A group that briefly held half its versions would
    /// let the selection policy prefer a version that is on its way out.
    /// </summary>
    [Fact]
    public async Task Saving_again_replaces_the_members_instead_of_adding_to_them()
    {
        await using var fixture = await GroupFixture.CreateAsync();
        await fixture.Repository.SaveAsync(Group(preferred: Uhd), TestContext.Current.CancellationToken);

        var reduced = new MediaVersionGroup(
            GroupId,
            ContentKeyValue,
            [new MediaVersion(Hd, "R:\\media\\b.mkv", true, null, 1920, 1080, false, "H264", 40)],
            Hd);
        await fixture.Repository.SaveAsync(reduced, TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.FindByIdAsync(GroupId, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal([Hd], stored!.Versions.Select(version => version.MediaFileId));
        Assert.Equal(Hd, stored.PreferredMediaFileId);
    }

    [Fact]
    public async Task The_selection_policy_reads_a_stored_group_the_way_it_reads_any_other()
    {
        await using var fixture = await GroupFixture.CreateAsync();
        await fixture.Repository.SaveAsync(Group(preferred: Hd), TestContext.Current.CancellationToken);

        var stored = await fixture.Repository.FindByContentKeyAsync(
            ContentKeyValue,
            TestContext.Current.CancellationToken);
        var selection = new MediaVersionSelectionPolicy().Select(
            stored!,
            new MediaVersionPreferences(PreferHdr: true));

        Assert.Equal(Hd, selection.StoredPreferredMediaFileId);
        Assert.Equal(2, selection.VisibleFileIds.Count);
    }

    [Fact]
    public async Task Nothing_is_saved_or_looked_up_without_being_asked_properly()
    {
        await using var fixture = await GroupFixture.CreateAsync();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Repository.SaveAsync(
            null!,
            TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.FindByContentKeyAsync(
            "  ",
            TestContext.Current.CancellationToken));
        _ = Assert.Throws<ArgumentNullException>(() => new MediaVersionGroupRepository(null!));
    }

    private static MediaVersionGroup Group(MediaFileId preferred) => new(
        GroupId,
        ContentKeyValue,
        [
            new MediaVersion(Uhd, "R:\\media\\a.mkv", true, TimeSpan.FromMinutes(116), 3840, 2160, true, "HEVC", 90),
            new MediaVersion(Hd, "R:\\media\\b.mkv", true, TimeSpan.FromMinutes(116), 1920, 1080, false, "H264", 40),
        ],
        preferred);

    private sealed class GroupFixture : IAsyncDisposable
    {
        private readonly DatabaseTestDirectory _directory;

        private GroupFixture(DatabaseTestDirectory directory, MediaVersionGroupRepository repository)
        {
            _directory = directory;
            Repository = repository;
        }

        public MediaVersionGroupRepository Repository { get; }

        public static async Task<GroupFixture> CreateAsync()
        {
            var directory = new DatabaseTestDirectory();
            var factory = new SqliteConnectionFactory(directory.DatabasePath);
            using (var runner = new MigrationRunner(factory))
            {
                await runner.MigrateAsync(CancellationToken.None);
            }

            return new GroupFixture(directory, new MediaVersionGroupRepository(factory));
        }

        public ValueTask DisposeAsync()
        {
            _directory.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
