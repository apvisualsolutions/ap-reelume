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
using Avalonia.Media;
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
    public async Task Tab_Enter_and_Escape_operate_review_without_a_mouse()
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
        // Accept with the keyboard, on the card the person tabbed to. It used to be Enter on a row
        // that a pair of arrow keys moved between; a card holds three decisions now, so the key
        // belongs to whichever of them has focus and Tab is what walks between them.
        var second = viewModel.Items[1];
        var acceptButton = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, second.AcceptCommand));
        acceptButton.Focus();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await WaitForAsync(() => repository.Candidates.Single(candidate => candidate.StableKey == "second").IsDecisionLocked);
        Assert.Equal(ReviewState.Accepted, repository.Candidates.Single(candidate => candidate.StableKey == "second").ReviewState);

        // Escape gives up whichever card the manual search was aimed at, from anywhere on the
        // surface: the binding is the view's own rather than a list's.
        viewModel.SelectedItem = viewModel.Items.Single();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(viewModel.SelectedItem);
        var box = view.GetVisualDescendants().OfType<TextBox>().Single(text => text.Name == "ManualSearchBox");
        Assert.True(box.Focus(), "The tray's own search box could not take focus.");
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

        // The decisions live in the card since 2026-08-25, so the button is found by the command it
        // carries rather than by a name on the surface: one card, one Reject, and it is that card's.
        var remaining = viewModel.Items.Single();
        viewModel.SelectedItem = remaining;
        Dispatcher.UIThread.RunJobs();
        var rejectButton = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, remaining.RejectCommand));
        rejectButton.Focus();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await WaitForAsync(() => repository.Candidates.Single(candidate => candidate.StableKey == "first").IsDecisionLocked);
        Assert.Equal(ReviewState.Rejected, repository.Candidates.Single(candidate => candidate.StableKey == "first").ReviewState);
        window.Close();
    }

    /// <summary>
    /// The tray counts what is in it, and a card's own «search manually» aims the box at its file.
    /// </summary>
    /// <remarks>
    /// The count is the line the prototype writes under the intro, and the aiming is what the card's
    /// third button does: it selects the card and puts the file's own name in the box, which is what
    /// a person would type first. It fills the box only when the box is empty — overwriting words
    /// somebody had already typed would be the button answering a question nobody asked.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_tray_counts_what_waits_and_a_card_aims_the_search_at_its_file()
    {
        var repository = new UiReviewRepository([
            Candidate("first", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
            Candidate("second", 0.70, ReviewState.Suggested, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(
            repository,
            new SearchForMatch(
                new IdentifyMediaFile(new MediaNameParser(), new CandidateScorer(), new UiCandidateSource(), repository),
                SilentIdentification.Create()));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, viewModel.Count);

        var card = viewModel.Items[0];
        card.SearchManuallyCommand.Execute(null);
        Assert.Same(card, viewModel.SelectedItem);

        // The stub's candidates carry no file, so there is nothing to put in the box and the button
        // has still done its half: the tray now knows which file a search would be about.
        Assert.True(string.IsNullOrEmpty(viewModel.ManualSearch));

        // And with a file behind it the box starts from the file's own name without its extension,
        // which is what a person would type first and what the parser already knows how to read.
        var withFile = new UiReviewRepository([
            Candidate(
                "third",
                0.55,
                ReviewState.Pending,
                "Identification.Signal.Title",
                @"D:\Cine\La.Llegada.2016.1080p.mkv"),
        ]);
        var aimed = CreateViewModel(withFile);
        await aimed.LoadAsync(TestContext.Current.CancellationToken);
        var withPath = Assert.Single(aimed.Items);
        Assert.Equal("La.Llegada.2016.1080p.mkv", withPath.FileName);

        withPath.SearchManuallyCommand.Execute(null);
        Assert.Equal("La.Llegada.2016.1080p", aimed.ManualSearch);

        // Words already typed are left alone.
        viewModel.ManualSearch = "La llegada 2016";
        viewModel.Items[1].SearchManuallyCommand.Execute(null);
        Assert.Equal("La llegada 2016", viewModel.ManualSearch);
        Assert.Same(viewModel.Items[1], viewModel.SelectedItem);

        // And the tray's own Search button runs the search the box holds, through the command rather
        // than through the method behind it.
        Assert.True(viewModel.SearchManuallyCommand.CanExecute(null));
        viewModel.SearchManuallyCommand.Execute(null);
        await WaitForAsync(() => viewModel.Items.Any(item => item.StableKey == "movie:329865"));
    }

    [AvaloniaFact]
    public async Task Reject_and_manual_search_are_explicit_actions()
    {
        var repository = new UiReviewRepository([
            Candidate("wrong", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
        ]);
        var source = new UiCandidateSource();
        var viewModel = CreateViewModel(
            repository,
            new SearchForMatch(
                new IdentifyMediaFile(new MediaNameParser(), new CandidateScorer(), source, repository),
                SilentIdentification.Create()));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        // Nothing to search for and nothing to search about: the button says so rather than looking
        // available. Both halves are required, and both are answered while the surface is on screen —
        // which is why the command has to be able to say its answer changed at all.
        Assert.False(viewModel.SearchManuallyCommand.CanExecute(null));
        viewModel.SelectedItem = Assert.Single(viewModel.Items);
        Assert.False(viewModel.SearchManuallyCommand.CanExecute(null));
        viewModel.ManualSearch = "La llegada 2016";
        Assert.True(viewModel.SearchManuallyCommand.CanExecute(null));

        await viewModel.SearchManuallyAsync(TestContext.Current.CancellationToken);

        // The words were read the way a file name is read, and what came back replaced what was there.
        Assert.Equal("La llegada", source.LastTitle);
        Assert.Equal(2016, source.LastYear);
        Assert.Equal("movie:329865", Assert.Single(viewModel.Items).StableKey);

        viewModel.SelectedItem = Assert.Single(viewModel.Items);
        await viewModel.RejectSelectedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ReviewState.Rejected, Assert.Single(repository.Candidates).ReviewState);
        Assert.Empty(viewModel.Items);
    }

    /// <summary>
    /// With nothing chosen there is nothing to decide, and every action says so by doing nothing.
    /// </summary>
    /// <remarks>
    /// The commands are bound to buttons that are on screen whether or not a card is selected, so
    /// "no selection" is a state a person reaches by pressing Escape, not an impossible one. What
    /// makes it worth a test is that the alternative is a null reference on the surface a person
    /// opens precisely because the automatic reading was not good enough.
    /// </remarks>
    [Fact]
    public async Task With_nothing_selected_no_decision_reaches_the_catalogue()
    {
        var repository = new UiReviewRepository([
            Candidate("undecided", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(repository);
        Assert.True(viewModel.IsEmpty);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(viewModel.IsEmpty);
        Assert.Null(viewModel.SelectedItem);

        await viewModel.AcceptSelectedAsync(TestContext.Current.CancellationToken);
        await viewModel.RejectSelectedAsync(TestContext.Current.CancellationToken);
        await viewModel.SearchManuallyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ReviewState.Pending, Assert.Single(repository.Candidates).ReviewState);
        Assert.False(viewModel.HasConflict);

        // The page is one page long, so asking for more asks the repository nothing at all.
        Assert.False(viewModel.HasMore);
        await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
        Assert.Single(viewModel.Items);

        // Typed words with no card chosen are still not a search: the file to search about is the
        // half that is missing, and clearing a selection that is already clear changes nothing.
        viewModel.ManualSearch = "La llegada 2016";
        viewModel.SelectedItem = null;
        await viewModel.SearchManuallyAsync(TestContext.Current.CancellationToken);
        Assert.Equal("undecided", Assert.Single(viewModel.Items).StableKey);
    }

    /// <summary>
    /// Somebody else decided this candidate first: the decision is refused, and the card is replaced
    /// by what the catalogue now holds rather than disappearing as though it had been applied.
    /// </summary>
    /// <remarks>
    /// This is the branch that separates "your decision was written" from "your decision was
    /// refused and here is why". Both leave the button pressed and the surface changed, and only the
    /// stored row tells them apart — which is exactly why the conflict has to keep the card.
    /// </remarks>
    [Fact]
    public async Task A_candidate_decided_elsewhere_is_refused_and_the_card_shows_what_is_stored()
    {
        var repository = new UiReviewRepository([
            Candidate("contested", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedItem = Assert.Single(viewModel.Items);

        // The row moves on under the surface, the way another window or a scan moves it.
        repository.Candidates[0] = repository.Candidates[0] with
        {
            ReviewState = ReviewState.Suggested,
            Revision = repository.Candidates[0].Revision + 1,
        };

        await viewModel.AcceptSelectedAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasConflict);
        Assert.Equal(ReviewState.Suggested, Assert.Single(repository.Candidates).ReviewState);

        // The card stays, carrying the stored state, and stays selected: a person who has just been
        // told no is looking at the same card they were looking at.
        var shown = Assert.Single(viewModel.Items);
        Assert.Equal(ReviewState.Suggested, shown.ReviewState);
        Assert.Same(shown, viewModel.SelectedItem);
    }

    /// <summary>
    /// The inbox arrives one page at a time, and asking for more when the last page is in hand asks
    /// the catalogue nothing.
    /// </summary>
    /// <remarks>
    /// Both halves live in one test on purpose. Merged Cobertura reports keep the better of two
    /// runs for a line rather than the union of them, so a branch whose sides are exercised by two
    /// different suites reads as half-covered forever — measured here on 2026-08-16, with the walk
    /// paging through twenty-six candidates and this suite stopping at one.
    /// </remarks>
    [Fact]
    public async Task The_inbox_pages_and_stops_when_there_is_no_more()
    {
        var repository = new UiReviewRepository(
            Enumerable.Range(1, 26).Select(index =>
                Candidate($"page:{index:D2}", 0.40 + (index / 1000.0), ReviewState.Pending, "Identification.Signal.Title")));
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(25, viewModel.Items.Count);
        Assert.True(viewModel.HasMore);

        await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(26, viewModel.Items.Count);
        Assert.False(viewModel.HasMore);

        // And again, with nothing left: the list is untouched rather than asked for a page that is
        // not there.
        await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(26, viewModel.Items.Count);
    }

    /// <summary>
    /// A candidate the catalogue no longer holds is refused with nothing to put in its place, and the
    /// card is left exactly as it was.
    /// </summary>
    /// <remarks>
    /// The refused branch has two shapes and they are not the same: a conflict answers with the row
    /// that won, and a vanished candidate answers with nothing. Replacing a card with a null would be
    /// the crash; leaving the list alone is what a person can act on.
    /// </remarks>
    [Fact]
    public async Task A_candidate_that_is_no_longer_there_is_refused_and_the_list_is_left_alone()
    {
        var repository = new UiReviewRepository([
            Candidate("vanished", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
        ])
        { AnswerNotFound = true };
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var card = Assert.Single(viewModel.Items);
        viewModel.SelectedItem = card;

        await viewModel.RejectSelectedAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.HasConflict);
        Assert.Same(card, Assert.Single(viewModel.Items));
        Assert.Same(card, viewModel.SelectedItem);
    }

    /// <summary>
    /// A refusal replaces the card it was about and leaves every other card where it was.
    /// </summary>
    [Fact]
    public async Task A_refusal_touches_only_the_card_it_was_about()
    {
        var repository = new UiReviewRepository([
            Candidate("contested", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
            Candidate("bystander", 0.60, ReviewState.Pending, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var contested = viewModel.Items.Single(item => item.StableKey == "contested");
        var bystander = viewModel.Items.Single(item => item.StableKey == "bystander");
        viewModel.SelectedItem = contested;

        var index = repository.Candidates.FindIndex(candidate => candidate.StableKey == "contested");
        repository.Candidates[index] = repository.Candidates[index] with
        {
            Revision = repository.Candidates[index].Revision + 1,
        };

        await viewModel.AcceptSelectedAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasConflict);
        Assert.NotSame(contested, viewModel.Items.Single(item => item.StableKey == "contested"));
        Assert.Same(bystander, viewModel.Items.Single(item => item.StableKey == "bystander"));
    }

    /// <summary>
    /// What the surface refuses to be built without. Each of these is a collaborator the inbox cannot
    /// answer a press without, so an inbox constructed without one is a surface that fails when
    /// somebody uses it rather than when somebody wires it.
    /// </summary>
    [Fact]
    public void The_inbox_refuses_to_exist_without_what_it_needs()
    {
        var repository = new UiReviewRepository([]);
        var publisher = new NullPublisher();
        var inbox = new GetReviewInbox(repository);
        var resolve = new ResolveMatch(repository, publisher, SilentIdentification.Create());
        var reject = new RejectMatch(repository, publisher);

        Assert.Throws<ArgumentNullException>(() => new ReviewInboxViewModel(null!, resolve, reject));
        Assert.Throws<ArgumentNullException>(() => new ReviewInboxViewModel(inbox, null!, reject));
        Assert.Throws<ArgumentNullException>(() => new ReviewInboxViewModel(inbox, resolve, null!));
    }

    /// <summary>
    /// Setting a property to what it already holds is not a change, and the surface does not announce
    /// one: a list that says it changed is a list the screen rebuilds.
    /// </summary>
    [Fact]
    public async Task Repeating_a_value_announces_nothing()
    {
        var repository = new UiReviewRepository([
            Candidate("steady", 0.50, ReviewState.Pending, "Identification.Signal.Title"),
        ]);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);

        viewModel.ManualSearch = "La llegada";
        viewModel.ManualSearch = "La llegada";
        var card = Assert.Single(viewModel.Items);
        viewModel.SelectedItem = card;
        viewModel.SelectedItem = card;

        Assert.Equal(1, announced.Count(name => name == nameof(ReviewInboxViewModel.ManualSearch)));
        Assert.Equal(1, announced.Count(name => name == nameof(ReviewInboxViewModel.SelectedItem)));
    }

    /// <summary>
    /// An empty tray is the good outcome, and it is painted like one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §4 is explicit about this: the empty inbox is <b>the desirable state</b> — everything was
    /// identified without anybody being asked — so it takes <c>PositiveSurfaceBrush</c> and a glyph
    /// rather than being a blank panel that reads like something failed to load.
    /// </para>
    /// <para>
    /// Measured on 2026-08-21: the view had no empty state at all, while <c>IsEmpty</c> had existed on
    /// the model all along with <b>no view reading it</b>. And the two positive brushes were declared
    /// in all four themes and spent by nobody — this is what finally spends them.
    /// </para>
    /// <para>
    /// The list is asserted hidden in the same breath: an empty state that appears <em>above</em> an
    /// empty list is two ways of saying nothing at once.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task An_empty_tray_is_painted_as_the_good_outcome_it_is()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var viewModel = CreateViewModel(new UiReviewRepository([]));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsEmpty, "the repository was empty and the model does not say so.");

        var (window, view) = ShowInbox(viewModel);

        var surface = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "ReviewInboxEmptySurface");
        Assert.True(surface.IsEffectivelyVisible);
        Assert.Equal(ThemeColour("PositiveSurfaceBrush"), Colour(surface.Background));
        Assert.Equal(ThemeColour("PositiveBorderBrush"), Colour(surface.BorderBrush));
        Assert.True(surface.BorderThickness.Top > 0, "the empty state has no border, so colour is its only signal.");
        Assert.Contains(surface.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "✓");

        var painted = surface.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();
        Assert.Contains(Resource("ReviewInboxEmptyTitle"), painted, StringComparer.Ordinal);
        Assert.Contains(Resource("ReviewInboxEmptyDescription"), painted, StringComparer.Ordinal);

        var list = Assert.Single(
            view.GetVisualDescendants().OfType<ItemsControl>(),
            box => box.Name == "ReviewCandidates");
        Assert.False(list.IsEffectivelyVisible, "an empty list under an empty state says nothing twice.");

        window.Close();
    }

    /// <summary>
    /// With something to review, the list is what shows and the good news is not claimed.
    /// </summary>
    [AvaloniaFact]
    public async Task A_tray_with_something_in_it_shows_the_list_and_not_the_good_news()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var viewModel = CreateViewModel(new UiReviewRepository([
            Candidate("waiting", 0.62, ReviewState.Pending, "Identification.Signal.Title"),
        ]));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(viewModel.IsEmpty);

        var (window, view) = ShowInbox(viewModel);

        var surface = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "ReviewInboxEmptySurface");
        Assert.False(surface.IsEffectivelyVisible, "the tray has something in it and still says there is nothing.");

        var list = Assert.Single(
            view.GetVisualDescendants().OfType<ItemsControl>(),
            box => box.Name == "ReviewCandidates");
        Assert.True(list.IsEffectivelyVisible);

        window.Close();
    }

    /// <summary>Both sentences exist in both languages, and neither survived translation untranslated.</summary>
    [AvaloniaFact]
    public void The_empty_trays_words_are_written_in_both_languages()
    {
        var byLanguage = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var culture in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(culture));
            byLanguage[culture] = [Resource("ReviewInboxEmptyTitle"), Resource("ReviewInboxEmptyDescription")];
        }

        Assert.NotEqual(byLanguage["es-ES"][0], byLanguage["en-US"][0]);
        Assert.NotEqual(byLanguage["es-ES"][1], byLanguage["en-US"][1]);
    }

    private static (Window Window, ReviewInboxView View) ShowInbox(ReviewInboxViewModel viewModel)
    {
        var view = new ReviewInboxView { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static Color Colour(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current!;
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme, so nothing can paint it.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }

    private static ReviewInboxViewModel CreateViewModel(
        UiReviewRepository repository,
        SearchForMatch? manualSearch = null)
    {
        var publisher = new NullPublisher();
        return new ReviewInboxViewModel(
            new GetReviewInbox(repository),
            new ResolveMatch(repository, publisher, SilentIdentification.Create()),
            new RejectMatch(repository, publisher),
            manualSearch: manualSearch);
    }

    private static MatchCandidate Candidate(
        string stableKey,
        double score,
        ReviewState state,
        string explanation,
        string? path = null)
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
            [explanation],
            Revision: 0,
            IsDecisionLocked: false,
            path);
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

    /// <summary>
    /// What a search answers, and what it was asked. The candidate source is where a manual search
    /// ends up, so this is where "the words a person typed reached the provider" is observable.
    /// </summary>
    private sealed class UiCandidateSource : IIdentificationCandidateSource
    {
        public string? LastTitle { get; private set; }

        public int? LastYear { get; private set; }

        public Task<IReadOnlyList<CandidateFacts>> GetLocalAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default)
        {
            LastTitle = parsed.CleanTitle;
            LastYear = parsed.Year;
            return Task.FromResult<IReadOnlyList<CandidateFacts>>(
            [
                // Close, and not close enough to decide on its own: what a manual search is for is the
                // answer somebody still has to look at.
                new CandidateFacts(
                    CandidateId.FromStableKey("movie:329865"),
                    "movie:329865",
                    CandidateContentKind.Movie,
                    TitleSimilarity: 0.80,
                    SeasonMatch: null,
                    EpisodeMatch: null,
                    YearMatch: 1.0,
                    DurationMatch: null),
            ]);
        }

        public Task<IReadOnlyList<CandidateFacts>> GetRemoteAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CandidateFacts>>([]);
    }

    private sealed class UiReviewRepository(IEnumerable<MatchCandidate> candidates) : IMatchCandidateRepository
    {
        public List<MatchCandidate> Candidates { get; } = [.. candidates];

        /// <summary>The row is gone: another window decided it, or a rescan replaced it.</summary>
        public bool AnswerNotFound { get; init; }

        public Task ReplaceForMediaFileAsync(MediaFileId mediaFileId, IReadOnlyList<MatchCandidate> replacements, CancellationToken cancellationToken = default)
        {
            _ = Candidates.RemoveAll(candidate => candidate.MediaFileId == mediaFileId && !candidate.IsDecisionLocked);
            Candidates.AddRange(replacements);
            return Task.CompletedTask;
        }

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
            if (AnswerNotFound)
            {
                return Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.NotFound, null));
            }

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
