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
            "PlaybackResetButton",
            "How long to wait before the next episode is decided by having just sat through one."),
        ["SegmentDetection"] = new(
            OptionPlace.Player,
            "SettingsSection.SegmentDetection",
            "SegmentDetectionSettingsView",
            "SegmentDetectionResetButton",
            "Whether intros are skipped is judged the moment one is, or is not, skipped."),

        // Settings: configured once, and not while watching a film.
        ["Appearance"] = new(
            OptionPlace.Settings,
            "SettingsSection.Appearance",
            "AppearanceSettingsView",
            "AppearanceResetButton",
            "Theme, accent, density and cover size dress the library, and nobody picks them in the "
                + "middle of a film."),
        ["Language"] = new(
            OptionPlace.Settings,
            "SettingsSection.Language",
            "LanguageSettingsView",
            "LanguageResetButton",
            "The interface's language is chosen once and governs every screen, so it belongs "
                + "nowhere near a film — and it needs a destination of its own rather than a card "
                + "inside Appearance, because two buttons saying the same thing on one screen is a "
                + "scene the walk cannot click."),
        ["Scanning"] = new(
            OptionPlace.Settings,
            "SettingsSection.Library",
            "ScanSettingsView",
            "ScanSettingsResetButton",
            "Watching folders for changes is a property of the machine and its disks, not of "
                + "anything on screen."),
        ["Recommendations"] = new(
            OptionPlace.Settings,
            "SettingsSection.Recommendations",
            "RecommendationSettingsView",
            "RecommendationResetButton",
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
            "LifecycleResetButton",
            "The tray icon and starting with Windows are about the machine's session, which is "
                + "settled before anything is played."),
        ["Privacy"] = new(
            OptionPlace.Settings,
            "SettingsSection.Privacy",
            "PrivacySettingsView",
            "PrivacyResetButton",
            "A consent is given deliberately and away from anything else, which is the whole "
                + "reason this section leads with nothing either."),
        ["Updates"] = new(
            OptionPlace.Settings,
            "SettingsSection.Updates",
            "UpdateView",
            "UpdateResetButton",
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
        ["NextEpisodeCountdown"] = "It has its button, and is still reached through Settings: it "
            + "moves into the gear.",
        ["SegmentDetection"] = "It has its button, and is still reached through Settings: it moves "
            + "into the gear.",
    };

    /// <summary>The ratchet over <see cref="Pending"/>, which only ever comes down.</summary>
    private const int MaximumPending = 3;

    /// <summary>
    /// Every button in the tree that puts something back, and what each one is allowed to say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The census is closed because that is the only measurable form of «all of them say it through
    /// one key». <b>What it is closed over is the markup and not the Spanish</b>, and that is the
    /// correction of 2026-09-13: it used to sweep the dictionary for text beginning «Restaurar
    /// valores», «Restaurar campos» or «Volver a 1», and a key reading «Restaurar ajustes
    /// predeterminados» — drawn on a real button, in a real panel the list still called
    /// buttonless — walked straight through with every suite green. A filter has to imagine in
    /// advance every thing that can go wrong, which is the reason rule 3 exists.
    /// </para>
    /// <para>
    /// A button is one of these when its own name or the command behind it says so, which is this
    /// repository's own convention rather than anybody's prose, and holds in both languages.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string> RestoreButtons = new(StringComparer.Ordinal)
    {
        ["PictureAdjustmentView#PictureResetButton"] = GroupResetKey,
        ["ShortcutSettingsView#RestoreDefaultsButton"] = GroupResetKey,
        ["PlaybackSettingsView#PlaybackResetButton"] = GroupResetKey,
        ["ScanSettingsView#ScanSettingsResetButton"] = GroupResetKey,
        ["SegmentDetectionSettingsView#SegmentDetectionResetButton"] = GroupResetKey,
        ["LifecycleSettingsView#LifecycleResetButton"] = GroupResetKey,
        ["PrivacySettingsView#PrivacyResetButton"] = GroupResetKey,
        ["UpdateView#UpdateResetButton"] = GroupResetKey,
        ["RecommendationSettingsView#RecommendationResetButton"] = GroupResetKey,
        ["AppearanceSettingsView#AppearanceResetButton"] = GroupResetKey,
        ["LanguageSettingsView#LanguageResetButton"] = GroupResetKey,

        // And the four that are NOT a group's reset, each with the reason it keeps a key of its own.
        // Three of these were invisible to the sweep by prose that this replaced, which is the
        // measurement that justifies the census being structural.
        //
        // «Volver a 1×» undoes one control on a bar, which is not a group — see TransportControlsView
        // in the rejections above.
        ["TransportControlsView#SpeedResetButton"] = "TransportSpeedResetAction",
        // «Restaurar campos del proveedor» refetches one title's record from TMDB. It puts back
        // data, not a preference, and it is not in any group's surface.
        ["MetadataEditorView#RestoreProviderMetadata"] = "MetadataRestoreAction",
        // «Confirmar restauración» runs a backup restore. Same verb in Spanish, different act: it
        // replaces the catalogue rather than putting a group's values back.
        ["RestoreWizardView#ConfirmRestoreButton"] = "RestoreConfirmLabel",
        // «Ampliar» restores the window from the small player. The word is Windows', not UX-010's.
        ["MiniPlayerChromeView#MiniPlayerRestore"] = "MiniPlayerRestore",
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
        var saying = RestoreButtonsInTheTree()
            .Where(button => string.Equals(button.Key, GroupResetKey, StringComparison.Ordinal))
            .Select(button => button.Identity)
            .ToArray();

        var declared = Groups
            .Where(entry => entry.Value.ResetButton is not null)
            .ToDictionary(
                entry => entry.Key,
                entry => $"{entry.Value.Surface}#{entry.Value.ResetButton}",
                StringComparer.Ordinal);

        var missing = declared
            .Where(entry => saying.Count(identity =>
                string.Equals(identity, entry.Value, StringComparison.Ordinal)) != 1)
            .Select(entry => $"{entry.Key}: expected exactly one {entry.Value} saying {GroupResetKey}")
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "these groups are listed and their button is not where the list says:\n  "
                + string.Join("\n  ", missing));

        // The other side, and the one without which a panel is born buttonless and nobody notices.
        var orphans = saying
            .Where(identity => !declared.ContainsValue(identity))
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"these buttons say {GroupResetKey} and no listed group claims them, so either a group "
                + "was added without being decided about or a button is in the wrong place:\n  "
                + string.Join("\n  ", orphans));

        // The floor that ties table and tree together, and the named canary beside it: an analysis
        // that stopped matching would otherwise agree with a table of nulls and call the whole
        // application pending-on-purpose.
        Assert.Equal(declared.Count, saying.Length);
        Assert.Contains("PictureAdjustmentView#PictureResetButton", saying, StringComparer.Ordinal);
    }

    [Fact]
    public void Every_button_that_puts_something_back_says_what_it_was_decided_to_say()
    {
        var found = RestoreButtonsInTheTree();

        // Anti-blindness floor, and it is the census itself: the two comparisons below already force
        // the set to be exactly the declared one, so a sweep that matched nothing fails on `gone`.
        // This keeps the number from quietly shrinking with the application.
        Assert.True(
            found.Length >= RestoreButtons.Count,
            $"only {found.Length} buttons that put something back were found across src/, fewer "
                + $"than the {RestoreButtons.Count} in the census — the sweep stopped matching.");

        var undeclared = found
            .Where(button => !RestoreButtons.ContainsKey(button.Identity))
            .Select(button => $"{button.Identity} says {button.Key}")
            .ToArray();
        Assert.True(
            undeclared.Length == 0,
            "these buttons put something back and are not in the census, so a second way of saying "
                + $"it may have appeared beside {GroupResetKey}:\n  " + string.Join("\n  ", undeclared));

        var gone = RestoreButtons.Keys
            .Where(identity => !found.Any(button =>
                string.Equals(button.Identity, identity, StringComparison.Ordinal)))
            .ToArray();
        Assert.True(
            gone.Length == 0,
            "these are in the census and no longer in the tree, so the census has drifted:\n  "
                + string.Join("\n  ", gone));

        var wrong = found
            .Where(button => !string.Equals(
                button.Key, RestoreButtons[button.Identity], StringComparison.Ordinal))
            .Select(button =>
                $"{button.Identity}: expected {RestoreButtons[button.Identity]}, says {button.Key}")
            .ToArray();
        Assert.True(
            wrong.Length == 0,
            "these buttons do not say what was decided, which is how two keys for one idea start:\n  "
                + string.Join("\n  ", wrong));
    }

    [Fact]
    public void Nothing_is_pending_that_the_list_does_not_admit_to()
    {
        // None is excluded here the same way the classification test excludes it: it is the gear
        // showing its list, not a group, and letting it stand as a Home in one test and not in the
        // other is how two readings of one enum start to drift.
        var destinations = Enum.GetNames<SettingsSection>()
            .Select(name => $"{nameof(SettingsSection)}.{name}")
            .Concat(Enum.GetNames<PlayerSettingsGroup>()
                .Where(name => name != nameof(PlayerSettingsGroup.None))
                .Select(name => $"{nameof(PlayerSettingsGroup)}.{name}"))
            .ToHashSet(StringComparer.Ordinal);

        // Three ways a group can fall short, and all three are debt rather than a difference of
        // opinion: no button, a destination that has not been built, or a group living somewhere
        // other than where rule 11 puts it. That last one is checked in BOTH directions — a Settings
        // group sitting in the gear is the same failure as a player group sitting in Settings, and
        // a rule about which of two places cannot be enforced over one of them.
        var owed = Groups
            .Where(entry => entry.Value.ResetButton is null
                || !destinations.Contains(entry.Value.Home)
                || entry.Value.Place != PlaceOf(entry.Value.Home))
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

        // Equal and not «at most», which is the difference between a ratchet and a ceiling: the two
        // assertions above already force the list to be exactly the debt, so a «<=» would let three
        // groups be fixed without the number moving and leave three silent slots behind. This is
        // what eng/check-coverage.ps1 means by failing at a floor that is short AND at one that is
        // long.
        Assert.Equal(MaximumPending, Pending.Count);
    }

    /// <summary>
    /// Whether a rejection names a destination rather than a view, so the two namespaces the closed
    /// list mixes can be told apart when each half is crossed with the tree.
    /// </summary>
    /// <summary>Which of the two places a destination belongs to, read off its own name.</summary>
    private static OptionPlace PlaceOf(string home) =>
        home.StartsWith($"{nameof(PlayerSettingsGroup)}.", StringComparison.Ordinal)
            ? OptionPlace.Player
            : OptionPlace.Settings;

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

        // The floor belongs to each anchor and not to the sum, which is the difference between a
        // gate and a gate that looks like one: Settings alone mounts fourteen views, so any floor
        // under that cannot tell «the gear offers a group» from «the gear offers nothing». Measured
        // on 2026-09-13 — replacing PictureAdjustmentView in the gear with anything else left this
        // whole file green when the floor was a single >= 12 over both.
        return Anchored("Shell/ShellView.axaml", "SettingsSections", presentation, minimum: 10)
            .Concat(Anchored(
                "Player/PlayerSettingsMenuView.axaml", "PlayerSettingsSurface", presentation, minimum: 1))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(view => view, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// The views mounted below a named element, found by crossing the markup with the types the
    /// presentation assembly actually declares rather than with a list of names kept by hand.
    /// </summary>
    private static string[] Anchored(
        string relativePath,
        string anchor,
        HashSet<string> views,
        int minimum)
    {
        var document = XDocument.Load(
            Path.Combine(RepositoryLayout.Root, "src", "ApSolutions.LocalMedia.Presentation", relativePath));

        var root = document.Descendants()
            .FirstOrDefault(element => (string?)element.Attribute(Xaml + "Name") == anchor);

        Assert.True(
            root is not null,
            $"{relativePath} has no element named {anchor}, so this gate has no place to start and "
                + "would report that nothing is mounted.");

        var found = root!.Descendants()
            .Select(element => element.Name.LocalName)
            .Where(views.Contains)
            .ToArray();

        Assert.True(
            found.Length >= minimum,
            $"only {found.Length} views are mounted under {anchor} in {relativePath}, which is fewer "
                + $"than the {minimum} that were measured there — so either this anchor stopped "
                + "matching or a place where options are offered has been emptied.");

        return found;
    }

    /// <summary>
    /// Every button in the tree that puts something back, with the resource key it says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What makes a button one of these is read off the markup and never off anybody's Spanish: its
    /// own name, or the command behind it. A key reading «Restaurar ajustes predeterminados» is
    /// caught here without this file knowing the word — measured on 2026-09-13, when a sweep by
    /// prose let exactly that through on a real button in a real panel.
    /// </para>
    /// <para>
    /// Any element whose local name ends in <c>Button</c> counts, not <c>Button</c> alone: a
    /// <c>ToggleButton</c> or a <c>HyperlinkButton</c> would otherwise be invisible while doing the
    /// same job.
    /// </para>
    /// </remarks>
    private static (string Identity, string Key)[] RestoreButtonsInTheTree()
    {
        var found = new List<(string Identity, string Key)>();

        foreach (var path in Directory.EnumerateFiles(
            Path.Combine(RepositoryLayout.Root, "src"), "*.axaml", SearchOption.AllDirectories))
        {
            // No silent skip: a view this cannot parse is a view it cannot vouch for, and the
            // compiler already refuses malformed markup — 70 of 70 parse today.
            var document = XDocument.Load(path);
            var view = Path.GetFileNameWithoutExtension(path);

            foreach (var button in document.Descendants()
                .Where(element => element.Name.LocalName.EndsWith("Button", StringComparison.Ordinal))
                .Where(PutsSomethingBack))
            {
                found.Add(($"{view}#{NameOf(button)}", KeySaidBy(button)));
            }
        }

        return [.. found];
    }

    /// <summary>
    /// Whether a button is one that puts something back: its own name says so, or the command it is
    /// bound to does. Both are this repository's own naming, which is why neither is a guess.
    /// </summary>
    private static bool PutsSomethingBack(XElement button)
    {
        var command = (string?)button.Attribute("Command") ?? string.Empty;

        return Mentions(NameOf(button)) || Mentions(command);

        static bool Mentions(string value) =>
            value.Contains("Reset", StringComparison.Ordinal)
            || value.Contains("Restore", StringComparison.Ordinal);
    }

    /// <summary>
    /// A control's name, which this tree writes both ways — <c>x:Name</c> and a bare <c>Name</c>.
    /// Reading only the prefixed one left a real button anonymous, which is how it slipped past the
    /// census the first time it ran.
    /// </summary>
    private static string NameOf(XElement element) =>
        (string?)element.Attribute(Xaml + "Name")
        ?? (string?)element.Attribute("Name")
        ?? "<unnamed>";

    /// <summary>
    /// The resource key a button says, looked for in its attributes <b>and</b> in the elements
    /// inside it.
    /// </summary>
    /// <remarks>
    /// The descendants are read because putting the label in a child is how a button gets an icon
    /// beside its text, and it is the ordinary shape in this tree. Reading attributes alone made
    /// a button with its content written as a <c>TextBlock</c> invisible to this gate while the
    /// list still called its panel buttonless — measured on 2026-09-13.
    /// </remarks>
    private static string KeySaidBy(XElement button)
    {
        var candidates = button.DescendantsAndSelf()
            .SelectMany(element => element.Attributes())
            .Select(attribute => attribute.Value);

        foreach (var value in candidates)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                value,
                @"^\{(?:Dynamic|Static)Resource\s+([^}]+)\}$",
                System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(2));

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return "<no resource key>";
    }
}
