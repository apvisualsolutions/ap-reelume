// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Which button leads each screen, decided once and written down.
/// </summary>
/// <remarks>
/// <para>
/// <c>primary-action</c> is the only part of the redesign that could not be swept: what a screen's
/// leading action is, is that screen's own decision. So the decision lives here as a closed table of
/// every view in the tree, and the test refuses both a view that has stopped leading with what it
/// used to and a view nobody has decided about. A new view fails until somebody chooses.
/// </para>
/// <para>
/// It is asserted as the <b>only</b> one per view rather than as present: two leading actions is a
/// screen that has not decided what it is for, and that would pass a check that merely looked for one.
/// </para>
/// <para>
/// The markup is what is read, because <c>ControlStateTests</c> already proves the class paints the
/// accent in all four themes. What is unproven without this is which button wears it.
/// </para>
/// </remarks>
public sealed class LeadingActionTests
{
    /// <summary>
    /// Every view, and the one button that is the point of it — or nothing, with the reason.
    /// </summary>
    /// <remarks>
    /// The reasons for an empty entry fall into six kinds, and every one of them is a decision:
    /// <list type="bullet">
    /// <item>a frame rather than a screen (<c>ShellView</c>);</item>
    /// <item>a row or card that repeats — a hierarchy repeated N times is not a hierarchy
    /// (<c>EpisodeRowView</c>, <c>PlayerVersionsView</c>);</item>
    /// <item>a block on a screen that has already accented something else: Home mounts four views and
    /// Continue wears its one accent, so <c>LibraryEntryView</c> leads with nothing even though it is
    /// a single block with a single button. <c>HomeLayoutTests</c> asserts the assembled screen has
    /// exactly one, which is the half a per-view table cannot see;</item>
    /// <item>chrome whose buttons alternate by state, so a leading action would move with what is
    /// happening (<c>TransportControlsView</c>, <c>MiniPlayerChromeView</c>);</item>
    /// <item>mutually exclusive options rather than actions (<c>AppearanceSettingsView</c>,
    /// <c>WatchStatusControl</c>);</item>
    /// <item>one button alone, which has nothing to be ranked against (<c>SkipMarkerButton</c>,
    /// <c>ShortcutSettingsView</c>, <c>RecommendationSettingsView</c>);</item>
    /// <item>and a consent, where accenting the affirmative is a dark pattern — that is the whole
    /// reason <c>LifecycleSettingsView</c> and <c>PrivacySettingsView</c> lead with nothing, in an
    /// application whose point is that it sends nothing anywhere.</item>
    /// </list>
    /// </remarks>
    private static readonly Dictionary<string, string?> Leading = new(StringComparer.Ordinal)
    {
        // Shell
        ["ShellView"] = null,
        ["StartupView"] = null,

        // Home
        ["HomeView"] = null,
        ["ResumeHeroView"] = "ResumeHeroAction",
        ["InProgressRailView"] = null,
        ["RecentlyAddedRailView"] = null,
        ["RecommendationsRailView"] = null,
        ["LibraryEntryView"] = null,

        // Library
        ["LibraryView"] = null,
        // Managing the folders is upkeep, not the point of Settings: nothing leads.
        ["RootManagementView"] = null,
        ["UnavailableBadge"] = null,
        ["PosterCardView"] = null,

        // Details
        ["MovieDetailsView"] = "MovieResumeAction",
        ["ShowDetailsView"] = null,
        ["EpisodeRowView"] = null,

        // Player
        ["PlayerView"] = "PlayerRecoveryRetry",
        ["TransportControlsView"] = null,
        ["VideoStatusOverlay"] = null,
        ["ResumePromptView"] = "ResumeButton",
        ["NextEpisodeOverlay"] = "PlayNextNowButton",
        ["SkipMarkerButton"] = null,
        ["MarkerEditorView"] = "SaveMarkerButton",
        ["DetectedMarkerReviewView"] = "AcceptDetectionButton",
        ["TrackSelectorView"] = null,
        ["AudioOutputView"] = null,
        ["SubtitleStyleView"] = null,
        ["ShortcutSettingsView"] = null,
        ["PlayerVersionsView"] = null,
        ["VersionSwitchDialog"] = "ConfirmSwitchButton",
        ["LooseFileBanner"] = "AddContainingFolderButton",
        ["MiniPlayerWindow"] = null,
        ["MiniPlayerChromeView"] = null,

        // Settings
        ["AppearanceSettingsView"] = null,
        ["PrivacySettingsView"] = null,
        ["ScanSettingsView"] = null,
        ["LifecycleSettingsView"] = null,
        ["RecommendationSettingsView"] = null,
        ["SegmentDetectionSettingsView"] = null,
        ["DiagnosticsPreviewView"] = null,

        // Review
        ["ReviewInboxView"] = "AcceptReviewAction",
        ["CandidateCardView"] = null,
        ["DuplicateReviewView"] = null,

        // Metadata
        ["MetadataEditorView"] = "MetadataSaveAction",
        ["RenamePreviewView"] = "RenameExecuteAction",

        // Catalog
        ["PersonalActionsView"] = null,
        ["WatchStatusControl"] = null,

        // Backup
        ["BackupView"] = "CreateCopyButton",
        ["RestoreWizardView"] = "ConfirmRestoreButton",

        // Onboarding, recovery, credits, updates
        ["RootOnboardingView"] = "RootAddAction",
        ["DatabaseRecoveryView"] = "RecoveryOpenBackupFolder",
        ["CreditsView"] = null,
        ["UpdateView"] = "UpdateCheckButton",
    };

