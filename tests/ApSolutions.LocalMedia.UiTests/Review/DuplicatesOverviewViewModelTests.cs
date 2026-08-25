// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Review;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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

    /// <summary>
    /// Every column of a row, including the ones a file that says less leaves blank.
    /// </summary>
    /// <remarks>
    /// The destination's table is eight columns wide and a real library fills few of them for every
    /// copy: a file whose dimensions were never read has no resolution, one under a gigabyte is
    /// counted in megabytes, one of no size at all says nothing rather than «0 MB», and one at the
    /// root of a drive has no folder. Each of those is the arm a screenshot of a tidy pair never
    /// shows.
    /// </remarks>
    [Fact]
    public async Task A_row_says_only_what_its_file_can_answer()
    {
        var model = new DuplicatesOverviewViewModel(
            new GetDuplicateOverview(new SparseReader()),
            Preferences());
        await model.LoadAsync(TestContext.Current.CancellationToken);

        var group = Assert.Single(model.Groups);
        Assert.Equal(2, group.VersionCount);
        Assert.NotEqual(default, group.GroupId);
        Assert.NotEqual(default, group.TitleId);

        var small = group.Files[0];
        Assert.Equal("...corto.mkv", small.ShortPath);
        Assert.Equal(@"D:\Cine", small.Location);
        Assert.Equal("700 MB", small.Size.Replace(',', '.'));
        Assert.Equal("44:00", small.Duration);
        Assert.Equal("1920 × 1080", small.Resolution);
        Assert.Equal("H264", small.VideoCodec);
        Assert.Equal("AAC", small.AudioCodec);
        Assert.NotEqual(default, small.MediaFileId);
        Assert.Same(small.Row, small.Row);

        // The silent one: no dimensions, no size, no duration, and a path with no folder in it.
        var silent = group.Files[1];
        Assert.Equal(string.Empty, silent.Resolution);
        Assert.Equal(string.Empty, silent.Size);
        Assert.Equal(string.Empty, silent.Duration);
        Assert.Equal(string.Empty, silent.Location);
        Assert.Equal("...suelto.mkv", silent.ShortPath);

        // Its command is the destination's own, handed to every row so a radio has something to
        // press without each row carrying a copy of the decision.
        Assert.Same(small.SetPreferredCommand, silent.SetPreferredCommand);

        // A path that is a folder and nothing else: no name to shorten and no parent to name.
        var root = new DuplicateFileRowViewModel(
            new DuplicateFileRow(
                new MediaFileId(Guid.NewGuid()),
                @"C:\",
                Width: null,
                Height: null,
                "H264",
                "AAC",
                SizeBytes: 0,
                Duration: TimeSpan.Zero,
                IsAvailable: true,
                IsPreferred: false),
            small.SetPreferredCommand);
        Assert.Equal(@"C:\", root.ShortPath);
        Assert.Equal(string.Empty, root.Location);
        Assert.Equal(string.Empty, root.Duration);
    }

    /// <summary>
    /// A destination built over nothing refuses, and a group that arrives with no files draws a
    /// heading over an empty table rather than throwing.
    /// </summary>
    [Fact]
    public async Task A_group_with_no_files_is_listed_without_a_table()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DuplicateFileRowViewModel(null!, NewModel().SetPreferredCommand));
        Assert.Throws<ArgumentNullException>(() =>
            new DuplicateFileRowViewModel(
                new DuplicateFileRow(
                    new MediaFileId(Guid.NewGuid()),
                    @"D:\Cine\a.mkv",
                    1920,
                    1080,
                    "H264",
                    "AAC",
                    1_000,
                    TimeSpan.FromMinutes(1),
                    IsAvailable: true,
                    IsPreferred: false),
                null!));

        var model = new DuplicatesOverviewViewModel(
            new GetDuplicateOverview(new FilelessReader()),
            Preferences());
        await model.LoadAsync(TestContext.Current.CancellationToken);

        var group = Assert.Single(model.Groups);
        Assert.False(group.HasFiles);
        Assert.Empty(group.Files);
    }

    /// <summary>A group the reader lists without the files behind it.</summary>
    private sealed class FilelessReader : IDuplicateOverviewReader
    {
        public Task<IReadOnlyList<DuplicateOverviewEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DuplicateOverviewEntry>>(
                [new DuplicateOverviewEntry(new TitleId(Guid.NewGuid()), "Arrival", 2)]);
    }

    /// <summary>
    /// Choosing a copy stores the choice for the group that copy belongs to, and refuses anything
    /// that is not a copy.
    /// </summary>
    /// <remarks>
    /// The group is found from the file rather than carried beside it: a radio knows which row it is
    /// on and nothing else, and a command parameter that had to carry both would be a pair nobody
    /// could see was wrong. So the lookup is the part that has to be measured — including the answer
    /// when the file belongs to no group the destination is showing.
    /// </remarks>
    // On the UI thread: both commands settle on a dispatcher continuation.
    [AvaloniaFact]
    public async Task Choosing_a_copy_stores_it_for_the_group_that_copy_belongs_to()
    {
        var reader = new SingleGroupReader();
        var listed = await reader.ListAsync(TestContext.Current.CancellationToken);
        var members = listed[0].Files!.Select(file => file.MediaFileId).ToArray();
        var groups = new RecordingVersionGroups(members);
        var model = new DuplicatesOverviewViewModel(
            new GetDuplicateOverview(reader),
            new SetPreferredVersion(groups));
        await model.LoadAsync(TestContext.Current.CancellationToken);

        var group = Assert.Single(model.Groups);
        var other = Assert.Single(group.Files, file => !file.IsPreferred);

        Assert.True(model.SetPreferredCommand.CanExecute(other));
        model.SetPreferredCommand.Execute(other);
        await WaitForAsync(() => groups.Saved.Count > 0);

        Assert.Equal(other.MediaFileId, groups.Saved[0]);

        // Anything that is not a row is refused, and a row the destination is not showing changes
        // nothing rather than storing a choice for a group nobody is looking at.
        Assert.False(model.SetPreferredCommand.CanExecute("not a row"));
        model.SetPreferredCommand.Execute(null);

        var stranger = new DuplicateFileRowViewModel(
            new DuplicateFileRow(
                new MediaFileId(Guid.NewGuid()),
                @"D:\Cine\ajeno.mkv",
                1920,
                1080,
                "H264",
                "AAC",
                1_000,
                TimeSpan.FromMinutes(44),
                IsAvailable: true,
                IsPreferred: false),
            model.SetPreferredCommand);
        model.SetPreferredCommand.Execute(stranger);
        await WaitForAsync(() => true);
        Assert.Single(groups.Saved);
    }

    /// <summary>Opening a group asks whoever owns the comparison, and only for a real row.</summary>
    // On the UI thread: both commands settle on a dispatcher continuation.
    [AvaloniaFact]
    public async Task Opening_a_group_reaches_the_opener_and_refuses_anything_else()
    {
        var opened = new List<TitleId>();
        var model = NewModel();
        await model.LoadAsync(TestContext.Current.CancellationToken);

        // With no opener the row is still offered — the shell wires one after building this — and
        // pressing it reaches nobody rather than throwing.
        var row = Assert.Single(model.Groups);
        model.OpenGroupCommand.Execute(row);

        model.GroupOpener = (titleId, _) =>
        {
            opened.Add(titleId);
            return Task.CompletedTask;
        };
        model.OpenGroupCommand.Execute(row);
        await WaitForAsync(() => opened.Count > 0);
        Assert.Equal(row.TitleId, Assert.Single(opened));

        Assert.False(model.OpenGroupCommand.CanExecute("not a group"));
        model.OpenGroupCommand.Execute(null);
        Assert.Single(opened);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The command never settled.");
    }

    /// <summary>A group whose two copies answer as little as the catalogue allows.</summary>
    private sealed class SparseReader : IDuplicateOverviewReader
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
                            @"D:\Cine\corto.mkv",
                            1920,
                            1080,
                            "H264",
                            "AAC",
                            734_003_200,
                            TimeSpan.FromMinutes(44),
                            IsAvailable: true,
                            IsPreferred: true),
                        new DuplicateFileRow(
                            new MediaFileId(Guid.NewGuid()),
                            "suelto.mkv",
                            Width: null,
                            Height: null,
                            "H264",
                            "AAC",
                            SizeBytes: 0,
                            Duration: null,
                            IsAvailable: false,
                            IsPreferred: false),
                    ]),
            ]);
    }

    /// <summary>
    /// A store that answers with the members it was told about and remembers which copy was chosen.
    /// </summary>
    /// <remarks>
    /// The members matter: <c>SetPreferredVersion</c> refuses a file that does not belong to the
    /// group it was asked about, which is the guard that keeps a radio on one table from choosing a
    /// copy on another.
    /// </remarks>
    private sealed class RecordingVersionGroups(params MediaFileId[] members) : IMediaVersionGroupRepository
    {
        public List<MediaFileId> Saved { get; } = [];

        public Task<MediaVersionGroup?> FindByContentKeyAsync(
            string contentKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(null);

        public Task<MediaVersionGroup?> FindByIdAsync(
            MediaVersionId id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(new MediaVersionGroup(
                id,
                "title:x",
                [
                    .. members.Select(member => new MediaVersion(
                        member,
                        @"D:\Cine\copia.mkv",
                        IsAvailable: true,
                        TimeSpan.FromMinutes(116),
                        1920,
                        1080,
                        IsHdr: false,
                        "H264",
                        1_000)),
                ],
                PreferredMediaFileId: null));

        public Task<MediaVersionGroup?> FindByMemberAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(null);

        public Task SaveAsync(MediaVersionGroup group, CancellationToken cancellationToken = default)
        {
            if (group.PreferredMediaFileId is { } preferred)
            {
                Saved.Add(preferred);
            }

            return Task.CompletedTask;
        }
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

    /// <summary>
    /// One group, and the same one every time it is asked.
    /// </summary>
    /// <remarks>
    /// Built once rather than per call: the destination reads the list again after storing a choice,
    /// and a reader that minted fresh identifiers each time would answer with a group whose files
    /// are not the ones anybody just clicked.
    /// </remarks>
    private sealed class SingleGroupReader : IDuplicateOverviewReader
    {
        private readonly IReadOnlyList<DuplicateOverviewEntry> _entries = Build();

        public Task<IReadOnlyList<DuplicateOverviewEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries);

        private static IReadOnlyList<DuplicateOverviewEntry> Build() =>
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
            ];
    }
}
