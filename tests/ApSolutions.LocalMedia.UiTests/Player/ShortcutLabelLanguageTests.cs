// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
/// <para>
/// <b>And differing is not enough on its own</b>, which an audit measured on 2026-09-02: deleting the
/// <c>ToggleMiniPlayer</c> arm drops it into the catch-all, so that row reads «Salir del modo actual»
/// / "Leave the current mode" — translated, different in the two languages, and belonging to another
/// command. Two labels being the <b>same</b> is what that looks like, so distinctness is asserted
/// alongside; and the count is exact rather than a floor, because a floor of nine tolerates one
/// command disappearing.
/// </para>
/// </remarks>
public sealed class ShortcutLabelLanguageTests
{
    [AvaloniaFact]
    public void Every_command_label_differs_between_the_two_languages()
    {
        var application = Avalonia.Application.Current!;
        var commands = Enum.GetValues<PlaybackInputCommand>();
        Assert.Equal(10, commands.Length);

        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
        var spanish = commands.Select(ShortcutSettingsViewModel.Describe).ToArray();

        App.ApplyLanguage(application, CultureInfo.GetCultureInfo("en-US"));
        var english = commands.Select(ShortcutSettingsViewModel.Describe).ToArray();

        // Ten commands, ten different sentences. A command whose arm is gone falls into the
        // catch-all and borrows another one's words, which shows up here and nowhere else.
        Assert.Equal(commands.Length, spanish.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(commands.Length, english.Distinct(StringComparer.Ordinal).Count());

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

    /// <summary>
    /// The collision sentence follows the language too, and it is the eleventh string.
    /// </summary>
    /// <remarks>
    /// It was a Spanish literal alongside the ten labels and the fix travelled with them, but nothing
    /// measured it — reverting <c>ShortcutConflictFormat</c> to a literal stayed green until this was
    /// written. It is the one whose format string cannot be cached, because the format <b>is</b> the
    /// language, so a gate that only ever provokes one collision would not see a cached one either.
    /// <para>
    /// <b>And comparing the two whole sentences is not enough</b>, measured while writing this: one
    /// of the holes is a command label, which <b>is</b> translated, so a Spanish literal frame still
    /// produces two different sentences and the obvious assertion passes. What has to differ is the
    /// <b>frame</b>, so both holes are blanked out of each sentence before they are compared — the
    /// same shape of mistake this whole file exists for, one level in.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_collision_sentence_differs_between_the_two_languages()
    {
        var application = Avalonia.Application.Current!;

        try
        {
            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
            var (spanish, spanishFrame) = Collide();

            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("en-US"));
            var (english, englishFrame) = Collide();

            Assert.False(string.IsNullOrWhiteSpace(spanish));
            Assert.False(string.IsNullOrWhiteSpace(english));

            // The words around the holes, which is the part a literal freezes.
            Assert.NotEqual(spanishFrame, englishFrame);

            // The two holes are filled, not left as the format's own braces.
            Assert.DoesNotContain("{0}", spanish, StringComparison.Ordinal);
            Assert.DoesNotContain("{1}", english, StringComparison.Ordinal);
        }
        finally
        {
            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
        }
    }

    private static (string Message, string Frame) Collide()
    {
        var viewModel = new ShortcutSettingsViewModel(new ShortcutMap());
        Assert.False(viewModel.TryRebind(PlaybackInputCommand.ToggleMute, new KeyGesture(Key.Space)));
        Assert.True(viewModel.HasConflict);

        var message = viewModel.ConflictMessage;
        var frame = message;

        // Blank out everything the sentence borrows from elsewhere: the gesture, which reads the
        // same in both languages, and whichever command label was substituted in, which does not.
        frame = frame.Replace(new KeyGesture(Key.Space).ToString(), "#", StringComparison.Ordinal);
        foreach (var command in Enum.GetValues<PlaybackInputCommand>())
        {
            frame = frame.Replace(ShortcutSettingsViewModel.Describe(command), "#", StringComparison.Ordinal);
        }

        return (message, frame);
    }
}
