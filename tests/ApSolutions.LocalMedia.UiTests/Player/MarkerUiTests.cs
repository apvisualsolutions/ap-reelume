// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The skip button and the marker editor. The button may only exist while the position is inside a
/// range, the editor works per series, and nothing anywhere offers automatic detection.
/// </summary>
public sealed class MarkerUiTests
{
    private static readonly SeriesId Series = new(Guid.Parse("d1f70001-0000-4000-8000-000000000001"));

    private static readonly SeriesId OtherSeries = new(Guid.Parse("d1f70002-0000-4000-8000-000000000002"));

    [AvaloniaFact]
    public void The_skip_button_only_exists_inside_its_range_whatever_the_episode_lasts()
    {
        var skipped = new List<TimeSpan>();
        var view = BuildButton(out var viewModel, target =>
        {
            skipped.Add(target);
            return Task.CompletedTask;
        });
        var button = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SkipMarkerButtonControl");

        // A fifty-minute episode: intro at 0:30-2:00, credits at 46:40-50:00.
        var shortEpisode = new[] { Marker(MarkerKind.Intro, 30, 120), Marker(MarkerKind.Credits, 2_800, 3_000) };
        viewModel.Apply(shortEpisode, TimeSpan.FromSeconds(10));
        Dispatcher.UIThread.RunJobs();
        Assert.False(button.IsVisible);

        viewModel.Apply(shortEpisode, TimeSpan.FromSeconds(60));
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsVisible);

        viewModel.Apply(shortEpisode, TimeSpan.FromSeconds(1_500));
        Dispatcher.UIThread.RunJobs();
        Assert.False(button.IsVisible);

