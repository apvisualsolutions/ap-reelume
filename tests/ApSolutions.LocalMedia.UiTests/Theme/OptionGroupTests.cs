// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.TestSupport;

using Avalonia.Controls;

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Where every group of options lives, and that each one can be put back from inside it (UX-010).
/// </summary>
/// <remarks>
/// <para>
/// Rule 11 has two halves and one closed list holds both. The first: a group whose value is chosen
/// while watching something lives in the player's gear, and one configured once lives in Settings.
/// <b>No test can judge that criterion</b> — «decided while watching» is not measurable — so what is
/// required is the written decision, exactly as <c>LeadingActionTests</c> requires a written leading
/// action. The second half is measurable and is asserted from both sides: a group off the list fails,
/// and a listed group without its button fails.
/// </para>
/// <para>
/// The markup is what is read rather than an assembled tree, and the reason is measured:
/// <c>PlayerView.axaml</c> mounts the gear through a <c>ContentControl</c> whose <c>Content</c> is a
/// binding, so without a data context the template never applies and a mounted enumeration sees
/// <b>not one</b> of the player's groups. A gate that saw the Settings sections and none of the gear's
/// would have approved precisely the half this work creates.
/// </para>
/// <para>
/// <b>What this gate cannot see, declared rather than left to be discovered:</b> a new block of
/// controls added inside a view that already exists creates neither an enum member nor a mounted
/// view, so legs A and B are both blind to it. That case has a gate already —
/// <c>eng/check-walk-coverage.ps1</c> fails on any control nobody presses — and a group is made of
/// controls. This one is not duplicated here; it is named here so nobody reads this file's silence
/// as coverage.
/// </para>
/// </remarks>
public sealed class OptionGroupTests
{
    /// <summary>Where rule 11 says a group belongs.</summary>
    private enum OptionPlace
    {
        /// <summary>Its value is chosen while watching something.</summary>
        Player,

        /// <summary>It is configured once, and not while watching a film.</summary>
        Settings,
    }

    /// <summary>
    /// One group of options: where it belongs, where it lives today, and the button that puts it back.
    /// </summary>
    /// <param name="Place">The rule 11 decision, which no test can judge.</param>
    /// <param name="Home">The destination it is reached through today.</param>
    /// <param name="Surface">The view its reset button lives inside.</param>
    /// <param name="ResetButton">The button's <c>x:Name</c>, or null while it is still pending.</param>
    /// <param name="Reason">Why it belongs where <paramref name="Place"/> says.</param>
    private sealed record OptionGroup(
        OptionPlace Place,
        string Home,
        string Surface,
        string? ResetButton,
        string Reason);

