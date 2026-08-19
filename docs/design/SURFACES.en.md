# Everything that is seen

An inventory of AP Reelume's visible surfaces, so a redesign can cover **all** of them and not only
the ones anybody remembers. The Spanish version is in [SURFACES.es.md](SURFACES.es.md).

This document decides no aesthetics. It states **what exists, where it lives and in which states it
appears**, measured from the tree on 2026-08-15 and measured again on **2026-08-18**, so nothing
visible is left undesigned.

## The rule that already holds, and needs no redoing

- **All 48 views use localised strings.** Not one carries untranslated text: the measurement found no
  view without `DynamicResource`.
- **470 string keys in Spanish and 470 in English**, in
  `src/ApSolutions.LocalMedia.Presentation/Resources/Strings.es.axaml` and `Strings.en.axaml`.
  `BilingualHeadingTests` compares the structure of the public documents, and a new visible string
  goes into both files or it does not go in.
- **The only three literal texts in the tree are symbols, not language**: `○ ◐ ●` (watched state),
  `→` (a rename's source and destination) and `!` (the transport's warning). If the redesign replaces
  them with icons, the accessible name still comes from `AutomationProperties`, which is already set.
- **Every interactive control has an accessible name**, and 80 accessibility tests require it. A
  redesign may change the shape, not take the name away.

## The 48 views, by area

| Area | Views |
| --- | --- |
| Shell (2) | `ShellView`, `StartupView` |
| Home (5) | `HomeView`, `ResumeHeroView`, `InProgressRailView`, `RecommendationsRailView`, `LibraryEntryView` |
| Library (2) | `LibraryView`, `UnavailableBadge` |
| Film card (1) | `MovieDetailsView` |
| Series card (2) | `ShowDetailsView`, `EpisodeRowView` |
| Player (16) | `PlayerView`, `TransportControlsView`, `VideoStatusOverlay`, `ResumePromptView`, `NextEpisodeOverlay`, `SkipMarkerButton`, `MarkerEditorView`, `DetectedMarkerReviewView`, `TrackSelectorView`, `AudioOutputView`, `SubtitleStyleView`, `ShortcutSettingsView`, `PlayerVersionsView`, `VersionSwitchDialog`, `LooseFileBanner`, `MiniPlayerWindow` |
| Settings (7) | `AppearanceSettingsView`, `PrivacySettingsView`, `ScanSettingsView`, `LifecycleSettingsView`, `RecommendationSettingsView`, `SegmentDetectionSettingsView`, `DiagnosticsPreviewView` |
| Review (3) | `ReviewInboxView`, `CandidateCardView`, `DuplicateReviewView` |
| Metadata (2) | `MetadataEditorView`, `RenamePreviewView` |
| Catalogue (2) | `PersonalActionsView`, `WatchStatusControl` |
| Backup (2) | `BackupView`, `RestoreWizardView` |
| First steps (1) | `RootOnboardingView` |
| Recovery (1) | `DatabaseRecoveryView` |
| Credits (1) | `CreditsView` |
| Update (1) | `UpdateView` |

All of them live in `src/ApSolutions.LocalMedia.Presentation/<area>/`.

## The surfaces that are not views

Eight things the user sees that have **no `.axaml`**: Windows draws their shape and it cannot be
redesigned. What is ours is **the text and the asset**, and that is exactly what is seen.

| Surface | What we decide |
| --- | --- |
| Tray icon and menu | The tooltip, the two menu entries, and which one a double click takes. The icon is the sixth asset (see below) |
| "Choose a media folder" dialog | The title and the initial folder |
| "Save the backup" dialog | The proposed name, with a date, and the type filter |
| "Open the backup" dialog | The filter and the initial folder |
| The handover with no dialog | When the run does not own the profile the folder is decided for it and the user sees nothing, so a confirmation afterwards has to say where the file ended up |
| Windows Explorer | The eight extensions under "Open with", the visible name, and the association icon |
| MSIX start-up | The splash screen and its background colour |
| Title bar and window chrome | The title, the remembered size and its minimum; the chrome is the system's on purpose, because that is what guarantees minimise, snap and close with the gestures the user already has |

## The update, which is more than it looks

`UpdateView` is **one view with twenty-three distinct messages**, and that is why it gets its own
section: a redesign covering only "checking" and "ready" leaves most of it out.

