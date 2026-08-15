# Everything that is seen

An inventory of AP Reelume's visible surfaces, so that a redesign can cover **all** of them rather
than the ones that come to mind. The Spanish version is in [SURFACES.es.md](SURFACES.es.md).

This document decides no aesthetics. It says **what exists, where it lives, and in which states it
appears**, measured from the tree on 2026-08-15, so that nothing visible goes undesigned.

## The rule already met, and not to be redone

- **All 48 views use localised strings.** Not one has untranslated text: the measurement found no
  view without a `DynamicResource`.
- **468 string keys in Spanish and 468 in English**, in
  `src/ApSolutions.LocalMedia.Presentation/Resources/Strings.es.axaml` and `Strings.en.axaml`.
  `BilingualHeadingTests` compares the structure of the public documents, and a new visible string
  goes into both files or it does not go in.
- **The only three literal texts in the tree are symbols, not language**: `○ ◐ ●` (watched state),
  `→` (a rename's source and destination) and `!` (the transport's warning). If the redesign replaces
  them with icons, the accessible name still comes from `AutomationProperties`, which is already set.
- **Every interactive control has an accessible name**, and 80 accessibility tests require it. A
  redesign may change the shape, not remove the name.

## The 48 views, by area

| Area | Views |
| --- | --- |
| Shell (2) | `ShellView`, `StartupView` |
| Home (5) | `HomeView`, `ResumeHeroView`, `InProgressRailView`, `RecommendationsRailView`, `LibraryEntryView` |
| Library (2) | `LibraryView`, `UnavailableBadge` |
| Film entry (1) | `MovieDetailsView` |
| Series entry (2) | `ShowDetailsView`, `EpisodeRowView` |
| Player (16) | `PlayerView`, `TransportControlsView`, `VideoStatusOverlay`, `ResumePromptView`, `NextEpisodeOverlay`, `SkipMarkerButton`, `MarkerEditorView`, `DetectedMarkerReviewView`, `TrackSelectorView`, `AudioOutputView`, `SubtitleStyleView`, `ShortcutSettingsView`, `PlayerVersionsView`, `VersionSwitchDialog`, `LooseFileBanner`, `MiniPlayerWindow` |
| Settings (7) | `AppearanceSettingsView`, `PrivacySettingsView`, `ScanSettingsView`, `LifecycleSettingsView`, `RecommendationSettingsView`, `SegmentDetectionSettingsView`, `DiagnosticsPreviewView` |
| Review (3) | `ReviewInboxView`, `CandidateCardView`, `DuplicateReviewView` |
| Metadata (2) | `MetadataEditorView`, `RenamePreviewView` |
| Catalogue (2) | `PersonalActionsView`, `WatchStatusControl` |
| Backup (2) | `BackupView`, `RestoreWizardView` |
| Onboarding (1) | `RootOnboardingView` |
| Recovery (1) | `DatabaseRecoveryView` |
| Credits (1) | `CreditsView` |
| Update (1) | `UpdateView` |

They all live in `src/ApSolutions.LocalMedia.Presentation/<area>/`.

## The update, which is more than it looks

`UpdateView` is **one view with twenty-three distinct messages**, which is why it is named
separately: a redesign covering only "checking" and "ready" leaves most of it out.

- **Process states (15)**: idle, checking, up to date, offered, unreachable, unusable release,
  downloading, ready, interrupted, verification failed, cancelled, not confirmed, tampered, handed to
  Windows, and launch refused.
- **Refusal reasons (8)**, which are what explain why an update is **not** applied: insecure
  download, unusable hash, unsigned checksums, wrong runtime, undeclared size, incomplete summary and
  undeclared host.
- **Controls**: check, download, install, cancel, the automatic-check box and the confirmation notice.
- There is a **progress bar**, and the status area announces itself to screen readers
  (`LiveSetting="Polite"`): whatever the redesign puts there has to stay a live region.

A refusal **is not a user error**: it is the updater declining to install something it could not
verify. It deserves different visual treatment from a failure.

## The installation, which is also seen

What Windows shows on install and in the Start menu comes from
`src/ApSolutions.LocalMedia.Windows.Package/`:

| Asset | Use | State |
| --- | --- | --- |
| `Assets/Square44x44Logo.png` | Taskbar, app list | 576 B — placeholder |
| `Assets/Square150x150Logo.png` | Start menu tile | 1.7 KiB — placeholder |
| `Assets/Wide310x150Logo.png` | Wide tile | 3.0 KiB — placeholder |
| `Assets/StoreLogo.png` | Store listing | 628 B — placeholder |
| `Assets/SplashScreen.png` | Splash screen | 7.0 KiB — placeholder |

All five date from 3 August and their size gives them away as placeholders rather than branding. **It
is the first thing anyone sees of the product**, before any view.

**And there is a measured defect in the text.** The manifest declares both languages —
`<Resource Language="es-ES"/>` and `<Resource Language="en-US"/>` — but its description is **a single
string with a slash inside it**:

```xml
Description="Biblioteca y reproductor de vídeo local / Local video library and player"
```

Windows shows it exactly like that in both languages, slash included. Real localisation uses
`ms-resource:` and one resource per language, as winget already does: it **does** have its two
`locale.es-ES.yaml` and `locale.en-US.yaml` files with their own well-written descriptions. The tile
and splash background colour (`#111827`) is decided here too.

## What this document does not cover

- **The user manual** (`DOC-101`, `DOC-201`, `T44.1`-`T44.6`) is written from the built application,
  so its screenshots depend on the redesign and come afterwards.
- **Themes and colour tokens** live in `src/ApSolutions.LocalMedia.Presentation/Theme/` and in
  `Resources/Brand.axaml`; this inventory names the surfaces, not the palette.
