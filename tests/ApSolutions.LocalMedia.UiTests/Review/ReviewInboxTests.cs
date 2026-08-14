// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Review;

public sealed class ReviewInboxTests
{
    [AvaloniaFact]
    public async Task Inbox_renders_pending_and_suggested_explanations_in_Spanish_and_English()
    {
        var repository = new UiReviewRepository([
            Candidate("cap-800", 0.59, ReviewState.Pending, "Identification.Warning.AmbiguousName"),
            Candidate("arrival", 0.75, ReviewState.Suggested, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
            var view = new ReviewInboxView { DataContext = viewModel };
            var window = new Window { Width = 1024, Height = 720, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var visibleText = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
            Assert.Contains(cultureName == "es-ES" ? "Pendiente" : "Pending", visibleText);
            Assert.Contains(cultureName == "es-ES" ? "Sugerida" : "Suggested", visibleText);
            Assert.Contains(cultureName == "es-ES" ? "Por qué" : "Why", visibleText);
            Assert.Contains(visibleText, text => text!.Contains("59", StringComparison.Ordinal));

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var artifactPath = Path.Combine(
                RepositoryLayout.Root,
                "artifacts",
                "ui-captures",
                "T14",
                $"review-{cultureName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            frame.Save(artifactPath, PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Tab_arrows_Enter_and_Escape_operate_review_without_a_mouse()
    {
        var repository = new UiReviewRepository([
            Candidate("first", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
            Candidate("second", 0.70, ReviewState.Suggested, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var view = new ReviewInboxView { DataContext = viewModel };
        var window = new Window { Width = 1024, Height = 720, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        list.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        var firstItem = view.GetVisualDescendants().OfType<ListBoxItem>().First();
        firstItem.Focus();
        Assert.True(list.IsKeyboardFocusWithin);

        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, list.SelectedIndex);

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await WaitForAsync(() => repository.Candidates.Single(candidate => candidate.StableKey == "second").IsDecisionLocked);
        Assert.Equal(ReviewState.Accepted, repository.Candidates.Single(candidate => candidate.StableKey == "second").ReviewState);

        viewModel.SelectedItem = viewModel.Items.Single();
        Dispatcher.UIThread.RunJobs();
        view.GetVisualDescendants().OfType<ListBoxItem>().Single().Focus();
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(viewModel.SelectedItem);

        var focusable = view.GetVisualDescendants().OfType<Control>().Where(control => control.Focusable).ToArray();
        Assert.True(focusable.Length >= 4);
        focusable[0].Focus();
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(focusable, control => control.IsKeyboardFocusWithin && control != focusable[0]);

        viewModel.SelectedItem = viewModel.Items.Single();
        var rejectButton = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Name == "RejectReviewAction");
        rejectButton.Focus();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await WaitForAsync(() => repository.Candidates.Single(candidate => candidate.StableKey == "first").IsDecisionLocked);
        Assert.Equal(ReviewState.Rejected, repository.Candidates.Single(candidate => candidate.StableKey == "first").ReviewState);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Reject_and_manual_search_are_explicit_actions()
    {
        var repository = new UiReviewRepository([
            Candidate("wrong", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedItem = Assert.Single(viewModel.Items);
        var searches = new List<string>();
        viewModel.ManualSearchRequested += (_, query) => searches.Add(query);
        viewModel.ManualSearch = "La llegada 2016";

        viewModel.SearchManuallyCommand.Execute(null);
        await viewModel.RejectSelectedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["La llegada 2016"], searches);
        Assert.Equal(ReviewState.Rejected, Assert.Single(repository.Candidates).ReviewState);
        Assert.Empty(viewModel.Items);
    }

    private static ReviewInboxViewModel CreateViewModel(UiReviewRepository repository)
    {
        var publisher = new NullPublisher();
        return new ReviewInboxViewModel(
            new GetReviewInbox(repository),
            new ResolveMatch(repository, publisher, SilentIdentification.Create()),
            new RejectMatch(repository, publisher));
    }

    private static MatchCandidate Candidate(string stableKey, double score, ReviewState state, string explanation)
    {
        var mediaFileId = new MediaFileId(CandidateId.FromStableKey($"file:{stableKey}").Value);
        return new MatchCandidate(
            CandidateId.FromStableKey(stableKey),
            mediaFileId,
            stableKey,
            CandidateContentKind.Movie,
            score,
            CandidateScorer.ScoringModelVersion,
            state,
            [new MatchSignal("Identification.Signal.Title", score, 0.5)],
            [explanation]);
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

        Assert.Fail("The keyboard action did not complete.");
    }

    private sealed class UiReviewRepository(IEnumerable<MatchCandidate> candidates) : IMatchCandidateRepository
    {
        public List<MatchCandidate> Candidates { get; } = [.. candidates];

        public Task ReplaceForMediaFileAsync(MediaFileId mediaFileId, IReadOnlyList<MatchCandidate> candidates, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>(Candidates.Where(candidate => candidate.MediaFileId == mediaFileId).ToArray());

        public Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(int offset, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>(Candidates
                .Where(candidate => candidate.ReviewState is ReviewState.Pending or ReviewState.Suggested)
                .OrderBy(candidate => candidate.ReviewState == ReviewState.Pending ? 0 : 1)
                .ThenBy(candidate => candidate.Score)
                .Skip(offset)
                .Take(limit)
                .ToArray());

        public Task<MatchDecisionWriteResult> TrySetReviewStateAsync(MediaFileId mediaFileId, CandidateId candidateId, int expectedRevision, ReviewState reviewState, bool lockDecision, CancellationToken cancellationToken = default)
        {
            var index = Candidates.FindIndex(candidate => candidate.MediaFileId == mediaFileId && candidate.Id == candidateId);
            var current = Candidates[index];
            if (current.Revision != expectedRevision || current.IsDecisionLocked)
            {
                return Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.Conflict, current));
            }

            var updated = current with { ReviewState = reviewState, Revision = current.Revision + 1, IsDecisionLocked = lockDecision };
            Candidates[index] = updated;
            return Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.Applied, updated));
        }
    }

    private sealed class NullPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull => Task.CompletedTask;
    }
}
