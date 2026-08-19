// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

/// <summary>
/// The mini player's five controls announce themselves, take focus, and say something different in
/// each language.
/// </summary>
/// <remarks>
/// The window is 480 logical pixels wide and the labels had to be short to fit five of them, which is
/// exactly the pressure that produces a control announced as nothing at all. So the name is asserted
/// in both languages rather than in the one the machine happens to be set to, and the two are
/// required to differ: a label that survived translation by not being translated is the failure this
/// catches.
/// </remarks>
public sealed class MiniPlayerChromeAutomationTests
{
    private static readonly string[] DeclaredControls =
    [
        "MiniPlayerPlayPause",
        "MiniPlayerSkipBack",
        "MiniPlayerSkipForward",
        "MiniPlayerRestore",
        "MiniPlayerClose",
    ];

    [AvaloniaFact]
    public void Every_mini_player_control_names_itself_and_takes_focus_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var namesByLanguage = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var view = new MiniPlayerChromeView();
            var window = new Window { Width = 480, Height = 270, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var controls = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(control => DeclaredControls.Contains(control.Name, StringComparer.Ordinal))
                .ToArray();

            Assert.Equal(DeclaredControls.Length, controls.Length);
            Assert.All(controls, control => Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                $"{control.Name} has no accessible name in {cultureName}."));
            Assert.All(controls, control => Assert.True(
                control.Focusable,
                $"{control.Name} cannot take focus, so the keyboard cannot reach it."));

            // The label and the name come from one key, so the visible text is what is announced.
            Assert.All(controls, control => Assert.Equal(
                AutomationProperties.GetName(control),
                control.Content as string));

            namesByLanguage[cultureName] = controls.ToDictionary(
                control => control.Name!,
                control => AutomationProperties.GetName(control)!,
                StringComparer.Ordinal);
            window.Close();
        }

        var untranslated = DeclaredControls
            .Where(name => string.Equals(
                namesByLanguage["es-ES"][name],
                namesByLanguage["en-US"][name],
                StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            untranslated.Length == 0,
            "These announce the same words in both languages, which is what an untranslated label "
                + $"looks like from here: {string.Join(", ", untranslated)}.");
    }
}