    /// <summary>
    /// The closed list: every group of options in the application, classified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Twelve, and the number is read from here rather than written beside it. «Fourteen» was in
    /// eleven places in this repository before anything was counted, and the ADR that carried it
    /// marked it unverified for a reason it had spotted itself: subtitle style and shortcuts were
    /// counted both as player groups and as Settings sections.
    /// </para>
    /// <para>
    /// The criterion a group has to meet, which is what settles every rejection below: it holds at
    /// least one value somebody chooses and that is stored; that value has a factory default this
    /// application can declare; and it appears and disappears as one unit, so its button has an
    /// unambiguous place inside it.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, OptionGroup> Groups = new(StringComparer.Ordinal)
    {
        // The player: chosen while watching something.
        ["Picture"] = new(
            OptionPlace.Player,
            "PlayerSettingsGroup.Picture",
            "PictureAdjustmentView",
            "PictureResetButton",
            "Brightness, contrast and gamma are judged against the picture itself: the number "
                + "somebody wants is the one that stops the film looking wrong."),
        ["SubtitleStyle"] = new(
            OptionPlace.Player,
            "SettingsSection.Subtitles",
            "SubtitleStyleView",
            null,
            "The right font size is the one that reads over this film at this distance, which is "
                + "not a question anybody can answer without something playing behind the text."),
        ["NextEpisodeCountdown"] = new(
            OptionPlace.Player,
            "SettingsSection.Playback",
            "PlaybackSettingsView",
            null,
            "How long to wait before the next episode is decided by having just sat through one."),
        ["SegmentDetection"] = new(
            OptionPlace.Player,
            "SettingsSection.SegmentDetection",
            "SegmentDetectionSettingsView",
            null,
            "Whether intros are skipped is judged the moment one is, or is not, skipped."),

        // Settings: configured once, and not while watching a film.
        ["Appearance"] = new(
            OptionPlace.Settings,
            "SettingsSection.Appearance",
            "AppearanceSettingsView",
            null,
            "Theme, accent, density and cover size dress the library, and nobody picks them in the "
                + "middle of a film."),
        ["Language"] = new(
            OptionPlace.Settings,
            "SettingsSection.Language",
            "LanguageSettingsView",
            null,
            "The interface's language is chosen once and governs every screen, so it belongs "
                + "nowhere near a film — and it needs a destination of its own rather than a card "
                + "inside Appearance, because two buttons saying the same thing on one screen is a "
                + "scene the walk cannot click."),
        ["Scanning"] = new(
            OptionPlace.Settings,
            "SettingsSection.Library",
            "ScanSettingsView",
            null,
            "Watching folders for changes is a property of the machine and its disks, not of "
                + "anything on screen."),
        ["Recommendations"] = new(
            OptionPlace.Settings,
            "SettingsSection.Recommendations",
            "RecommendationSettingsView",
            null,
            "What counts as watched is a rule over the whole library, and changing it rewrites "
                + "every title's state — the opposite of something tried against one film."),
        ["Shortcuts"] = new(
            OptionPlace.Settings,
            "SettingsSection.Shortcuts",
            "ShortcutSettingsView",
            "RestoreDefaultsButton",
            "A key map is learned once and used everywhere."),
        ["Lifecycle"] = new(
            OptionPlace.Settings,
            "SettingsSection.Lifecycle",
            "LifecycleSettingsView",
            null,
            "The tray icon and starting with Windows are about the machine's session, which is "
                + "settled before anything is played."),
        ["Privacy"] = new(
            OptionPlace.Settings,
            "SettingsSection.Privacy",
            "PrivacySettingsView",
            null,
            "A consent is given deliberately and away from anything else, which is the whole "
                + "reason this section leads with nothing either."),
        ["Updates"] = new(
            OptionPlace.Settings,
            "SettingsSection.Updates",
            "UpdateView",
            null,
            "Whether the application looks for a new version is decided once, and never by "
                + "watching one."),
    };

    /// <summary>
    /// What was looked at and is <b>not</b> a group, with the reason it was rejected.
    /// </summary>
    /// <remarks>
    /// This half is what makes the gate honest. Without it a destination that nobody classified is
    /// simply absent, and «decided against» reads exactly like «forgotten» — which is how the
    /// application came to have two reset controls in the first place.
    /// </remarks>
    private static readonly Dictionary<string, string> NotGroups = new(StringComparer.Ordinal)
    {
        ["SettingsSection.Backups"] = "Backing up and restoring are actions over the catalogue, not "
            + "preferences: there is no value here to put back, and «restore the backups to their "
            + "factory values» does not name anything. Measured on 2026-09-13: not one stored "
            + "setting behind either view.",
        ["SettingsSection.Credits"] = "A page of names and licences, with nothing to choose.",
        ["RootManagementView"] = "The library's folders are data somebody added, not a preference. "
            + "«Putting them back» is emptying the library — the most destructive thing this "
            + "application does, behind the most innocent word — which is the same trap ADR-0012 "
            + "rejects when it turns down a global «reset everything».",
        ["BackupView"] = "Three actions and a path; nothing stored to put back.",
        ["RestoreWizardView"] = "Restoring a backup is an operation, and the «restore» in its name "
            + "is a different verb from this one.",
        ["CreditsView"] = "Reading matter.",
        ["DiagnosticsPreviewView"] = "A read-only window onto what Privacy would send, mounted "
            + "inside that group rather than beside it. It has no control of its own.",
        ["AudioOutputView"] = "The output device is where sound is going right now, and the channel "
            + "layout is not this application's setting at all: measured on 2026-09-02, what "
            + "carries the choice is the endpoint's own format in Windows, which reaches every "
            + "program on the machine. A «restore» here would change the whole computer's audio.",
        ["TrackSelectorView"] = "Which track is playing is content, not a preference — there is no "
            + "factory audio track, there is the one the file carries. Its single check box says "
            + "whether to remember that choice for the series, which qualifies the storing rather "
            + "than being a value of its own.",
        ["TransportControlsView"] = "Playback speed is one control on a bar with its own undo next "
            + "to it, not a group: a group needs a panel, and a panel for a single control is a "
            + "worse interface than the reset that is already there.",
    };

