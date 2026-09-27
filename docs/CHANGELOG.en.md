# Changelog

Every notable change to AP Reelume, for the people who use it. The Spanish version is at
[CHANGELOG.es.md](CHANGELOG.es.md).

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the versioning is
[SemVer](https://semver.org/).

## [Unreleased] / [Sin publicar]

### Added

- **Courses.** A folder of lessons can be marked as a course and appears in a section of its own,
  **Courses**, with one card per course. The course page says where you left off and what you
  watched last, and each lesson can be marked as watched or unmarked. While playing, the player's
  **Lessons** panel shows the whole course, and finishing a lesson offers the next one. Each course's
  picture is taken from its own video.
- **Your own cover.** In a title's editor, a button opens the Windows file browser and the image you
  choose becomes the cover; a later query to the provider does not overwrite it.
- **Where each cover comes from, your choice.** In Settings, **Cover order** ranks the three places a
  cover can come from — yours, the provider's and a frame of the video — and a title's editor can
  choose for that title alone, without a later change of the general order undoing that choice.
- **A movie or show with no cover shows a frame of its own video.** It is taken in the background,
  gives way to playback or a scan, and each file is decoded only once while it does not change.
- **Brightness, contrast and gamma** during playback, to lift a dark video. Leaving them as they come
  does not change the picture.
- **Noise reduction.** Next to brightness and gamma, it takes out the blocks compression leaves in a
  low-quality video, which are the ones that show when it is lifted. It starts off and is remembered
  like the rest of the picture. If the computer cannot clean every frame in time, the application
  cleans it a little less rather than let the video stutter.
- **A video smaller than its window looks sharper.** The application enlarges it with a scaler of its
  own that comes switched on and works on any graphics card; switching it off returns the picture
  exactly as it was.
- **Player settings, over the video.** What is decided while watching — speed, tracks, picture,
  subtitle style, the next episode's wait, intro detection, shortcuts — opens from the player's gear,
  without leaving the film. Every group of options, there and in Settings, carries its own **Restore
  default values** button.
- **The next episode, your way.** You can decide whether the next episode starts on its own and how
  many seconds it waits.
- **Audio channels.** Stereo, 5.1 and 7.1 can be chosen when the output device supports them; when
  it does not, the application says so.
- **Appearance.** Theme, following the Windows theme, accent colour, Mica background, accent tint,
  density, cover size, corner rounding, titles under covers, animations and player surface. The
  language has its own section in Settings.
- **Updates.** The application can check for a new version, by hand or automatically; it comes
  switched off. Before offering a version it verifies the signature over its hashes, and when it
  refuses one it says why.
- **A scan can be stopped from the screen**, it says when it has finished, and the library warns
  about a folder it cannot read.
- **Floating window.** The mini player is a frameless window you can drag.
- **During playback everything but the picture hides**, and comes back when the mouse moves or a key
  is pressed. Double-clicking the picture and the `F` key enter and leave full screen.
- Every button says what it does when the pointer rests on it.
- `.flv` files join the library.

### Changed

- **A licence of its own, free for whoever uses it.** AP Reelume is no longer free software: it is
  still delivered free of charge and its code can be examined, but reading it grants no right to
  modify or redistribute it. What was published before 2026-09-13 keeps the rights it was published
  with. The text is in [LICENSE](../LICENSE).
- **The video engine is built without GPL code**, from the same VLC version, and plays the same
  formats as before, subtitles included. The third-party notices and the text of every licence
  travel inside the package.
- **Ratings are five stars.**
- **The library stretches its covers to fill each row**, and the screens follow the application's
  design: icons, buttons, menus and the marks over covers.

### Removed

- **Teletext is no longer decoded.** Both of VLC's teletext decoders carry GPL code, so the
  GPL-free engine does not include them.

### Fixed

- Switching to another version of a film and choosing **Start again** now starts it from the
  beginning next time too: sometimes a minute of the previous version stayed stored.
- Closing the application after watching a video no longer ends in a crash.
- The player controls hide by themselves after three seconds without mouse movement while the film
  plays, and the pointer with them.
- A video added to a library folder appears on its own, without restarting the application, and an
  external drive or a network folder is checked again every so often.
- The playback speed is remembered, and no longer leaks into films that did not ask for it.
- Restoring the provider's data no longer deletes the cover you chose.
- In full screen the video fills the screen, and it no longer distorts when the window is resized.
- HD video colour is decoded with the matrix it belongs to.
- Subtitles that live next to the file are loaded, and reach the screen.
- The library no longer stops at the fiftieth title.
- The folder list no longer says "Available" with the drive unplugged.
- Home loads at startup and is no longer empty with a full library.
- Removing a folder warns how much is lost and does what it promises.
- The player's shortcut list speaks the application's language.

## [0.1.0] — 2026-08-04

The first installable artifact. It catalogues, identifies, plays, and remembers where you left off,
in Spanish and English, with no account and without sending anything.

### Added

- **Local library.** Local, USB, and UNC/NAS folders in their original location, copying and moving
  no video. Initial, startup, manual, and incremental scanning, cancellable and resumable, with
  continuous watching and a fallback scan for drives watching does not cover.
- **Hybrid identification.** Movie, show, season, and episode detection from names and folders, with
  TMDB metadata in Spanish and a fallback language. Confidence thresholds: automatic from 90%,
  suggested between 60% and 89%, pending below. Anything ambiguous goes to a review inbox.
- **Duplicates as versions.** No file is deleted or hidden; a version is chosen by quality and
  availability.
- **Protected metadata and artwork editing,** and optional rename with preview, audit log, and undo.
- **Embedded LibVLC player,** with external opening as a fallback. The usual containers and codecs,
  HDR10 with SDR tone mapping, internal and external tracks and subtitles, speed, skips, and boosted
  volume with a limiter, fullscreen, and a mini player.
- **Continuity.** Exact progress saved every five seconds and on pause, seek, and close; resume
  within ±5 s; watch statuses with a configurable threshold; progress transferred between compatible
  versions; a cancellable countdown to the next episode; manual intro and credits markers.
- **Personal experience.** A hybrid home with resume and library, favourites, watch later, a rating,
  and local recommendations that explain themselves and can be turned off.
- **Accessibility.** Full keyboard, visible focus, screen readers, scaling, high contrast, reduced
  motion, and customisable subtitles.
- **Data and privacy.** Local SQLite with WAL and versioned migrations, rotating backups with a
  manifest, and video-free ZIP export/import. Zero telemetry without consent; opt-in, sanitised
  diagnostics.
- **Windows integration.** Configurable tray and startup, off by default, media keys, and
  "Open with…" that plays without importing into the catalogue.
- **Distribution.** An x64 MSIX and an independent ZIP, with published SHA-256 sums, an SBOM in
  CycloneDX and SPDX, licence and third-party notices inside the artifact, and a reproducible build.
- **The application can be told where it lives.** `AP_LOCALMEDIA_DATA_ROOT` names the data folder; it
  is read once at startup and a blank value is the same as not setting it.

### Fixed

- Consenting to the first scan scanned nothing, so a new install stayed empty forever.
- Adding a folder twice closed the process instead of refusing it with a sentence.
- A scanned, unidentified file opened the series card, which offers nothing to play.
- Choosing an audio track neither applied nor stored it.
- The session never fed the progress tracker, so the resume offer never came back.
- Withdrawing diagnostics consent left the exported report on disk.
- The video status indicator was never fed: it stayed blank while the engine decoded on the GPU.
- An older release opened and wrote over a database a later one had already migrated.
- **Installed as an MSIX, the data was not going where the documentation promises**: Windows
  redirected the writes into the package container, and **uninstalling deleted the whole library**,
  backups included. The package now turns that redirection off, so the MSIX and the ZIP share one
  data folder and uninstalling removes the application alone.

### Security

- The artifact **carries no access token**. Remote identification requires placing one by hand in
  `AP_LOCALMEDIA_TMDB_TOKEN`, and without it no connection is opened.
- The package declares one capability, `runFullTrust`, and none for network, location, or system
  libraries.
- The payload is examined before publication for keys, tokens, and local paths.

### Known limitations

- **No code signing.** Windows will show a SmartScreen warning, and the documentation does not claim
  otherwise. Check the published hash; the build is reproducible.
- **An unsigned MSIX will not install.** Windows requires a signature it trusts, so this release's
  MSIX is for inspection and archival; use the ZIP, which needs no installer.
- **One class of video adapter.** The matrix ran in full on a discrete adapter; Intel Quick Sync's
  decode path has never been exercised.
- **Multichannel sound unchecked:** 5.1 and 7.1 selection has not been exercised because no available
  audio device declared more than two channels.
- **No ARM64,** no Store, and no updater: those arrive with the first stable release.
- **Automatic version grouping is not wired.** The version comparison exists and is tested, but
  nothing creates groups today, so in the artifact it appears only if a group arrived some other way.

[0.1.0]: https://github.com/apvisualsolutions/ap-reelume/releases/tag/v0.1.0
