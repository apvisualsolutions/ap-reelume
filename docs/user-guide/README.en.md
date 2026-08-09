# User guide

How to do each thing in AP Reelume. The Spanish version is at [README.es.md](README.es.md).

## The first time

1. Run `ApSolutions.LocalMedia.Windows.exe`. There is no installation and no sign-up.
2. Go to **Library**. **Add your folders** is at the top.
3. Type the folder path, choose whether it is **Local**, **USB**, or **UNC or NAS**, and press
   **Add folder**.
4. The application asks before the first scan. Press **Allow first scan**.
5. When it finishes, press **Apply** to see the catalogue.

Adding a folder **copies and moves nothing**. If the folder is already in the library, or overlaps
one that is, the application says so and adds nothing.

## The library

- **Search** by title, cast, or genre. The search is local.
- **Filter** by watch status and **sort**; press **Apply** for it to take effect.
- A video whose drive is disconnected shows as **unavailable**. It is not removed from the catalogue,
  and it comes back on its own when the drive returns, without duplicating.

### Scanning

In **Settings** you choose when it scans: at startup, manually, or incrementally. Continuous watching
applies local changes within seconds; for USB and NAS there is a fallback scan that recovers what
watching did not see.

## Identification

The application infers movie, show, season, and episode from the name and the folder. Patterns like
`S01E02`, `1x02`, and `Cap.803` are covered.

- A match at **90% or above** applies itself.
- Between **60% and 89%** it is suggested and waits.
- **Below 60%** it stays pending.

Anything that does not resolve itself goes to **Review**, where you decide. A correction of yours is
not overwritten afterwards.

### Online metadata

The TMDB request happens only when an access token exists in the `AP_LOCALMEDIA_TMDB_TOKEN`
environment variable. **The download carries none.** Without a token, identification works from
whatever is already in the local cache and opens no metadata connection. The application's only
possible connections — metadata and the update check, both under your control — are enumerated in
the privacy statement.

## A title's card

From the card you can play, mark watched or unwatched, favourite it, save it for later, and rate it
from 1 to 10. Also:

- **Edit metadata.** What you edit is locked: a later remote refresh does not overwrite it.
- **Preview rename.** It shows what it would do before doing it. On a conflict it does not run. It
  never moves folders, and offers undo when that is viable.
- **Review versions.** When one piece of content has several files, they are treated as versions.
  None is deleted and none is hidden.

## Playing

Press **Play from the start**, or **Continue** if you already began it.

| Action | Where |
|---|---|
| Pause, stop, back, forward | Transport controls |
| Audio and subtitle track | Session panel |
| Audio output | Session panel |
| Intro and credits markers | Session panel |
| Fullscreen and mini player | Player buttons |

- **Speed, skips, and volume** are configurable. Above 100% the volume passes through a limiter, so a
  boost does not produce peaks.
- **Subtitles**, internal and external: SRT, ASS, and VTT. The selection is reapplied to the next
  episode.
- **The video status** on screen says what the engine is doing: dynamic range, HDR10 passthrough, SDR
  tone mapping, hardware acceleration, or a fall back to software.

There is one playback session, and only one. Moving between window, fullscreen, and mini player keeps
the position and the preferences.

## Continuity

Progress is saved every five seconds and additionally on pause, seek, and close. When you come back,
the application offers to continue where you stopped, within ±5 s even if the shutdown was
unexpected.

A moved or renamed file keeps its identity and its progress. If you switch version, the progress
travels with the content: exact when the durations match, proportional when the difference is safe,
and with a confirmation when it is not.

At the end of an episode there is a countdown to the next one. It is cancellable from keyboard,
mouse, or media key, and if the next file is not there it returns to the card rather than failing.

## Keyboard and accessibility

Every essential action works without a mouse. Shortcuts are configurable in **Settings**, and the
keyboard's media keys are registered only while a session exists.

The application respects the system theme, high contrast, scaling, and Windows' reduced-motion
preference. Subtitles have their own controls for size, font, colour, background, and outline.

## Your data

- **Backups.** In **Backups** you can create one and restore it. Backups rotate and carry a manifest
  with hashes.
- **Export and import.** A ZIP with your catalogue, your marks, and your personal artwork. It
  **carries no videos**, and the downloaded image cache is excluded because it regenerates itself.
- **Restoring to another path.** If the library moved, the restore remaps the paths without
  duplicating anything.

Everything lives in `%LOCALAPPDATA%\APSolutions\LocalMedia` unless you name another folder with
`AP_LOCALMEDIA_DATA_ROOT`.

**Uninstalling does not delete your data.** Windows removes the application but leaves that folder
untouched: your catalog, your progress, and your backups stay where they were, and a reinstall
finds them again. To really erase everything, delete that folder by hand after uninstalling. Your
videos are never inside it: the application neither copies nor moves them.

## Window, tray, and startup

Closing closes. If you would rather it stayed in the tray, or started with Windows, turn that on in
**Settings**: both are off by default and both are reversible. On closing, the application writes
progress before it goes.

**If you uninstall with "start with Windows" turned on**, the entry that made it possible is left
orphaned in the registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`). It is harmless: it
points at a program that no longer exists and Windows simply ignores it. Reinstalling repairs it on
its own, because the application rewrites its entry at startup. To remove it by hand, open Task
Manager → **Startup apps** and disable it, or delete it from that key with the registry editor.

## Opening a loose file

You can open a video with AP Reelume from Explorer without adding it to the library. It plays and
**creates nothing** in the catalogue. If you then want it catalogued, the application offers to add
its folder, and only then does it add it.
