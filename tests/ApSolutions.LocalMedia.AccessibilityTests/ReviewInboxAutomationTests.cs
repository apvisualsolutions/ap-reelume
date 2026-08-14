// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

public sealed class ReviewInboxAutomationTests
{
    [AvaloniaFact]
    public void Review_controls_have_names_roles_help_and_keyboard_gestures_in_both_languages()
    {
        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
            var view = new ReviewInboxView();
            var window = new Window { Width = 1024, Height = 720, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var controls = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control is Button or TextBox or ListBox)
                .ToArray();
            Assert.NotEmpty(controls);
            Assert.All(controls, control => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))));
            Assert.All(controls.Where(control => control is Button or TextBox), control => Assert.True(control.Focusable));
            Assert.Contains(
                view.GetVisualDescendants().OfType<ListBox>().Single().KeyBindings,
                binding => binding.Gesture is KeyGesture { Key: Key.Enter });
            Assert.Contains(view.KeyBindings, binding => binding.Gesture is KeyGesture { Key: Key.Escape });

            var treePath = Path.Combine(
                RepositoryLayout.Root,
                "artifacts",
                "ui-captures",
                "T14",
                $"review-uia-{cultureName}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(treePath)!);
            File.WriteAllLines(treePath, controls.Select(control =>
                $"{control.GetType().Name}|{AutomationProperties.GetName(control)}|Focusable={control.Focusable}"));
            window.Close();
        }
    }

    [Fact]
    public void Candidate_card_exposes_textual_state_score_and_explanation_not_color_alone()
    {
        var repositoryRoot = RepositoryLayout.Root;
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Review",
            "CandidateCardView.axaml"));

        Assert.Contains("ReviewStatusPending", xaml, StringComparison.Ordinal);
        Assert.Contains("ReviewStatusSuggested", xaml, StringComparison.Ordinal);
        Assert.Contains("ScorePercent", xaml, StringComparison.Ordinal);
        Assert.Contains("ExplanationCodes", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText", xaml, StringComparison.Ordinal);
    }
}
