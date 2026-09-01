// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The shortcut list speaks the language the application is set to.
/// </summary>
/// <remarks>
/// <b>It spoke Spanish in both until 2026-09-02.</b> Ten command labels and the key-collision
/// sentence were literals in <c>ShortcutSettingsViewModel</c>, so the whole of that list read in
/// Spanish with the application in English. Nothing went red for it: the bilingual gates read the
/// views' markup, and a visible string living in a <c>.cs</c> file is outside what they look at.
/// <para>
/// So this asserts the property those gates cannot: every label differs between the two languages,
/// which is what a translated string does and what a literal cannot. Asserting that one of them
/// equals a particular word would pass just as well with the other nine still hard-coded.
/// </para>
/// </remarks>
public sealed class ShortcutLabelLanguageTests
{
    [AvaloniaFact]
    public void Every_command_label_differs_between_the_two_languages()
    {
        var application = Avalonia.Application.Current!;
        var commands = Enum.GetValues<PlaybackInputCommand>();
        Assert.True(commands.Length >= 9, $"only {commands.Length} commands were read.");

        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
        var spanish = commands.Select(ShortcutSettingsViewModel.Describe).ToArray();

        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("en-US"));
        var english = commands.Select(ShortcutSettingsViewModel.Describe).ToArray();

        var untranslated = commands
            .Where((_, index) => string.Equals(spanish[index], english[index], StringComparison.Ordinal))
            .Select(command => command.ToString())
            .ToArray();

        Assert.True(
            untranslated.Length == 0,
            "These read the same in Spanish and English, which is what a hard-coded literal does: "
            + string.Join(", ", untranslated));

        // Every label is a label, in both. Checking that none equals its enumeration member's name
        // was tried first and is wrong: «Stop» is the correct English word for Stop, so that check
        // fails on a translation that is right — the accident of a good name matching a good word.
        Assert.All(spanish, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.All(english, label => Assert.False(string.IsNullOrWhiteSpace(label)));

        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
    }
}
