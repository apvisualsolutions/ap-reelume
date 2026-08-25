// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Review;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Review;

/// <summary>
/// The corners of the overview the assembled scenes never stand in: models built over nothing, a
/// load announced to nobody, and the set that changes nothing and must say nothing.
/// </summary>
public sealed class DuplicatesOverviewViewModelTests
{
    [Fact]
    public void Models_built_over_nothing_refuse_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new DuplicateGroupRowViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new DuplicatesOverviewViewModel(null!, Preferences()));
        Assert.Throws<ArgumentNullException>(() =>
            new DuplicatesOverviewViewModel(new GetDuplicateOverview(new SingleGroupReader()), null!));
        Assert.Throws<ArgumentNullException>(() =>
            new DuplicateFileRowViewModel(null!, NewModel().SetPreferredCommand));
    }

    /// <summary>
    /// Loaded once with nobody listening and once with a listener: the announcement's null half is
    /// a branch, and the thirteen forms of the house defect say a surface nobody hears about is a
    /// surface nobody sees.
    /// </summary>
    [Fact]
    public async Task A_load_is_safe_to_announce_to_nobody_and_heard_by_a_listener()
    {
        var first = NewModel();
        await first.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(first.HasGroups);
        Assert.False(first.IsEmpty);
        Assert.Equal("Arrival", Assert.Single(first.Groups).Title);

        var second = NewModel();
        var announced = new List<string>();
        second.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);
        await second.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Contains(nameof(second.Groups), announced);
        Assert.Contains(nameof(second.HasGroups), announced);
        Assert.Contains(nameof(second.IsEmpty), announced);
    }

    /// <summary>
    /// The set that changes nothing says nothing. Every public path hands the list a fresh
    /// instance, so the unchanged side of the comparison is reached the way the composition
    /// root's private seams are: by reflection, on the member itself.
    /// </summary>
    [Fact]
    public async Task Setting_the_same_list_again_announces_nothing()
    {
        var model = NewModel();
        await model.LoadAsync(TestContext.Current.CancellationToken);

        var announced = new List<string>();
        model.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        var groups = typeof(DuplicatesOverviewViewModel)
            .GetProperty(nameof(model.Groups), BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(groups);
        groups!.SetValue(model, model.Groups);

        Assert.Empty(announced);
    }

    /// <summary>
    /// The table the destination draws since 2026-08-25: a group carries its files, and each of them
    /// carries what a person compares copies by. A group that arrived without files would draw a
    /// header over nothing, which is the shape the destination had before — a count with no table.
    /// </summary>
    [Fact]
    public async Task A_group_carries_the_files_that_answer_to_it()
    {
        var model = NewModel();
        await model.LoadAsync(TestContext.Current.CancellationToken);

        var group = Assert.Single(model.Groups);
        Assert.True(group.HasFiles);
        Assert.Equal(2, group.Files.Count);

        var preferred = Assert.Single(group.Files, file => file.IsPreferred);
        Assert.Equal("...Arrival.2160p.mkv", preferred.ShortPath);
        Assert.Equal("3840 × 2160", preferred.Resolution);
        Assert.Equal("HEVC", preferred.VideoCodec);
        Assert.Equal("E-AC-3", preferred.AudioCodec);
        Assert.Equal("18,4 GB", preferred.Size.Replace('.', ','));
        Assert.Equal("1:56:00", preferred.Duration);
        Assert.True(preferred.IsAvailable);

        // The second copy is the absent one, and every column of it says so rather than being blank:
        // a row with no size and no running time is a row nobody can compare against the first.
        var other = Assert.Single(group.Files, file => !file.IsPreferred);
        Assert.False(other.IsAvailable);
        Assert.Equal("1920 × 1080", other.Resolution);
        Assert.NotEqual(string.Empty, other.Size);
    }

    private static DuplicatesOverviewViewModel NewModel() => new(
        new GetDuplicateOverview(new SingleGroupReader()),
        Preferences());

    private static SetPreferredVersion Preferences() => new(new NoVersionGroups());

    private sealed class NoVersionGroups : IMediaVersionGroupRepository
    {
        public Task<MediaVersionGroup?> FindByContentKeyAsync(
            string contentKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(null);

        public Task<MediaVersionGroup?> FindByIdAsync(
            MediaVersionId id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(null);

        public Task<MediaVersionGroup?> FindByMemberAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(null);

        public Task SaveAsync(MediaVersionGroup group, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SingleGroupReader : IDuplicateOverviewReader
    {
        public Task<IReadOnlyList<DuplicateOverviewEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DuplicateOverviewEntry>>(
            [
                new DuplicateOverviewEntry(
                    new TitleId(Guid.NewGuid()),
                    "Arrival",
                    2,
                    new MediaVersionId(Guid.NewGuid()),
                    [
                        new DuplicateFileRow(
                            new MediaFileId(Guid.NewGuid()),
                            @"D:\Cine\Arrival.2160p.mkv",
                            3840,
                            2160,
                            "HEVC",
                            "E-AC-3",
                            19_756_431_155,
                            TimeSpan.FromMinutes(116),
                            IsAvailable: true,
                            IsPreferred: true),
                        new DuplicateFileRow(
                            new MediaFileId(Guid.NewGuid()),
                            @"E:\Respaldo\Arrival.1080p.mkv",
                            1920,
                            1080,
                            "H264",
                            "AAC",
                            4_509_715_660,
                            TimeSpan.FromMinutes(116),
                            IsAvailable: false,
                            IsPreferred: false),
                    ]),
            ]);
    }
}