- **Process states (15)**: idle, checking, up to date, version available, offline, unusable version,
  downloading, ready, interrupted, verification failed, cancelled, unconfirmed, tampered with,
  handed to Windows, and launch refused.
- **Refusal reasons (8)**, the ones that explain why an update is **not** applied: insecure download,
  unusable hash, unsigned sums, wrong runtime, undeclared size, incomplete summary, and undeclared
  host.
- **Controls**: check, download, install, cancel, the automatic-check box, and the confirmation
  notice.
- There is a **progress bar** and the status area announces itself to screen readers
  (`LiveSetting="Polite"`): whatever the redesign puts there has to stay a live region, and stay in
  **one** container — splitting it in two splits the announcement.

A refusal **is not a user error**: it is the updater declining to install something it could not
verify. It deserves visual treatment distinct from a failure's.

## The player, which has no single state either

Measured on 2026-08-18, and the distinction matters because these are **two grammars, not one**:

- **`PlayerView` shows six failure reasons**, each with its own string and its own `TextBlock`: file
  not found, could not open, engine unavailable, unsupported codec, corrupted file, and no playable
  track. These are failures: **there is no picture**.
- `PlaybackFailureCode` has **seven** values, and the seventh — `UnsupportedCapability` — is **not
  one of those six**: it travels in `VideoOutputDecision` and surfaces through `VideoStatusOverlay`.
  The video **does play**, tone mapped; what is reported is that the format itself is out of scope. A
  redesign painting it as a failure would say there is no picture when there is one.
- **`VideoStatusOverlay` has six notices**: hardware acceleration, software fallback, HDR10
  passthrough, tone mapping, standard range, and unsupported format. These are not errors: they are
  the state of the picture.

## The lists and their empty state

Measured on 2026-08-18: **23 lists with data** in the tree — taking a list to be a `ListBox`,
`ItemsControl` or `ItemsRepeater` with `ItemsSource` — and **only four have a string written for
when they are empty**:

| List with an empty string | View that paints it |
| --- | --- |
| Library | `ShellView` (`EmptyLibraryTitle`, `EmptyLibraryDescription`) |
| In progress | `InProgressRailView` (`HomeInProgressEmpty`) |
| Recommendations | `RecommendationsRailView` (`RecommendationsEmpty`) |
| A series' episodes | `ShowDetailsView` (`ShowDetailsEmpty`) |

The **other nineteen say nothing when empty**, and none of them says anything while loading or when
it fails. A redesign has to decide those three states per list, not only the full one.

**And there is one nobody sees coming**: the library's empty state is painted by `ShellView`, not by
`LibraryView`, so **searching and finding nothing shows no text at all** — not even the empty-library
one, which would say something false anyway: the library is not empty, it is the search that finds
nothing.

## Absent is not the same as disabled

Twelve surfaces change shape with state, and the redesign has to be able to paint all of them. The
distinction is the one `PrivacySettingsView` already models: **absent** means the control does not
exist and leaves no gap; **disabled** means it exists and cannot be used.

| Surface | What changes | Grammar |
| --- | --- | --- |
| `PrivacySettingsView` | The automatic-refresh switch **does not exist** without consented connectivity (LIB-016): offering it would offer something that cannot happen | absent |
| `PrivacySettingsView` | The diagnostics preview, depending on whether there is anything to show | absent |
| `PrivacySettingsView` | Exporting diagnostics always exists, possible or not | disabled |
| `UpdateView` | Its four controls, by the state of the process | disabled |
| `UpdateView` | The confirmation notice, which exists only with a downloaded version | absent |
| `ShellView` | The eight Settings blocks | absent |
| `PlayerView` | The five optional panels in the side column | absent |
| `RootOnboardingView` | Its four forms: no roots, roots, confirming a removal, and asking consent | absent |
| `LooseFileBanner` | Only with a loose session | absent |
| `MetadataEditorView` | Its three messages | absent |
| `RecommendationsRailView` | Empty is not the same as switched off by setting | absent |
| `RestoreWizardView` | Only the missing root gains an editable field | absent |

**The two grammars share a screen** in privacy and in update, which is why they have to be told apart
by sight.

## The themes, measured

