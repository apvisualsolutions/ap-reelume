using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Reviewing detections: the list shows what the detector found for one episode, accepting locks a
/// row, correcting validates before it saves, and deleting removes exactly one row.
/// </summary>
public sealed class DetectedMarkerReviewTests
{
    private static readonly SeriesId Series = new(Guid.Parse("c2d40001-0000-4000-8000-000000000001"));

    private static readonly MediaFileId File = new(Guid.Parse("c2d40001-0000-4000-8000-00000000000a"));

    [Fact]
    public async Task Loading_shows_the_detections_of_one_episode()
    {
        var intro = Row(MarkerKind.Intro, 10, 35);
        var credits = Row(MarkerKind.Credits, 150, 180);
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([intro, credits]));

        await viewModel.LoadAsync(File);

        Assert.Equal([intro, credits], viewModel.Detections);
        Assert.Null(viewModel.Selected);
    }

    [Fact]
    public async Task Accepting_replaces_the_row_with_its_locked_version()
    {
        var row = Row(MarkerKind.Intro, 10, 35);
        var locked = row with { UserCorrected = true };
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]),
            onAccept: _ => Task.FromResult<DetectedMarker?>(locked));
        await viewModel.LoadAsync(File);
        viewModel.Selected = row;

        viewModel.AcceptCommand.Execute(null);
        await Task.Yield();

        var shown = Assert.Single(viewModel.Detections);
        Assert.True(shown.UserCorrected);
    }

    [Fact]
    public async Task Correcting_applies_the_returned_row()
    {
        var row = Row(MarkerKind.Intro, 10, 35);
        var corrected = row with
        {
            Start = TimeSpan.FromSeconds(12),
            End = TimeSpan.FromSeconds(37),
            UserCorrected = true,
        };
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]),
            onCorrect: (_, _, _) => Task.FromResult<DetectedMarker?>(corrected));
        await viewModel.LoadAsync(File);
        viewModel.Selected = row;
        viewModel.StartSeconds = 12;
        viewModel.EndSeconds = 37;

        viewModel.CorrectCommand.Execute(null);
        await Task.Yield();

        var shown = Assert.Single(viewModel.Detections);
        Assert.Equal(TimeSpan.FromSeconds(12), shown.Start);
        Assert.True(shown.UserCorrected);
        Assert.False(viewModel.HasRangeError);
    }

    [Fact]
    public async Task An_impossible_correction_sets_the_error_and_changes_nothing()
    {
        var row = Row(MarkerKind.Intro, 10, 35);
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]),
            onCorrect: (_, _, _) => Task.FromResult<DetectedMarker?>(null));
        await viewModel.LoadAsync(File);
        viewModel.Selected = row;
        viewModel.StartSeconds = 40;
        viewModel.EndSeconds = 20;

        viewModel.CorrectCommand.Execute(null);
        await Task.Yield();

        Assert.True(viewModel.HasRangeError);
        Assert.Equal([row], viewModel.Detections);
    }

    [Fact]
    public async Task Deleting_removes_exactly_the_selected_row()
    {
        var intro = Row(MarkerKind.Intro, 10, 35);
        var credits = Row(MarkerKind.Credits, 150, 180);
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([intro, credits]),
            onDelete: _ => Task.FromResult(true));
        await viewModel.LoadAsync(File);
        viewModel.Selected = intro;

        viewModel.DeleteCommand.Execute(null);
        await Task.Yield();

        Assert.Equal([credits], viewModel.Detections);
        Assert.Null(viewModel.Selected);
    }

    [Fact]
    public async Task Without_handlers_or_a_selection_the_commands_do_nothing_and_break_nothing()
    {
        var row = Row(MarkerKind.Intro, 10, 35);
        var bare = new DetectedMarkerReviewViewModel();
        await bare.LoadAsync(File);
        Assert.Empty(bare.Detections);

        var loaded = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]),
            onAccept: _ => Task.FromResult<DetectedMarker?>(row with { UserCorrected = true }),
            onCorrect: (_, _, _) => Task.FromResult<DetectedMarker?>(row),
            onDelete: _ => Task.FromResult(true));
        await loaded.LoadAsync(File);

        // Nothing selected: every command returns without touching the list.
        loaded.AcceptCommand.Execute(null);
        loaded.CorrectCommand.Execute(null);
        loaded.DeleteCommand.Execute(null);
        await Task.Yield();
        Assert.Equal([row], loaded.Detections);

        // A selection but no handlers: same promise.
        var handlerless = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]));
        await handlerless.LoadAsync(File);
        handlerless.Selected = row;
        handlerless.AcceptCommand.Execute(null);
        handlerless.CorrectCommand.Execute(null);
        handlerless.DeleteCommand.Execute(null);
        await Task.Yield();
        Assert.Equal([row], handlerless.Detections);
    }

    [Fact]
    public async Task A_row_that_vanished_underneath_the_review_changes_nothing()
    {
        var row = Row(MarkerKind.Intro, 10, 35);
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]),
            onAccept: _ => Task.FromResult<DetectedMarker?>(null),
            onDelete: _ => Task.FromResult(false));
        await viewModel.LoadAsync(File);
        viewModel.Selected = row;

        viewModel.AcceptCommand.Execute(null);
        viewModel.DeleteCommand.Execute(null);
        await Task.Yield();

        Assert.Equal([row], viewModel.Detections);
        Assert.Equal(row, viewModel.Selected);
    }

    [Fact]
    public async Task A_result_for_a_row_the_list_no_longer_shows_is_added_not_lost()
    {
        var row = Row(MarkerKind.Intro, 10, 35);
        var replacement = Row(MarkerKind.Intro, 12, 37) with { UserCorrected = true };
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]),
            onAccept: _ => Task.FromResult<DetectedMarker?>(replacement));
        await viewModel.LoadAsync(File);
        viewModel.Selected = row;

        viewModel.AcceptCommand.Execute(null);
        await Task.Yield();

        Assert.Contains(replacement, viewModel.Detections);
        Assert.Equal(replacement, viewModel.Selected);
    }

    [Fact]
    public void The_commands_can_always_execute_and_say_so_when_asked()
    {
        var viewModel = new DetectedMarkerReviewViewModel();
        var raised = 0;
        viewModel.AcceptCommand.CanExecuteChanged += (_, _) => raised++;

        Assert.True(viewModel.AcceptCommand.CanExecute(null));
        Assert.True(viewModel.CorrectCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCommand.CanExecute(null));
        viewModel.AcceptCommand.Execute(null);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Selecting_the_same_row_twice_announces_it_once()
    {
        var row = Row(MarkerKind.Intro, 10, 35);
        var viewModel = new DetectedMarkerReviewViewModel(
            onLoad: _ => Task.FromResult<IReadOnlyList<DetectedMarker>>([row]));
        await viewModel.LoadAsync(File);
        var announced = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DetectedMarkerReviewViewModel.Selected))
            {
                announced++;
            }
        };

        viewModel.Selected = row;
        viewModel.Selected = row;

        Assert.Equal(1, announced);
    }

    private static DetectedMarker Row(MarkerKind kind, double startSeconds, double endSeconds) =>
        new(
            Guid.NewGuid(),
            Series,
            File,
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            Confidence: 0.8,
            DetectorVersion: 1,
            UserCorrected: false);
}
