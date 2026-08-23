// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Updates;

namespace ApSolutions.LocalMedia.Presentation.Shell;

/// <summary>
/// What the application hands the shell, in one place.
/// <para>
/// Some surfaces exist for the whole life of the window and arrive built; others describe one title,
/// one plan, or one playing session and cannot exist until there is something to describe, so they
/// arrive as a request the shell makes when a person asks for them. Every member is optional: a shell
/// with nothing in it still runs, which is what the first-run and the recovery paths rely on.
/// </para>
/// </summary>
public sealed record ShellSurfaces
{
    public AppearanceSettingsViewModel? AppearanceSettings { get; init; }

    public LibraryViewModel? Library { get; init; }

    public HomeViewModel? Home { get; init; }

    public RecommendationSettingsViewModel? RecommendationSettings { get; init; }

    public LifecycleSettingsViewModel? LifecycleSettings { get; init; }

    public BackupViewModel? Backups { get; init; }

    /// <summary>The duplicates destination's list; the per-title review stays on its own surface.</summary>
    public DuplicatesOverviewViewModel? DuplicatesOverview { get; init; }

    public RestoreWizardViewModel? Restore { get; init; }

    public PrivacySettingsViewModel? PrivacySettings { get; init; }

    /// <summary>Checking for a newer version, reading what changed, and confirming it.</summary>
    public UpdateViewModel? Updates { get; init; }

    /// <summary>Adding a folder to the library, which is the first thing a new install needs.</summary>
    public RootOnboardingViewModel? Onboarding { get; init; }

    /// <summary>
    /// Scans one root. Consenting to the first scan has to start one: the surface asks the question
    /// and records the answer, and without this a new install adds a folder and stays empty.
    /// </summary>
    public Func<LibraryRootId, CancellationToken, Task>? StartScan { get; init; }

    public ReviewInboxViewModel? ReviewInbox { get; init; }

    public ScanSettingsViewModel? ScanSettings { get; init; }

    public ShortcutSettingsViewModel? Shortcuts { get; init; }

    public SubtitleStyleViewModel? SubtitleStyle { get; init; }

    /// <summary>The switch for automatic segment detection; off until a person turns it on.</summary>
    public SegmentDetectionSettingsViewModel? SegmentDetection { get; init; }

    /// <summary>Builds the metadata editor of one title, or nothing when it has no metadata yet.</summary>
    public Func<TitleId, CancellationToken, Task<MetadataEditorViewModel?>>? OpenMetadataEditor { get; init; }

    /// <summary>Builds a rename preview for one title. Nothing is renamed by asking for it.</summary>
    public Func<TitleId, CancellationToken, Task<RenamePreviewViewModel?>>? OpenRename { get; init; }

    /// <summary>Builds the version comparison of one title, or nothing when it has a single version.</summary>
    public Func<TitleId, CancellationToken, Task<DuplicateReviewViewModel?>>? OpenDuplicates { get; init; }

    /// <summary>Opens one media file and returns everything that session puts on screen.</summary>
    public Func<PlayDetailsRequest, CancellationToken, Task<PlayerSurfaces?>>? OpenPlayer { get; init; }

    /// <summary>
    /// Opens one file that is not in the library, by path, and returns what that session puts on
    /// screen — which is deliberately less: the player, its transport, and the banner that says where
    /// this came from.
    /// </summary>
    /// <remarks>
    /// It is a second entry rather than a flag on <see cref="OpenPlayer"/> because the two differ in
    /// what they may touch, not in how they are called: a catalogued session resumes, tracks progress,
    /// carries markers and knows its other versions, and a loose one must do none of those — "a loose
    /// session leaves the database as it found it". Sharing one entry would put that promise behind an
    /// `if`, which is where promises go to be forgotten.
    /// </remarks>
    public Func<string, CancellationToken, Task<PlayerSurfaces?>>? OpenLoosePlayer { get; init; }

    /// <summary>Stops the session the shell is showing. Closing the surface must also stop the media.</summary>
    public Func<CancellationToken, Task>? ClosePlayer { get; init; }

    /// <summary>
    /// Moves the running session between embedded, fullscreen, and mini, and answers with the mode
    /// that ended up in force. The engine is never reopened, so nothing about the media changes.
    /// </summary>
    public Func<PlaybackMode, CancellationToken, Task<PlaybackMode>>? ChangePlaybackMode { get; init; }
}
