// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Review;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Review;

/// <summary>
/// The review inbox is where a moved file waits for a person (LIB-002/003): the held offers are
/// listed with their candidates, confirming one hands the old entity its new path, and keeping the
/// file as new stores its identity so the offer never returns.
/// </summary>
public sealed class ReassignmentInboxTests
{
    private static readonly LibraryRootId Root = new(Guid.Parse("11b20001-0000-4000-8000-000000000001"));

    [Fact]
    public async Task The_inbox_lists_what_reconciliation_held()
    {
        var queue = new PendingReassignments();
        queue.Offer(Offer(@"R:\media\archive\film.mkv", CandidateRow(@"R:\media\old\film.mkv")));
        var viewModel = CreateViewModel(new InMemoryMediaFiles(), queue);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasReassignments);
        var offer = Assert.Single(viewModel.Reassignments);
        Assert.Equal(@"R:\media\archive\film.mkv", offer.NewPath);
        Assert.Equal(@"R:\media\old\film.mkv", Assert.Single(offer.Candidates).Path);
    }

    // On the UI thread since 2026-08-25: the row's own command is what is pressed now, and an
    // async command settles on a dispatcher continuation.
    [AvaloniaFact]
    public async Task Confirming_a_candidate_reassigns_the_entity_and_clears_the_offer()
    {
        var repository = new InMemoryMediaFiles();
        var old = Media(@"R:\media\old\film.mkv", 1);
        var stranger = Media(@"R:\media\archive\film.mkv", 2);
        var identity = new FileIdentity(null, null, "sha256:v1:SAME");
        await repository.UpsertAsync(old, TestContext.Current.CancellationToken);
        await repository.UpsertAsync(stranger, TestContext.Current.CancellationToken);
        await repository.SaveIdentityAsync(old.Id, identity, TestContext.Current.CancellationToken);
        var queue = new PendingReassignments();
        var pending = new PendingReassignment(
            new ReconcileFileCommand(Root, stranger.Path, identity),
            [new ReassignmentCandidate(old.Id, old.Path)]);
        queue.Offer(pending);
        var viewModel = CreateViewModel(repository, queue);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        // Through the row's own command, which is what the view presses: the method behind it was
        // measured and the command that reaches it was not, and a command nothing reads is this
        // repository's characteristic defect wearing a binding.
        var offer = Assert.Single(viewModel.Reassignments);
        Assert.Single(offer.Candidates).ConfirmCommand.Execute(null);
        await WaitForAsync(() => !viewModel.HasReassignments);

        var settled = await repository.FindByPathAsync(
            Root,
            stranger.Path,
            TestContext.Current.CancellationToken);
        Assert.Equal(old.Id, settled?.Id);
        Assert.Null(await repository.FindByIdAsync(stranger.Id, TestContext.Current.CancellationToken));
        Assert.Empty(queue.List());
        Assert.False(viewModel.HasReassignments);
    }

    // On the UI thread since 2026-08-25: the row's own command is what is pressed now, and an
    // async command settles on a dispatcher continuation.
    [AvaloniaFact]
    public async Task Keeping_the_file_as_new_stores_its_identity_and_clears_the_offer()
    {
        var repository = new InMemoryMediaFiles();
        var stranger = Media(@"R:\media\archive\film.mkv", 3);
        var identity = new FileIdentity(null, null, "sha256:v1:HELD");
        await repository.UpsertAsync(stranger, TestContext.Current.CancellationToken);
        var queue = new PendingReassignments();
        var pending = new PendingReassignment(
            new ReconcileFileCommand(Root, stranger.Path, identity),
            [CandidateRow(@"R:\media\old\film.mkv")]);
        queue.Offer(pending);
        var viewModel = CreateViewModel(repository, queue);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.Reassignments).KeepAsNewCommand.Execute(null);
        await WaitForAsync(() => !viewModel.HasReassignments);

        Assert.Equal(
            identity,
            await repository.GetIdentityAsync(stranger.Id, TestContext.Current.CancellationToken));
        Assert.Empty(queue.List());
        Assert.False(viewModel.HasReassignments);
    }

    [Fact]
    public async Task Without_its_collaborators_the_section_stays_absent()
    {
        var viewModel = new ReviewInboxViewModel(
            new GetReviewInbox(new EmptyCandidates()),
            new ResolveMatch(new EmptyCandidates(), new NullPublisher(), SilentIdentification.Create()),
            new RejectMatch(new EmptyCandidates(), new NullPublisher()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.HasReassignments);
    }

    /// <summary>
    /// And a decision asked of that same inbox is refused rather than thrown: without the
    /// reconciliation collaborators there is nothing to decide with.
    /// </summary>
    /// <remarks>
    /// The section is invisible in that configuration, so this is not reachable by clicking — but it
    /// is reachable by holding an offer, which is what any caller with a <c>PendingReassignment</c>
    /// does. Answering with an exception would turn a surface that is merely absent into a crash.
    /// </remarks>
    [Fact]
    public async Task Without_its_collaborators_a_held_decision_is_refused_rather_than_thrown()
    {
        var repository = new InMemoryMediaFiles();
        var stranger = Media(@"R:\media\archive\film.mkv", 4);
        await repository.UpsertAsync(stranger, TestContext.Current.CancellationToken);
        var viewModel = new ReviewInboxViewModel(
            new GetReviewInbox(new EmptyCandidates()),
            new ResolveMatch(new EmptyCandidates(), new NullPublisher(), SilentIdentification.Create()),
            new RejectMatch(new EmptyCandidates(), new NullPublisher()));
        var pending = Offer(stranger.Path, CandidateRow(@"R:\media\old\film.mkv"));

        await viewModel.ConfirmReassignmentAsync(
            pending,
            pending.Candidates[0],
            TestContext.Current.CancellationToken);
        await viewModel.KeepAsNewAsync(pending, TestContext.Current.CancellationToken);

        // Nothing was decided, and nothing was written: the discovered row keeps no identity, so a
        // later scan with the collaborators in place still offers it.
        Assert.Null(await repository.GetIdentityAsync(stranger.Id, TestContext.Current.CancellationToken));
        Assert.False(viewModel.HasReassignments);
    }

    /// <summary>
    /// What an offer refuses to be built without, and what a decision refuses to be asked without.
    /// </summary>
    /// <remarks>
    /// The offer rows come from a queue, and the two callbacks are how a press reaches the catalogue
    /// at all. A row built without one of them is a button that does nothing when it is pressed —
    /// which is this repository's characteristic defect, and the reason the guard is here rather
    /// than left to the first person who wires a new caller.
    /// </remarks>
    [Fact]
    public async Task An_offer_and_its_decisions_refuse_what_they_cannot_act_on()
    {
        var pending = Offer(@"R:\media\archive\film.mkv", CandidateRow(@"R:\media\old\film.mkv"));
        var candidate = pending.Candidates[0];
        var viewModel = CreateViewModel(new InMemoryMediaFiles(), new PendingReassignments());

        Assert.Throws<ArgumentNullException>(() =>
            new ReassignmentCandidateViewModel(null!, _ => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() =>
            new ReassignmentCandidateViewModel(candidate, null!));
        Assert.Throws<ArgumentNullException>(() =>
            new PendingReassignmentViewModel(null!, _ => Task.CompletedTask, () => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() =>
            new PendingReassignmentViewModel(pending, null!, () => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() =>
            new PendingReassignmentViewModel(pending, _ => Task.CompletedTask, null!));

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            viewModel.ConfirmReassignmentAsync(null!, candidate, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            viewModel.ConfirmReassignmentAsync(pending, null!, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            viewModel.KeepAsNewAsync(null!, TestContext.Current.CancellationToken));
    }

    private static ReviewInboxViewModel CreateViewModel(
        InMemoryMediaFiles repository,
        PendingReassignments queue)
    {
        var reconcile = new ReconcileScanResults(repository, new FileReconciliationPolicy());
        return new ReviewInboxViewModel(
            new GetReviewInbox(new EmptyCandidates()),
            new ResolveMatch(new EmptyCandidates(), new NullPublisher(), SilentIdentification.Create()),
            new RejectMatch(new EmptyCandidates(), new NullPublisher()),
            queue,
            new ManualReassignmentViewModel(reconcile),
            new ReconcileScannedFiles(
                repository,
                reconcile,
                new UnusedIdentityProvider(),
                new FileReconciliationPolicy(),
                queue));
    }

    private static PendingReassignment Offer(string newPath, ReassignmentCandidate candidate) => new(
        new ReconcileFileCommand(Root, newPath, new FileIdentity(null, null, "sha256:v1:OFFER")),
        [candidate]);

    private static ReassignmentCandidate CandidateRow(string path) =>
        new(new MediaFileId(Guid.NewGuid()), path);

    private static MediaFile Media(string path, int seed) => new(
        new MediaFileId(Guid.Parse($"11b2000{seed}-0000-4000-8000-0000000000f{seed}")),
        Root,
        path,
        1_000 + seed,
        DateTimeOffset.UnixEpoch,
        new TechnicalMetadata(TimeSpan.FromMinutes(90), "mkv", ["HEVC"], ["EAC3"], 1920, 1080));

    private sealed class UnusedIdentityProvider : IFileIdentityProvider
    {
        public Task<FileIdentity> GetAsync(
            string path,
            TechnicalMetadata technicalMetadata,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NullPublisher : ApSolutions.LocalMedia.Application.Events.IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull => Task.CompletedTask;
    }

    private sealed class EmptyCandidates : IMatchCandidateRepository
    {
        public Task ReplaceForMediaFileAsync(
            MediaFileId mediaFileId,
            IReadOnlyList<MatchCandidate> candidates,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>([]);

        public Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>([]);

        public Task<MatchDecisionWriteResult> TrySetReviewStateAsync(
            MediaFileId mediaFileId,
            CandidateId candidateId,
            int expectedRevision,
            ReviewState reviewState,
            bool lockDecision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.NotFound, null));
    }

    private sealed class InMemoryMediaFiles : IMediaFileRepository
    {
        private readonly Dictionary<Guid, MediaFile> _files = [];
        private readonly Dictionary<Guid, FileIdentity> _identities = [];

        public Task<MediaFile?> FindByPathAsync(
            LibraryRootId rootId,
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_files.Values.FirstOrDefault(file =>
                file.LibraryRootId == rootId
                && string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase)));

        public Task<MediaFile?> FindByIdAsync(MediaFileId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_files.TryGetValue(id.Value, out var file) ? file : null);

        public Task<IReadOnlyDictionary<string, MediaFile>> FindByPathsAsync(
            LibraryRootId rootId,
            IReadOnlyCollection<string> paths,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default)
        {
            _files[mediaFile.Id.Value] = mediaFile;
            return Task.CompletedTask;
        }

        public Task UpsertBatchAsync(
            IReadOnlyCollection<MediaFile> mediaFiles,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
            FileIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_identities
                .Where(pair =>
                    pair.Value.HasStableFileId
                    && string.Equals(pair.Value.VolumeId, identity.VolumeId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pair.Value.FileId, identity.FileId, StringComparison.OrdinalIgnoreCase))
                .Select(pair => new IdentifiedMediaFile(_files[pair.Key], pair.Value))
                .FirstOrDefault());

        public Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
            string fingerprint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IdentifiedMediaFile>>([.. _identities
                .Where(pair => string.Equals(pair.Value.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                .Select(pair => new IdentifiedMediaFile(_files[pair.Key], pair.Value))]);

        public Task SaveIdentityAsync(
            MediaFileId mediaFileId,
            FileIdentity identity,
            CancellationToken cancellationToken = default)
        {
            _identities[mediaFileId.Value] = identity;
            return Task.CompletedTask;
        }

        public Task<FileIdentity?> GetIdentityAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_identities.TryGetValue(mediaFileId.Value, out var identity) ? identity : null);

        public Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default)
        {
            _files.Remove(mediaFileId.Value);
            _identities.Remove(mediaFileId.Value);
            return Task.CompletedTask;
        }

        public Task ReassignAsync(
            MediaFileId mediaFileId,
            LibraryRootId libraryRootId,
            string newPath,
            FileIdentity identity,
            CancellationToken cancellationToken = default)
        {
            var file = _files[mediaFileId.Value];
            _files[mediaFileId.Value] = file with
            {
                LibraryRootId = libraryRootId,
                Path = newPath,
                IsAvailable = true,
            };
            _identities[mediaFileId.Value] = identity;
            return Task.CompletedTask;
        }

        public Task SetRootAvailabilityAsync(
            LibraryRootId libraryRootId,
            bool isAvailable,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> GetScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveScanCheckpointAsync(
            LibraryRootId rootId,
            string resumeAfterPath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ClearScanCheckpointAsync(
            LibraryRootId rootId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetScannedTitleAsync(
            MediaFileId mediaFileId,
            ScannedTitle title,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// A command answers on its own continuation, so what is asserted is the state it leaves rather
    /// than the moment it was pressed.
    /// </summary>
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

        Assert.Fail("The reassignment command did not settle.");
    }

}