| Measure | Value |
| --- | --- |
| Dictionaries in `Theme/DesignTokens.axaml` | **4**: `Light`, `Dark`, `HighContrastLight` and `HighContrastDark` |
| Token declarations in the dictionaries | **344**, across **86 names**: 24 brushes and 62 aliases (12 for the button, 31 for the checkbox, 3 for lists, 16 for text fields) |
| Scalars, outside the dictionaries | **13** |
| Plus, in `Resources/Brand.axaml` | 3 (strings, no colours) |
| Focus selectors | **10**: `Button`, `ToggleButton`, `ToggleSwitch`, `RadioButton`, `TextBox`, `ComboBox`, `CheckBox`, `Slider`, `NumericUpDown`, `ListBoxItem` |
| Types with a disabled outline | **the same 10**, by adorner |

Four things the redesign has to know:

- **High contrast is two themes, and the system picks.** There used to be one dictionary, declared
  over `ThemeVariant.Light`, and **no path selected it**. Today `IHighContrastService` asks Windows
  and `FluentThemeService` switches to `HighContrastLight` or `HighContrastDark` by the luminance of
  `COLOR_WINDOW`, never by the theme's name, which is localised.
- **High contrast is not chosen in the application**: `ThemePreference` has three values and keeps
  them. It is a need declared to the system rather than a taste in this application, and offering a
  copy would create two sources of truth for one need.
- **The player ignores the chosen theme.** `PlayerThemeVariant` always returns `ThemeVariant.Dark`,
  on purpose, because a darkened room does not want a white interface. It is the one surface that
  does not obey the preference.
- **In high contrast, disabled is said with geometry and not with colour.** Neither palette has a
  third colour to spend — the disabled fill, the resting fill and the surface are one, the border is
  one for all four states, and the disabled text is the primary text — so the difference is the
  **dotted outline**, drawn as an adorner over the ten types.
- **Every control type gets in through its own resources, and no two are alike.** A button consumes
  **12** of the base theme's resources; a checkbox, **73**; a combo box, 59; a `RadioButton`, 38; a
  `ToggleButton`, 37; a `Slider`, 32. A `TextBox` has **2** of its own and a `ListBoxItem` **1**:
  those paint from **shared** system brushes. Assuming the next type works like the last is how this
  goes wrong.
- **One family of resources can be worth several types, and that is measured too.** `TextControl*`
  is taken by the `TextBox` (25 places) and the `NumericUpDown` (35, because it is a box with two
  arrows), and by **none** of the button, checkbox or slider. The `ComboBox` only touches it through
  the box it grows **when editable**, and the tree has none.
- **A shared brush is redirected by measuring who else takes it.** The three list ones
  (`SystemControlHighlightList*`) were checked by painting them a colour no theme uses and mounting
  twelve control types: **only the list consumes them**. And what decided the row's design: its
  content presenter **does** take the row's border by template binding, but its text comes from a
  generic brush, so a selected row's label colour **cannot be given** — hence a tinted fill and a
  border for the cue.

## The installation, which is also seen

What Windows shows while installing and in the Start menu comes from
`src/ApSolutions.LocalMedia.Windows.Package/`:

| Asset | Use | State |
| --- | --- | --- |
| `Assets/Square44x44Logo.png` | Taskbar, app list | 576 B — provisional |
| `Assets/Square150x150Logo.png` | Start menu tile | 1.7 KiB — provisional |
| `Assets/Wide310x150Logo.png` | Wide tile | 3.0 KiB — provisional |
| `Assets/StoreLogo.png` | Store listing | 628 B — provisional |
| `Assets/SplashScreen.png` | Splash screen | 7.0 KiB — provisional |
| `Presentation/Assets/tray-icon.png` | Tray icon | **The sixth, and it lives in another project** |

The first five are from 3 August and their size gives them away as placeholders, not branding. **It
is the first thing anyone sees of the product**, before any view.

**And five files are not five assets.** MSIX scales each one, so what has to be produced is **35**:
the 44 px square in five scales plus five target sizes and its unplated variant; the other four in
five scales each; and the tray one in five **real** sizes, 16/20/24/32/48, which are scales of
nothing. A tray icon with a painted background reads as a square the moment somebody changes the bar
colour, so that one needs real alpha.

The tile and splash background colour (`#111827`) is decided here too.

## What this document does not cover

- **The user manual** (`DOC-101`, `DOC-201`, `T44.1`-`T44.6`) is written from the built application,
  so its screenshots depend on the redesign and come afterwards.
- **The palette.** The colour values live in `Theme/DesignTokens.axaml` and in
  `Resources/Brand.axaml`; this inventory counts how many there are and which themes exist, it does
  not decide their values.
