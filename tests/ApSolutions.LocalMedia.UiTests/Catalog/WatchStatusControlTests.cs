// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Catalog;

/// <summary>
/// The watch status is shown as an icon and words, never colour alone, and the manual buttons say what
/// they will do. These tests drive each state and check that exactly its own line is visible.
/// </summary>
public sealed class WatchStatusControlTests
{
    [AvaloniaFact]
    public void Each_state_shows_exactly_its_own_line_with_an_icon_and_words()
    {
        var view = Build(out var viewModel);
        var lines = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => !string.IsNullOrEmpty(block.Name))
            .ToDictionary(block => block.Name!, block => block);

        viewModel.Apply(WatchStatus.NotStarted, isManualOverride: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["NotStartedText"].IsVisible);
        Assert.False(lines["InProgressText"].IsVisible);
        Assert.False(lines["WatchedText"].IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(lines["NotStartedText"].Text));
        Assert.False(string.IsNullOrWhiteSpace(lines["NotStartedGlyph"].Text));

        viewModel.Apply(WatchStatus.InProgress, isManualOverride: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["InProgressText"].IsVisible);
        Assert.False(lines["NotStartedText"].IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(lines["InProgressGlyph"].Text));

        viewModel.Apply(WatchStatus.Watched, isManualOverride: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["WatchedText"].IsVisible);
        Assert.False(lines["InProgressText"].IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(lines["WatchedGlyph"].Text));
    }

    [AvaloniaFact]
    public void A_manual_decision_is_announced_in_text_and_can_be_undone()
    {
        var view = Build(out var viewModel);
        var manual = view.GetVisualDescendants().OfType<TextBlock>().Single(b => b.Name == "ManualOverrideText");
        var clear = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ClearOverrideButton");

        viewModel.Apply(WatchStatus.Watched, isManualOverride: false);
        Dispatcher.UIThread.RunJobs();
        Assert.False(manual.IsVisible);
        Assert.False(clear.IsEffectivelyEnabled);

        viewModel.Apply(WatchStatus.Watched, isManualOverride: true);
        Dispatcher.UIThread.RunJobs();
        Assert.True(manual.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(manual.Text));
        Assert.True(clear.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void Every_button_requests_the_change_it_names()
    {
        var requested = new List<WatchStatus?>();
        var view = Build(out var viewModel, status =>
        {
            requested.Add(status);
            return Task.CompletedTask;
        });
        var buttons = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => !string.IsNullOrEmpty(button.Name))
            .ToDictionary(button => button.Name!, button => button);

        viewModel.Apply(WatchStatus.InProgress, isManualOverride: false);
        Dispatcher.UIThread.RunJobs();
        buttons["MarkWatchedButton"].Command?.Execute(null);
        buttons["MarkNotStartedButton"].Command?.Execute(null);
        viewModel.Apply(WatchStatus.NotStarted, isManualOverride: true);
        Dispatcher.UIThread.RunJobs();
        buttons["ClearOverrideButton"].Command?.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([WatchStatus.Watched, WatchStatus.NotStarted, null], requested);
    }

    [AvaloniaFact]
    public void Every_control_is_named_for_assistive_technology()
    {
        var view = Build(out _);

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(view)));
        foreach (var button in view.GetVisualDescendants().OfType<Button>())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                $"{button.Name} has no automation name.");
        }
    }

    [AvaloniaFact]
    public void The_control_is_captured_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var captures = Path.Combine(RepositoryLayout.Root, "artifacts", "ui-captures", "T26");
        _ = Directory.CreateDirectory(captures);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var viewModel = new WatchStatusViewModel();
            var view = new WatchStatusControl { DataContext = viewModel };
            var window = new Window { Width = 420, Height = 200, Content = view };
            window.Show();
            viewModel.Apply(WatchStatus.Watched, isManualOverride: true);
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(captures, $"watch-status-{cultureName}.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    private static WatchStatusControl Build(
        out WatchStatusViewModel viewModel,
        Func<WatchStatus?, Task>? onChanged = null)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        viewModel = new WatchStatusViewModel(onChanged);
        var view = new WatchStatusControl { DataContext = viewModel };
        var window = new Window { Width = 420, Height = 200, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }
}