    /// <summary>
    /// Groups that do not yet satisfy the rule, each with the reason. It is only allowed to shrink.
    /// </summary>
    /// <remarks>
    /// The symmetric half of this gate — «a listed group without its button fails» — has to exist
    /// <b>before</b> the buttons do, or every panel written between now and then is born without one,
    /// which is exactly how the application came to have two reset controls. So the debt is declared
    /// rather than hidden, the way <c>eng/walk-pending.txt</c> and <c>eng/coverage-debt.txt</c>
    /// already are, and the mechanism is deleted when the list empties.
    /// </remarks>
    private static readonly Dictionary<string, string> Pending = new(StringComparer.Ordinal)
    {
        ["SubtitleStyle"] = "No button yet, and still reached through Settings: it moves into the gear.",
        ["NextEpisodeCountdown"] = "No button yet, and still reached through Settings: it moves into the gear.",
        ["SegmentDetection"] = "No button yet, and still reached through Settings: it moves into the gear.",
        ["Appearance"] = "No button yet.",
        ["Language"] = "No button yet, and its destination does not exist: it is a card inside "
            + "Appearance today and needs a rail entry of its own.",
        ["Scanning"] = "No button yet.",
        ["Recommendations"] = "No button yet.",
        ["Lifecycle"] = "No button yet.",
        ["Privacy"] = "No button yet.",
        ["Updates"] = "No button yet.",
    };

    /// <summary>The ratchet over <see cref="Pending"/>, which only ever comes down.</summary>
    private const int MaximumPending = 10;

    /// <summary>
    /// Every resource key in the tree shaped like a reset, and what kind of thing each one undoes.
    /// </summary>
    /// <remarks>
    /// The census is closed because that is the only measurable form of «all of them say it through
    /// one key»: comparing the text would let two keys with the same words through, which is exactly
    /// how «Volver a 1×» and «Restaurar campos del proveedor» came to live side by side for one idea.
    /// A fifth key fails here rather than in a review nobody runs.
    /// </remarks>
    private static readonly Dictionary<string, string> RestoreShapedKeys = new(StringComparer.Ordinal)
    {
        ["RestoreDefaultsAction"] = "The one key every group's button says it with.",
        ["TransportSpeedResetAction"] = "«Volver a 1×» undoes one control on the bar, which is not "
            + "a group — see TransportControlsView in the rejections.",
        ["MetadataRestoreAction"] = "«Restaurar campos del proveedor» puts one title's record back "
            + "to what TMDB says, which is refetching data rather than restoring a preference.",
    };

