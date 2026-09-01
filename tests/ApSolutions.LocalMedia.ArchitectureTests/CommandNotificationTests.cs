// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// The gate on commands that never announce themselves (ARQ-004).
/// </summary>
/// <remarks>
/// A private command class whose <c>CanExecuteChanged</c> has an empty add and remove throws every
/// subscription away, so a button bound to it asks once and keeps that first answer forever. The
/// silence is safe only while <c>CanExecute</c> ignores state that changes: a constant, or a
/// predicate that reads nothing but its own parameter, has nothing to announce.
/// <para>
/// <c>LibraryViewModel.BackCommand</c> is why this is a gate rather than a note. Its predicate reads
/// <c>Surface</c>, and the moment its two buttons were bound to the command the walk measured what
/// the empty event costs: <c>Volver a la biblioteca is on screen but cannot be pressed:
/// visible=True, enabled=False</c>. Both detail branches sit in the visual tree from the start, so
/// the command was asked while the surface was still Browse and was never asked again. It left this
/// list on 2026-08-18 by gaining a real event, which is the only way out.
/// </para>
/// <para>
/// The list works like the orphan list in <see cref="ServiceConsumptionTests"/> — it may only shrink
/// truthfully — with one addition: every entry carries the exact predicate that makes its silence
/// safe. Rewriting that predicate to read state fails this gate in the same change that introduces
/// it, and that is the whole point. The alternative was remembering.
/// </para>
/// </remarks>
public sealed class CommandNotificationTests
{
    /// <summary>
    /// Every file that declares an empty <c>CanExecuteChanged</c>, and each predicate it declares.
    /// A file earns its place here by asking a question whose answer cannot change.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> SilenceIsSafe =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/ApSolutions.LocalMedia.Presentation/Onboarding/RootOnboardingViewModel.cs"] =
                ["public bool CanExecute(object? parameter) => true;"],
            ["src/ApSolutions.LocalMedia.Presentation/Player/ShortcutSettingsViewModel.cs"] =
                ["public bool CanExecute(object? parameter) => true;"],
            ["src/ApSolutions.LocalMedia.Presentation/Recovery/DatabaseRecoveryViewModel.cs"] =
                [
                    "public bool CanExecute(object? parameter) => parameter is DatabaseRecoveryAction "
                    + "action && SafeActions.Contains(action);",
                ],
            // Four now, and the two new ones ask the same kind of question as the two that were
            // here: is this parameter one of the values this command takes. A colour is a colour or
            // it is not, and an enum member is defined or it is not — neither answer moves while a
            // button is on screen, which is what earns a place on this list.
            ["src/ApSolutions.LocalMedia.Presentation/Settings/AppearanceSettingsViewModel.cs"] =
                [
                    "public bool CanExecute(object? parameter) => parameter is \"es\" or \"en\";",
                    "public bool CanExecute(object? parameter) => parameter is ThemePreference;",
                    "public bool CanExecute(object? parameter) => AccentPalette.IsAccent(parameter as string);",
                    "public bool CanExecute(object? parameter) => parameter is T value && Enum.IsDefined(value);",
                ],
            // The three channel layouts, whose question is whether the parameter is one of the three
            // words the markup carries. Whether a layout can be CHOSEN is a different question and
            // a moving one — the driver answers it, and it changes when the output device does — so
            // it is asked by the button's IsEnabled binding, which notifies, rather than in here,
            // which does not.
            ["src/ApSolutions.LocalMedia.Presentation/Player/AudioOutputViewModel.cs"] =
                [
                    "public bool CanExecute(object? parameter) => parameter is string word "
                    + "&& Words.ContainsKey(word);",
                ],
            // The subtitle swatches, whose question is whether the parameter is a colour at all.
            ["src/ApSolutions.LocalMedia.Presentation/Player/SubtitleStyleViewModel.cs"] =
                [
                    "public bool CanExecute(object? parameter) => parameter is string value "
                    + "&& SubtitleStyle.IsColour(value);",
                ],
            ["src/ApSolutions.LocalMedia.Presentation/Settings/LifecycleSettingsViewModel.cs"] =
                ["public bool CanExecute(object? parameter) => true;"],
            // Two: the rail's routes and the player's five panel pills. A pill is drawn only when
            // its panel has something in it, so a pill that exists can always be pressed — the
            // command has nothing to change its mind about.
            ["src/ApSolutions.LocalMedia.Presentation/Shell/ShellViewModel.cs"] =
                [
                    "public bool CanExecute(object? parameter) => parameter is AppRoute;",
                    "public bool CanExecute(object? parameter) => parameter is PlayerPanel;",
                ],
            ["src/ApSolutions.LocalMedia.Windows/Tray/WindowsTrayService.cs"] =
                ["public bool CanExecute(object? parameter) => true;"],
        };

    [Fact]
    public void Every_command_that_never_announces_is_named_in_the_list()
    {
        var undeclared = FilesSilencingTheEvent()
            .Where(file => !SilenceIsSafe.ContainsKey(file))
            .ToArray();

        Assert.True(
            undeclared.Length == 0,
            "These declare a CanExecuteChanged that throws its subscriptions away, so a button bound "
            + "to them keeps the first answer forever: "
            + string.Join(", ", undeclared)
            + ". Use AsyncRelayCommand, or name the file here with the predicate that makes its "
            + "silence safe.");
    }

    [Fact]
    public void The_list_names_only_files_that_still_silence_the_event()
    {
        var silent = FilesSilencingTheEvent();

        var stale = SilenceIsSafe.Keys
            .Where(file => !silent.Contains(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "These entries no longer silence CanExecuteChanged; remove them so the list stays true: "
            + string.Join(", ", stale) + ".");
    }

    [Fact]
    public void A_predicate_that_started_reading_state_is_not_accepted_by_its_old_entry()
    {
        foreach (var (file, declared) in SilenceIsSafe)
        {
            var actual = PredicatesIn(file);
            var expected = declared.Select(Normalise).Order(StringComparer.Ordinal).ToArray();

            Assert.True(
                actual.SequenceEqual(expected, StringComparer.Ordinal),
                $"The predicates in {file} are no longer the ones this list vouches for. Declared: "
                + string.Join(" | ", expected)
                + ". Found: "
                + string.Join(" | ", actual)
                + ". If the new one reads state that changes, the command has to announce it — "
                + "LibraryViewModel.BackCommand is the worked example.");
        }
    }

    /// <summary>
    /// The scan's own floor. A reformat that stopped the pattern from matching would turn this gate
    /// green by silence, so it has to keep finding what it already knows is there — and it has to
    /// keep telling a silenced event apart from a real one, which is what the file that left the
    /// list proves.
    /// </summary>
    [Fact]
    public void The_scan_still_sees_the_commands_it_guards()
    {
        var silent = FilesSilencingTheEvent();

        Assert.Equal(SilenceIsSafe.Count, silent.Length);
        Assert.DoesNotContain(
            "src/ApSolutions.LocalMedia.Presentation/Library/LibraryViewModel.cs",
            silent);
        Assert.Contains(
            "src/ApSolutions.LocalMedia.Windows/Tray/WindowsTrayService.cs",
            silent);
    }

    private static string[] FilesSilencingTheEvent() =>
    [
        .. SourceFiles()
            .Where(file => EmptyNotification().IsMatch(File.ReadAllText(file)))
            .Select(Relative)
            .Order(StringComparer.Ordinal),
    ];

    private static string[] PredicatesIn(string relativePath)
    {
        var source = File.ReadAllText(RepositoryLayout.PathFromRoot(relativePath));

        return
        [
            .. Predicate()
                .Matches(source)
                .Select(match => Normalise(match.Value))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(
            RepositoryLayout.PathFromRoot("src"),
            "*.cs",
            SearchOption.AllDirectories);

    private static string Relative(string file) =>
        Path.GetRelativePath(RepositoryLayout.Root, file).Replace('\\', '/');

    /// <summary>Whitespace is not the rule; the question the predicate asks is.</summary>
    private static string Normalise(string text) =>
        Regex.Replace(text, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(2)).Trim();

    private static Regex EmptyNotification() => new(
        @"event\s+EventHandler\?\s+CanExecuteChanged\s*\{\s*add\s*\{\s*\}\s*remove\s*\{\s*\}\s*\}",
        RegexOptions.None,
        TimeSpan.FromSeconds(2));

    private static Regex Predicate() => new(
        @"public\s+bool\s+CanExecute\(object\?\s+parameter\)\s*=>[^;]+;",
        RegexOptions.Singleline,
        TimeSpan.FromSeconds(2));
}
