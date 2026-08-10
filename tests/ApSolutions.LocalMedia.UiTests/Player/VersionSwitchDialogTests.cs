// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
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
/// The confirmation shown when progress cannot be carried across safely. It states why in words, offers
/// a way out, and never appears when the decision was unambiguous.
/// </summary>
public sealed class VersionSwitchDialogTests
{
    [AvaloniaFact]
    public void Nothing_is_asked_when_the_decision_needs_no_confirmation()
    {
        var view = Build(out var viewModel);
        var surface = view.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "VersionSwitchSurface");

        viewModel.Apply(ProgressTransferDecision.Exact(TimeSpan.FromMinutes(20)));
        Dispatcher.UIThread.RunJobs();

        Assert.False(viewModel.IsVisible);
        Assert.False(surface.IsVisible);
    }

    [AvaloniaFact]
    public void Each_reason_shows_exactly_its_own_line()
    {
        var view = Build(out var viewModel);
        var lines = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => !string.IsNullOrEmpty(block.Name))
            .ToDictionary(block => block.Name!, block => block);

        viewModel.Apply(ProgressTransferDecision.Confirm(
            TimeSpan.FromMinutes(65),
            ProgressTransferReason.LargeDifference));
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsVisible);
        Assert.True(lines["LargeDifferenceText"].IsVisible);
        Assert.False(lines["UnknownDurationText"].IsVisible);
        Assert.False(lines["IncompatibleStructureText"].IsVisible);
        Assert.Equal("01:05:00", lines["SuggestedPositionText"].Text);

        viewModel.Apply(ProgressTransferDecision.Confirm(
            TimeSpan.FromMinutes(5),
            ProgressTransferReason.UnknownDuration));
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["UnknownDurationText"].IsVisible);
        Assert.False(lines["LargeDifferenceText"].IsVisible);

        viewModel.Apply(ProgressTransferDecision.Confirm(
            TimeSpan.FromMinutes(5),
            ProgressTransferReason.IncompatibleStructure));
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["IncompatibleStructureText"].IsVisible);
        Assert.False(lines["UnknownDurationText"].IsVisible);
    }

    [AvaloniaFact]
    public void Every_button_reports_the_choice_it_names_and_closes_the_question()
    {
        var chosen = new List<VersionSwitchChoice>();
        var view = Build(out var viewModel, choice =>
        {
            chosen.Add(choice);
            return Task.CompletedTask;
        });
        var buttons = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => !string.IsNullOrEmpty(button.Name))
            .ToDictionary(button => button.Name!, button => button);

        viewModel.Apply(ProgressTransferDecision.Confirm(
            TimeSpan.FromMinutes(65),
            ProgressTransferReason.LargeDifference));
        Dispatcher.UIThread.RunJobs();
        buttons["ConfirmSwitchButton"].Command?.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(VersionSwitchChoice.Confirm, viewModel.Chosen);
        Assert.False(viewModel.IsVisible);

        viewModel.Apply(ProgressTransferDecision.Confirm(
            TimeSpan.FromMinutes(65),
            ProgressTransferReason.LargeDifference));
        Dispatcher.UIThread.RunJobs();
        buttons["RestartSwitchButton"].Command?.Execute(null);
        viewModel.Apply(ProgressTransferDecision.Confirm(
            TimeSpan.FromMinutes(65),
            ProgressTransferReason.LargeDifference));
        Dispatcher.UIThread.RunJobs();
        buttons["CancelSwitchButton"].Command?.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            [VersionSwitchChoice.Confirm, VersionSwitchChoice.Restart, VersionSwitchChoice.Cancel],
            chosen);
    }

    [AvaloniaFact]
    public void The_dialog_and_every_button_are_named_for_assistive_technology()
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
    public void The_dialog_is_captured_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var captures = Path.Combine(GetRepositoryRoot(), "artifacts", "ui-captures", "T27");
        _ = Directory.CreateDirectory(captures);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var viewModel = new VersionSwitchViewModel();
            var view = new VersionSwitchDialog { DataContext = viewModel };
            var window = new Window { Width = 520, Height = 260, Content = view };
            window.Show();
            viewModel.Apply(ProgressTransferDecision.Confirm(
                TimeSpan.FromMinutes(65),
                ProgressTransferReason.LargeDifference));
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(captures, $"version-switch-{cultureName}.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    private static VersionSwitchDialog Build(
        out VersionSwitchViewModel viewModel,
        Func<VersionSwitchChoice, Task>? onChosen = null)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        viewModel = new VersionSwitchViewModel(onChosen);
        var view = new VersionSwitchDialog { DataContext = viewModel };
        var window = new Window { Width = 520, Height = 260, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