    /// <summary>The one key every group's button says it with.</summary>
    private const string GroupResetKey = "RestoreDefaultsAction";

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Every_destination_is_either_a_group_of_options_or_written_off_as_not_one()
    {
        var destinations = Enum.GetNames<SettingsSection>()
            .Select(name => $"{nameof(SettingsSection)}.{name}")
            .Concat(Enum.GetNames<PlayerSettingsGroup>()
                .Where(name => name != nameof(PlayerSettingsGroup.None))
                .Select(name => $"{nameof(PlayerSettingsGroup)}.{name}"))
            .ToArray();

        // Anti-blindness floor: a reader that found no destinations would pass by measuring nothing.
        Assert.True(
            destinations.Length >= 12,
            $"only {destinations.Length} destinations were found across the two enums, so this gate "
                + "is reading the wrong thing rather than finding a small application.");

        var classified = Groups.Values
            .Select(group => group.Home)
            .Concat(NotGroups.Keys.Where(IsDestination))
            .ToHashSet(StringComparer.Ordinal);

        var undecided = destinations.Where(home => !classified.Contains(home)).ToArray();
        Assert.True(
            undecided.Length == 0,
            "these destinations are in the tree and nobody has decided whether they hold a group of "
                + "options, so rule 11 has not been applied to them:\n  "
                + string.Join("\n  ", undecided));

        // A group whose destination does not exist yet is admitted through Pending and nowhere else,
        // so «the rail entry has not been built» stays distinguishable from «the list has drifted».
        var promised = Groups
            .Where(entry => Pending.ContainsKey(entry.Key))
            .Select(entry => entry.Value.Home)
            .ToHashSet(StringComparer.Ordinal);

        var invented = classified
            .Where(home => !destinations.Contains(home, StringComparer.Ordinal))
            .Where(home => !promised.Contains(home))
            .ToArray();
        Assert.True(
            invented.Length == 0,
            "these are classified and no longer exist as destinations, so the list has drifted from "
                + "the tree:\n  " + string.Join("\n  ", invented));
    }

    [Fact]
    public void No_two_groups_answer_to_one_destination()
    {
        // Not hygiene: the walk resolves a control by its accessible name and refuses two visible
        // matches, so two groups reachable at once — each carrying a button that says the same thing
        // through the same key — is a scene that cannot be walked. The invariant is that restriction
        // written as data, and it fails here rather than forty minutes later in the walk.
        // Anti-blindness floor: this passes over an empty table by comparing nothing, and an empty
        // table is what a broken list looks like.
        Assert.True(
            Groups.Count >= 12,
            $"only {Groups.Count} groups are listed, which is fewer than the twelve that were "
                + "measured, so the list has lost entries rather than the application having shrunk.");

        var shared = Groups
            .GroupBy(entry => entry.Value.Home, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(entry => entry.Key))}")
            .ToArray();

