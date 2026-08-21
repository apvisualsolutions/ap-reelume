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
/// The window is 480 logical pixels wide and five short labels never fitted in it - which is what
/// §4's glyphs were for, and exactly the pressure that produces a control announced as nothing at
/// all. Since the five paint a glyph, the name is the only thing left carrying their identity, so it
/// is asserted in both languages rather than in the one the machine happens to be set to, and the two
/// are required to differ: a label that survived translation by not being translated is the failure
/// this catches.
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

            // Until 2026-08-21 the label and the name came from one key, and this asserted that the
            // visible text was what got announced. §4's glyphs separate the two on purpose, so what
            // is asserted now is the half that must not move: the name is still the word the key
            // holds, and the content is a glyph rather than that word. A name that had followed the
            // content would leave a screen reader announcing a private-use codepoint, which is the
            // one way this change could have broken five controls without any layout looking wrong.
            Assert.All(controls, control =>
            {
                Assert.True(
                    Avalonia.Application.Current!.TryFindResource(control.Name!, out var word),
                    $"{control.Name} names itself from a key that is not declared.");
                Assert.Equal(word as string, AutomationProperties.GetName(control));

                var content = Assert.IsType<string>(control.Content);
                Assert.Equal(1, content.Length);
                Assert.InRange(content[0], '\uE000', '\uF8FF');
                Assert.NotEqual(word as string, content);
            });

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
