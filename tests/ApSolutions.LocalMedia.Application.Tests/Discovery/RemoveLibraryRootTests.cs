// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Discovery;

/// <summary>
/// Removing a folder is one thing now. Until 2026-09-06 the command carried a <c>PreserveCatalog</c>
/// flag, defaulted to true, threaded through a command, a domain interface, an adapter and sixteen
/// test doubles — and read by nobody: the repository discarded it on its first line. The only test
/// that mentioned it asserted, by reflection, that the parameter had a default value of true. It
/// checked a signature and not an effect, and it is gone.
/// </summary>
public sealed class RemoveLibraryRootTests
{
    [Fact]
    public async Task Removing_a_root_deletes_the_covers_of_the_titles_that_left()
    {
        var first = new TitleId(Guid.NewGuid());
        var second = new TitleId(Guid.NewGuid());
        var roots = new RecordingRoots([first, second]);
        var artwork = new RecordingArtwork();

        await new RemoveLibraryRoot(roots, artwork).ExecuteAsync(
            new RemoveLibraryRootCommand(new LibraryRootId(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // The covers live on disk, so only the titles the transaction actually took may lose theirs.
        Assert.Equal([first, second], artwork.Removed);
    }

    [Fact]
    public async Task A_removal_without_an_artwork_store_still_empties_the_catalogue()
    {
        var roots = new RecordingRoots([new TitleId(Guid.NewGuid())]);

        await new RemoveLibraryRoot(roots).ExecuteAsync(
            new RemoveLibraryRootCommand(new LibraryRootId(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, roots.Removals);
    }

    [Fact]
    public async Task Removing_a_root_asks_for_a_root_and_for_nothing_else()
    {
        Assert.Throws<ArgumentNullException>(() => new RemoveLibraryRoot(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new RemoveLibraryRoot(new RecordingRoots([]))
                .ExecuteAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Summarizing_a_root_asks_the_reader_for_that_root()
    {
        var id = new LibraryRootId(Guid.NewGuid());
        var reader = new StubReader(new LibraryRootRemovalSummary(3, 5, TimeSpan.FromMinutes(92)));

        var summary = await new SummarizeLibraryRootRemoval(reader)
            .ExecuteAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(id, reader.Asked);
        Assert.Equal(3, summary.TitleCount);
        Assert.Equal(5, summary.MarkCount);
        Assert.Equal(TimeSpan.FromMinutes(92), summary.Progress);
    }

    [Fact]
    public void A_root_with_nothing_to_lose_summarises_as_three_zeros()
    {
        // Not null: a folder added a moment ago and not yet scanned has nothing to lose, and the
        // question still has to be drawn. Three zeros are an answer; an absent summary is a decision
        // the surface would have to make on its own.
        Assert.Equal(0, LibraryRootRemovalSummary.Empty.TitleCount);
        Assert.Equal(0, LibraryRootRemovalSummary.Empty.MarkCount);
        Assert.Equal(TimeSpan.Zero, LibraryRootRemovalSummary.Empty.Progress);
        Assert.Throws<ArgumentNullException>(() => new SummarizeLibraryRootRemoval(null!));
    }

    private sealed class RecordingRoots : ILibraryRootRepository
    {
        private readonly IReadOnlyList<TitleId> _leaving;

        public RecordingRoots(IReadOnlyList<TitleId> leaving) => _leaving = leaving;

        public int Removals { get; private set; }

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<TitleId>> RemoveAsync(
            LibraryRootId id,
            CancellationToken cancellationToken = default)
        {
            Removals++;
            return Task.FromResult(_leaving);
        }
    }

    private sealed class RecordingArtwork : IArtworkStore
    {
        public List<TitleId> Removed { get; } = [];

        public string? Find(TitleId titleId, Uri source) => null;

        public string? FindPersonal(TitleId titleId, string coverFileName) => null;

        public Task<string?> FetchAsync(
            TitleId titleId,
            Uri source,
            string alternativeText,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ArtworkReference> ImportPersonalAsync(
            TitleId titleId,
            string sourcePath,
            string alternativeText,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveTitleAsync(TitleId titleId, CancellationToken cancellationToken = default)
        {
            Removed.Add(titleId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubReader : ILibraryRootRemovalReader
    {
        private readonly LibraryRootRemovalSummary _summary;

        public StubReader(LibraryRootRemovalSummary summary) => _summary = summary;

        public LibraryRootId Asked { get; private set; }

        public Task<LibraryRootRemovalSummary> SummarizeAsync(
            LibraryRootId id,
            CancellationToken cancellationToken = default)
        {
            Asked = id;
            return Task.FromResult(_summary);
        }
    }
}
