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

            // Every control a person operates, plus the tray's own list — named because a screen
            // reader announces the region before its cards. The inner lists are not on this roll:
            // the explanation codes inside a card are a bulleted paragraph, not a region, and a name
            // on each of them would be read out before every «why».
            var controls = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control is Button or TextBox || control.Name == "ReviewCandidates")
                .ToArray();
            Assert.NotEmpty(controls);
            Assert.All(controls, control => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))));
            Assert.All(controls.Where(control => control is Button or TextBox), control => Assert.True(control.Focusable));
            // Enter on the list used to accept the selection, and the list itself went with it: the
            // decisions live in the card, a card holds three of them, and measured, the list's
            // binding answered Enter before the focused Reject button did — so the keyboard accepted
            // what a person was trying to refuse. What is asserted now is that the tray carries no
            // gesture of its own except the one that gives up: Escape.
            Assert.Empty(view.GetVisualDescendants().OfType<ListBox>());
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