    /// <summary>Views that carry no leading action, which is most of them and each on purpose.</summary>
    private static readonly Regex ButtonPattern =
        new(@"<Button\b.*?(?:/>|</Button>)", RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_view_leads_with_the_button_it_was_decided_to_lead_with()
    {
        // A view is decided by its root element rather than by a list of exceptions: the tree also
        // holds Application, Styles and ResourceDictionary files, and a named exclusion list is a
        // place for a real view to hide the day somebody adds one that does not fit.
        var views = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.axaml", SearchOption.AllDirectories)
            .Where(path => IsView(File.ReadAllText(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        // Anti-blindness floor: a reader that found no views would pass by measuring nothing.
        Assert.True(
            views.Length >= 40,
            $"only {views.Length} views were found under src/, so this gate is reading the wrong "
                + "thing rather than finding a small application.");

        var undecided = new List<string>();
        var wrong = new List<string>();
        var accentedTotal = 0;

        foreach (var path in views)
        {
            var view = Path.GetFileNameWithoutExtension(path);
            var markup = File.ReadAllText(path);

            var accented = ButtonPattern.Matches(markup)
                .Where(match => match.Value.Contains("primary-action", StringComparison.Ordinal))
                .Select(match => Identify(match.Value))
                .ToArray();
            accentedTotal += accented.Length;

            if (!Leading.TryGetValue(view, out var expected))
            {
                undecided.Add(
                    $"{view} is in the tree and not in the table, so nobody has decided whether it "
                        + $"leads with anything (it currently accents {accented.Length}).");
                continue;
            }

            var wanted = expected is null ? Array.Empty<string>() : [expected];
            if (!accented.SequenceEqual(wanted, StringComparer.Ordinal))
            {
                wrong.Add(
                    $"{view}: expected [{string.Join(", ", wanted)}] to lead, found "
                        + $"[{string.Join(", ", accented)}]");
            }
        }

        Assert.True(
            undecided.Count == 0,
            "these views have no entry in the table:\n  " + string.Join("\n  ", undecided));

        Assert.True(
            wrong.Count == 0,
            "these views do not lead with what was decided:\n  " + string.Join("\n  ", wrong));

        // The second floor, and the one that catches a pattern that stopped matching: seventeen
        // buttons wear the class, so a reader that found none would otherwise agree with a table of
        // nulls and call the whole application undecided-on-purpose.
        var declared = Leading.Values.Count(value => value is not null);
        Assert.Equal(declared, accentedTotal);
        Assert.True(
            accentedTotal >= 17,
            $"only {accentedTotal} buttons wear primary-action across the tree, which is fewer than "
                + "the 17 that were decided.");
    }

    /// <summary>
    /// Whether an <c>.axaml</c> is a view: a <c>UserControl</c> or a <c>Window</c>, and not one of the
    /// application, style or dictionary files that share the extension.
    /// </summary>
    private static bool IsView(string markup) =>
        Regex.IsMatch(
            markup,
            @"<(UserControl|Window)\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

    /// <summary>
    /// The identity a button is known by: its <c>x:Name</c>, or the resource key behind its accessible
    /// name, which is what the walk aims at and what the coverage gate counts.
    /// </summary>
    private static string Identify(string button)
    {
        // The lookbehind is not decoration: without it this matches inside
        // AutomationProperties.Name and returns "{DynamicResource X}" as the button's own name.
        var name = Regex.Match(
            button,
            @"(?<![\w.])(?:x:)?Name=""([^""]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));
        if (name.Success)
        {
            return name.Groups[1].Value;
        }

        var key = Regex.Match(
            button,
            @"AutomationProperties\.Name=""\{(?:Dynamic|Static)Resource ([^}""]+)\}""",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

        return key.Success ? key.Groups[1].Value.Trim() : "<unidentified>";
    }
}