        Assert.True(
            shared.Length == 0,
            "these destinations hold more than one group, so both reset buttons would be on screen "
                + "at once and neither could be clicked:\n  " + string.Join("\n  ", shared));
    }

    [Fact]
    public void Every_view_mounted_as_a_section_or_a_gear_group_is_named_by_the_list()
    {
        var mounted = MountedViews();

        // Anti-blindness floor: if the anchors stop matching, the reader returns nothing and would
        // report that the application mounts no options at all.
        Assert.True(
            mounted.Length >= 12,
            $"only {mounted.Length} views were found mounted as sections or gear groups, so this "
                + "gate is reading the wrong markup rather than finding a small application.");

        var named = Groups.Values
            .Select(group => group.Surface)
            .Concat(NotGroups.Keys.Where(key => !IsDestination(key)))
            .ToHashSet(StringComparer.Ordinal);

        // A second view mounted under a destination that already exists creates no enum member, so
        // leg A cannot see it — ScanSettingsView arrived exactly that way, under Library.
        var unnamed = mounted.Where(view => !named.Contains(view)).ToArray();
        Assert.True(
            unnamed.Length == 0,
            "these views are mounted where options are offered and no entry names them, so a group "
                + "may have been added without anybody deciding about it:\n  "
                + string.Join("\n  ", unnamed));
    }

    [Fact]
    public void Every_listed_group_carries_its_reset_button_and_every_such_button_belongs_to_a_group()
    {
        var buttons = ResetButtonsInTheTree();

        var declared = Groups
            .Where(entry => entry.Value.ResetButton is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        var missing = new List<string>();
        foreach (var (name, group) in declared)
        {
            var found = buttons.Where(button =>
                    string.Equals(button.View, group.Surface, StringComparison.Ordinal)
                    && string.Equals(button.Name, group.ResetButton, StringComparison.Ordinal))
                .ToArray();

            if (found.Length != 1)
            {
                missing.Add(
                    $"{name}: expected exactly one {group.ResetButton} saying {GroupResetKey} inside "
                        + $"{group.Surface}, found {found.Length}.");
            }
        }

        Assert.True(
            missing.Count == 0,
            "these groups are listed and their button is not where the list says:\n  "
                + string.Join("\n  ", missing));

        // The other side, and the one without which a panel is born buttonless and nobody notices.
        var orphans = buttons
            .Where(button => !declared.Values.Any(group =>
                string.Equals(group.Surface, button.View, StringComparison.Ordinal)
                && string.Equals(group.ResetButton, button.Name, StringComparison.Ordinal)))
            .Select(button => $"{button.View}#{button.Name}")
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"these buttons say {GroupResetKey} and no listed group claims them, so either a group "
                + "was added without being decided about or a button is in the wrong place:\n  "
                + string.Join("\n  ", orphans));

        // The floor that ties table and tree together, and the named canary beside it: an analysis
        // that stopped matching would otherwise agree with a table of nulls and call the whole
        // application pending-on-purpose.
        Assert.Equal(declared.Count, buttons.Length);
        Assert.Contains(buttons, button =>
            string.Equals(button.View, "PictureAdjustmentView", StringComparison.Ordinal));
    }

    [Fact]
    public void The_only_keys_shaped_like_a_reset_are_the_ones_that_were_decided()
    {
        var found = RestoreKeysInTheDictionaries();

        // Anti-blindness floor: a sweep that matched nothing would agree with an empty census.
        Assert.True(
            found.Length >= 1,
            "no key shaped like a reset was found in the string dictionaries, so this sweep is "
                + "reading the wrong file rather than finding an application without resets.");

        var undeclared = found.Where(key => !RestoreShapedKeys.ContainsKey(key)).ToArray();
        Assert.True(
            undeclared.Length == 0,
            "these keys are shaped like a reset and are not in the census, so a second way to say "
                + $"the same thing has appeared beside {GroupResetKey}:\n  "
                + string.Join("\n  ", undeclared));

        var gone = RestoreShapedKeys.Keys.Where(key => !found.Contains(key, StringComparer.Ordinal)).ToArray();
        Assert.True(
            gone.Length == 0,
            "these keys are in the census and no longer in the dictionaries, so the census has "
                + "drifted from the tree:\n  " + string.Join("\n  ", gone));
    }

    [Fact]
    public void Nothing_is_pending_that_the_list_does_not_admit_to()
    {
        var destinations = Enum.GetNames<SettingsSection>()
            .Select(name => $"{nameof(SettingsSection)}.{name}")
            .Concat(Enum.GetNames<PlayerSettingsGroup>().Select(name => $"{nameof(PlayerSettingsGroup)}.{name}"))
            .ToHashSet(StringComparer.Ordinal);

        // Three ways a group can fall short, and all three are debt rather than a difference of
        // opinion: no button, a button that is not where rule 11 puts the group, or a destination
        // that has not been built.
        var owed = Groups
            .Where(entry => entry.Value.ResetButton is null
                || !destinations.Contains(entry.Value.Home)
                || (entry.Value.Place == OptionPlace.Player
                    && entry.Value.Home.StartsWith(nameof(SettingsSection), StringComparison.Ordinal)))
            .Select(entry => entry.Key)
            .ToArray();

        var unadmitted = owed.Where(name => !Pending.ContainsKey(name)).ToArray();
        Assert.True(
            unadmitted.Length == 0,
            "these groups do not satisfy the rule and are not admitted as pending, so the gate would "
                + "be green over work nobody knows is owed:\n  " + string.Join("\n  ", unadmitted));

        var stale = Pending.Keys.Where(name => !owed.Contains(name, StringComparer.Ordinal)).ToArray();
        Assert.True(
            stale.Length == 0,
            "these are listed as pending and already satisfy the rule, so the list is only allowed "
                + "to shrink and has not:\n  " + string.Join("\n  ", stale));

        Assert.True(
            Pending.Count <= MaximumPending,
            $"{Pending.Count} groups are pending and the ratchet is {MaximumPending}; it only comes "
                + "down.");
    }

    /// <summary>
    /// Whether a rejection names a destination rather than a view, so the two namespaces the closed
    /// list mixes can be told apart when each half is crossed with the tree.
    /// </summary>
    private static bool IsDestination(string candidate) =>
        candidate.StartsWith($"{nameof(SettingsSection)}.", StringComparison.Ordinal)
        || candidate.StartsWith($"{nameof(PlayerSettingsGroup)}.", StringComparison.Ordinal);

    /// <summary>
    /// Every view mounted where options are offered: under the Settings page's own panel, and inside
    /// the player's gear.
    /// </summary>
    private static string[] MountedViews()
    {
        var presentation = typeof(ShellView).Assembly
            .GetTypes()
            .Where(type => typeof(UserControl).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        return Anchored("Shell/ShellView.axaml", "SettingsSections", presentation)
            .Concat(Anchored("Player/PlayerSettingsMenuView.axaml", "PlayerSettingsSurface", presentation))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(view => view, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// The views mounted below a named element, found by crossing the markup with the types the
    /// presentation assembly actually declares rather than with a list of names kept by hand.
    /// </summary>
    private static IEnumerable<string> Anchored(string relativePath, string anchor, HashSet<string> views)
    {
        var document = XDocument.Load(
            Path.Combine(RepositoryLayout.Root, "src", "ApSolutions.LocalMedia.Presentation", relativePath));

        var root = document.Descendants()
            .FirstOrDefault(element => (string?)element.Attribute(Xaml + "Name") == anchor);

        Assert.True(
            root is not null,
            $"{relativePath} has no element named {anchor}, so this gate has no place to start and "
                + "would report that nothing is mounted.");

        return root!.Descendants()
            .Select(element => element.Name.LocalName)
            .Where(views.Contains);
    }

    /// <summary>Every button in the tree that says «Restaurar valores por defecto», with its view.</summary>
    private static (string View, string Name)[] ResetButtonsInTheTree()
    {
        var found = new List<(string View, string Name)>();

        foreach (var path in Directory.EnumerateFiles(
            Path.Combine(RepositoryLayout.Root, "src"), "*.axaml", SearchOption.AllDirectories))
        {
            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            foreach (var button in document.Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Where(SaysTheGroupResetKey))
            {
                found.Add((
                    Path.GetFileNameWithoutExtension(path),
                    (string?)button.Attribute(Xaml + "Name") ?? "<unnamed>"));
            }
        }

        return [.. found];
    }

    /// <summary>
    /// Whether a button says the shared key, in its content or in the name a screen reader gets.
    /// Both are read, because a button that only carried it in one would still be that button.
    /// </summary>
    private static bool SaysTheGroupResetKey(XElement button) =>
        button.Attributes().Any(attribute =>
            attribute.Value.Contains($"Resource {GroupResetKey}", StringComparison.Ordinal));

    /// <summary>The keys in the Spanish dictionary whose text is a way of saying «put this back».</summary>
    private static string[] RestoreKeysInTheDictionaries()
    {
        var document = XDocument.Load(Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Resources",
            "Strings.es.axaml"));

        return [.. document.Descendants()
            .Where(element => element.Name.LocalName == "String")
            .Where(element => element.Value.StartsWith("Restaurar valores", StringComparison.Ordinal)
                || element.Value.StartsWith("Restaurar campos", StringComparison.Ordinal)
                || element.Value.StartsWith("Volver a 1", StringComparison.Ordinal))
            .Select(element => (string?)element.Attribute(Xaml + "Key") ?? string.Empty)
            .Where(key => key.Length > 0)
            .OrderBy(key => key, StringComparer.Ordinal)];
    }
}