        // A ninety-minute episode with its own ranges: the same control, different seconds.
        var longEpisode = new[] { Marker(MarkerKind.Intro, 60, 200), Marker(MarkerKind.Credits, 5_100, 5_400) };
        viewModel.Apply(longEpisode, TimeSpan.FromSeconds(120));
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsVisible);

        viewModel.Apply(longEpisode, TimeSpan.FromSeconds(3_000));
        Dispatcher.UIThread.RunJobs();
        Assert.False(button.IsVisible);
        Assert.Empty(skipped);
    }

    [AvaloniaFact]
    public void Skipping_asks_for_the_exact_end_of_the_range()
    {
        var skipped = new List<TimeSpan>();
        var view = BuildButton(out var viewModel, target =>
        {
            skipped.Add(target);
            return Task.CompletedTask;
        });
        var button = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SkipMarkerButtonControl");

        viewModel.Apply([Marker(MarkerKind.Credits, 2_800, 3_000)], TimeSpan.FromSeconds(2_900));
        Dispatcher.UIThread.RunJobs();
        button.Command?.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TimeSpan.FromSeconds(3_000), Assert.Single(skipped));
    }

    [AvaloniaFact]
    public void The_editor_shows_the_markers_of_one_series_and_reloads_when_the_series_changes()
    {
        var view = BuildEditor(out var viewModel);
        var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "MarkerList");

        viewModel.Load([Marker(MarkerKind.Intro, 30, 120), Marker(MarkerKind.Credits, 2_800, 3_000)], TimeSpan.FromMinutes(50));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, viewModel.Markers.Count);
        Assert.Equal(2, list.ItemCount);

        viewModel.Load([], TimeSpan.FromMinutes(90));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(viewModel.Markers);
        Assert.Equal(0, list.ItemCount);
    }

    [AvaloniaFact]
    public void An_invalid_range_and_an_overlap_are_both_stated_in_text()
    {
        var view = BuildEditor(
            out var viewModel,
            (kind, start, end, id) => Task.FromResult(start >= end
                ? new SaveManualMarkerResult(SaveMarkerOutcome.InvalidRange, null, null)
                : new SaveManualMarkerResult(SaveMarkerOutcome.Overlaps, null, Marker(kind, 30, 120))));
        var lines = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => IsOwnControl(block.Name))
            .ToDictionary(block => block.Name!, block => block);
        viewModel.Load([], TimeSpan.FromMinutes(50));

        viewModel.StartSeconds = 200;
        viewModel.EndSeconds = 100;
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["MarkerRangeErrorText"].IsVisible);
        Assert.False(lines["MarkerOverlapErrorText"].IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(lines["MarkerRangeErrorText"].Text));

        viewModel.StartSeconds = 100;
        viewModel.EndSeconds = 200;
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["MarkerOverlapErrorText"].IsVisible);
        Assert.False(lines["MarkerRangeErrorText"].IsVisible);
    }

    [AvaloniaFact]
    public void A_saved_marker_joins_the_list_and_a_deleted_one_leaves_it()
    {
        var saved = Marker(MarkerKind.Intro, 30, 120);
        var deleted = new List<Guid>();
        var view = BuildEditor(
            out var viewModel,
            (_, _, _, _) => Task.FromResult(new SaveManualMarkerResult(SaveMarkerOutcome.Saved, saved, null)),
            id =>
            {
                deleted.Add(id);
                return Task.FromResult(true);
            });
        viewModel.Load([], TimeSpan.FromMinutes(50));

        viewModel.StartSeconds = 30;
        viewModel.EndSeconds = 120;
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Single(viewModel.Markers);

        viewModel.SelectedMarker = viewModel.Markers[0];
        viewModel.DeleteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(saved.Id, Assert.Single(deleted));
        Assert.Empty(viewModel.Markers);
        _ = view;
    }

    /// <summary>
    /// Saving a marker that is already in the list replaces it in place rather than adding a second
    /// copy of it, which is what editing one does.
    /// </summary>
    /// <remarks>
    /// The other arm the walk was taking by accident. <c>SaveAsync</c> looks the saved marker up and
    /// either replaces it or appends it; every unit test until now saved a NEW marker, so only the
    /// appending arm was taken here and the replacing one was covered — some runs — by the
    /// autonomous walk editing an existing marker with a real mouse. That is the pair that made this
    /// file measure 79 % on one run and 81 % on the next with no code change between them.
    /// </remarks>
    [AvaloniaFact]
    public void Saving_a_marker_that_is_already_listed_replaces_it_instead_of_adding_a_second()
    {
        var existing = Marker(MarkerKind.Intro, 30, 120);
        // Same identity, different minutes: that is what makes the save replace instead of append.
        var edited = existing with
        {
            Start = TimeSpan.FromSeconds(45),
            End = TimeSpan.FromSeconds(150),
        };
        var view = BuildEditor(
            out var viewModel,
            (_, _, _, _) => Task.FromResult(new SaveManualMarkerResult(SaveMarkerOutcome.Saved, edited, null)));
        viewModel.Load([existing], TimeSpan.FromMinutes(50));
        Assert.Single(viewModel.Markers);

        viewModel.SelectedMarker = viewModel.Markers[0];
        viewModel.StartSeconds = 45;
        viewModel.EndSeconds = 150;
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // One row, not two, and it is the edited one: the identity is what decides, not the position.
        var row = Assert.Single(viewModel.Markers);
        Assert.Equal(existing.Id, row.Id);
        Assert.Equal(TimeSpan.FromSeconds(45), row.Start);
        Assert.Equal(TimeSpan.FromSeconds(150), row.End);
        _ = view;
    }

    /// <summary>
    /// A marker that leaves the list while its own deletion is in flight, which is the arm
    /// <c>DeleteAsync</c>'s index guard exists for.
    /// </summary>
    /// <remarks>
    /// This is written on purpose because it was being covered <b>by accident</b>, and that cost two
    /// red CI runs. <c>DeleteAsync</c> awaits the handler and only then looks the marker up again, so
    /// the arm where it is no longer there needs the list to change during the await. No unit test
    /// did that, and the only thing that ever took it was the autonomous walk pressing with a real
    /// mouse — which reaches that state some runs and not others. The file's branch coverage
    /// therefore oscillated between 79 % and 81 % with identical code, and the gate has no correct
    /// floor for a file that oscillates: at 79 it fails when the run measures 81, and at 81 it fails
    /// when the run measures 79. Both happened. Taking the arm deliberately here is what stops the
    /// number from moving, and it is a real state rather than a contrived one: anything that reloads
    /// the card mid-delete produces it.
    /// </remarks>
    [AvaloniaFact]
    public void Deleting_a_marker_that_left_the_list_meanwhile_removes_nothing_and_still_clears_the_selection()
    {
        var saved = Marker(MarkerKind.Intro, 30, 120);
        MarkerEditorViewModel? model = null;
        var deleted = new List<Guid>();
        var view = BuildEditor(
            out var viewModel,
            (_, _, _, _) => Task.FromResult(new SaveManualMarkerResult(SaveMarkerOutcome.Saved, saved, null)),
            id =>
            {
                deleted.Add(id);

                // The list is recomposed while the deletion is in flight, which is what a reload of
                // the card does. The handler still reports success: the marker IS gone, it just went
                // by another road.
                model!.Markers.Clear();
                return Task.FromResult(true);
            });
        model = viewModel;
        viewModel.Load([], TimeSpan.FromMinutes(50));

        viewModel.StartSeconds = 30;
        viewModel.EndSeconds = 120;
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        viewModel.SelectedMarker = viewModel.Markers[0];

        viewModel.DeleteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // It asked for the deletion and did not fail looking for a row that had already gone. A
        // RemoveAt on the -1 the lookup returns would have thrown at whoever was drawing.
        Assert.Equal(saved.Id, Assert.Single(deleted));
        Assert.Empty(viewModel.Markers);
        Assert.True(viewModel.IsEmpty);

        // And the selection is cleared either way, because the row it pointed at is gone in both.
        Assert.Null(viewModel.SelectedMarker);
        _ = view;
    }

    [AvaloniaFact]
    public void Every_control_is_named_and_takes_keyboard_focus()
    {
        var editor = BuildEditor(out var viewModel);
        viewModel.Load([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromMinutes(50));
        Dispatcher.UIThread.RunJobs();

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(editor)));

        // Only the controls this view declares are its responsibility; the internal parts of a
        // standard control belong to the framework and are audited end to end in T33.
        foreach (var button in editor.GetVisualDescendants().OfType<Button>().Where(b => IsOwnControl(b.Name)))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                $"{button.Name} has no automation name.");
            Assert.True(button.Focusable, $"{button.Name} cannot take keyboard focus.");
        }

        // A list is navigated through its items, which is where Avalonia puts the keyboard focus.
        var list = editor.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "MarkerList");
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(list)));
        var items = editor.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.True(item.Focusable));
    }

    [AvaloniaFact]
    public void The_editor_offers_no_automatic_detection_of_any_kind()
    {
        var editor = BuildEditor(out _);

        foreach (var named in editor.GetVisualDescendants().OfType<Control>().Where(c => IsOwnControl(c.Name)))
        {
            Assert.DoesNotContain("Detect", named.Name!, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            typeof(MarkerEditorViewModel).GetProperties(),
            property => property.Name.Contains("Detect", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(MarkerEditorViewModel).GetMethods(),
            method => method.Name.Contains("Detect", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void Both_surfaces_are_captured_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var captures = Path.Combine(RepositoryLayout.Root, "artifacts", "ui-captures", "T29");
        _ = Directory.CreateDirectory(captures);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));

            var editorViewModel = new MarkerEditorViewModel();
            var editor = new MarkerEditorView { DataContext = editorViewModel };
            var editorWindow = new Window { Width = 560, Height = 380, Content = editor };
            editorWindow.Show();
            editorViewModel.Load([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromMinutes(50));
            Dispatcher.UIThread.RunJobs();
            var editorFrame = editorWindow.CaptureRenderedFrame();
            Assert.NotNull(editorFrame);
            editorFrame.Save(
                Path.Combine(captures, $"marker-editor-{cultureName}.png"),
                PngBitmapEncoderOptions.Default);
            editorWindow.Close();

            var buttonViewModel = new SkipMarkerViewModel();
            var button = new SkipMarkerButton { DataContext = buttonViewModel };
            var buttonWindow = new Window { Width = 320, Height = 120, Content = button };
            buttonWindow.Show();
            buttonViewModel.Apply([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromSeconds(60));
            Dispatcher.UIThread.RunJobs();
            var buttonFrame = buttonWindow.CaptureRenderedFrame();
            Assert.NotNull(buttonFrame);
            buttonFrame.Save(
                Path.Combine(captures, $"skip-marker-{cultureName}.png"),
                PngBitmapEncoderOptions.Default);
            buttonWindow.Close();
        }
    }

    /// <summary>True for a control this view named itself, rather than a template part of a built-in one.</summary>
    /// <summary>
    /// The four guards that answer «there is nothing to do», and the search arm that walks past a row
    /// before finding the one it wants.
    /// </summary>
    /// <remarks>
    /// These are the branches that kept this file's figure moving. None of them is defensive: a model
    /// built with no handler is what a preview and an unassembled composition are, a skip with no
    /// active range is the state the button spends most of an episode in, and deleting anything but
    /// the first marker walks past one that does not match.
    /// <para>
    /// They are plain <c>[Fact]</c>s driving the models directly, because what they measure is a
    /// decision and not a screen. Until 2026-08-31 the only things that reached some of them were the
    /// autonomous walk — which arrives at that state some runs and not others — and nothing at all.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Nothing_happens_where_there_is_nothing_to_do()
    {
        // No range under the playhead, which is most of an episode: the command is bound and pressing
        // it must do nothing rather than throw.
        var skipped = 0;
        var idle = new SkipMarkerViewModel(_ =>
        {
            skipped++;
            return Task.CompletedTask;
        });
        await idle.SkipAsync();
        Assert.Equal(0, skipped);
        Assert.False(idle.IsVisible);

        // A range under the playhead but no handler: a composition that never wired one.
        var unwired = new SkipMarkerViewModel();
        unwired.Apply([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromSeconds(60));
        Assert.True(unwired.IsVisible);
        await unwired.SkipAsync();

        // And an editor with neither handler: Save and Delete answer by doing nothing.
        var editor = new MarkerEditorViewModel();
        editor.Load([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromMinutes(50));
        await editor.SaveAsync();
        await editor.DeleteAsync();
        Assert.Single(editor.Markers);
        Assert.False(editor.HasRangeError);
    }

    /// <summary>
    /// Delete with a handler and nothing selected, which is how the panel opens every time.
    /// </summary>
    [Fact]
    public async Task Deleting_with_nothing_selected_asks_the_handler_nothing()
    {
        var asked = 0;
        var editor = new MarkerEditorViewModel(onDelete: _ =>
        {
            asked++;
            return Task.FromResult(true);
        });
        editor.Load([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromMinutes(50));

        Assert.Null(editor.SelectedMarker);
        await editor.DeleteAsync();

        Assert.Equal(0, asked);
        Assert.Single(editor.Markers);
    }

    /// <summary>
    /// Deleting the second of two markers, so the search walks past one that does not match. Every
    /// other test deletes a list's only row, where the first row is already the answer.
    /// </summary>
    [Fact]
    public async Task Deleting_a_marker_that_is_not_the_first_walks_past_the_ones_before_it()
    {
        var editor = new MarkerEditorViewModel(onDelete: _ => Task.FromResult(true));
        var first = Marker(MarkerKind.Intro, 30, 120);
        var second = Marker(MarkerKind.Credits, 2_800, 3_000);
        editor.Load([first, second], TimeSpan.FromMinutes(50));
        editor.SelectedMarker = second;

        await editor.DeleteAsync();

        Assert.Equal([first.Id], editor.Markers.Select(marker => marker.Id));
        Assert.Null(editor.SelectedMarker);
    }

    /// <summary>
    /// A model nobody is listening to. Every other test here mounts a view, which subscribes, so the
    /// arm where <c>PropertyChanged</c> is null was reached by nothing deterministic.
    /// </summary>
    [Fact]
    public void A_model_with_no_listener_still_recomputes_its_state()
    {
        var button = new SkipMarkerViewModel();

        button.Apply([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromSeconds(60));
        Assert.True(button.IsVisible);
        Assert.True(button.IsIntro);

        button.Apply([Marker(MarkerKind.Intro, 30, 120)], TimeSpan.FromSeconds(600));
        Assert.False(button.IsVisible);
    }

    private static bool IsOwnControl(string? name) =>
        !string.IsNullOrEmpty(name) && !name.StartsWith("PART_", StringComparison.Ordinal);

    private static IntroMarker Marker(MarkerKind kind, double startSeconds, double endSeconds) =>
        new(
            Guid.Parse($"d1f7{(int)kind:D4}-{(int)startSeconds % 10000:D4}-4000-8000-000000000001"),
            Series,
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            MarkerOrigin.Manual,
            Confidence: null,
            UserCorrected: false);

    private static SkipMarkerButton BuildButton(
        out SkipMarkerViewModel viewModel,
        Func<TimeSpan, Task>? onSkip = null)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        viewModel = new SkipMarkerViewModel(onSkip);
        var view = new SkipMarkerButton { DataContext = viewModel };
        var window = new Window { Width = 320, Height = 120, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static MarkerEditorView BuildEditor(
        out MarkerEditorViewModel viewModel,
        Func<MarkerKind, TimeSpan, TimeSpan, Guid?, Task<SaveManualMarkerResult>>? onSave = null,
        Func<Guid, Task<bool>>? onDelete = null)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        viewModel = new MarkerEditorViewModel(onSave, onDelete);
        var view = new MarkerEditorView { DataContext = viewModel };
        var window = new Window { Width = 560, Height = 380, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }
}
