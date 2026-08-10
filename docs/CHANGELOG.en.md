# Changelog

Every notable change to AP Reelume. The Spanish version is at [CHANGELOG.es.md](CHANGELOG.es.md).

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the versioning is
[SemVer](https://semver.org/). The canonical scope record, with status and evidence, is
[FEATURES.md](FEATURES.md).

## [Unreleased] / [Sin publicar]

### Added

- **An independent updater.** It checks for a newer version only when you ask or have allowed it,
  tells you what changed in Spanish and English, downloads into a folder of its own while verifying
  the published hash and size, and hands the package to Windows only after a confirmation that names
  that version. A download that is cut off resumes from where it stopped; one that does not match is
  deleted. Your library takes no part in the download: the database and the running binary were
  measured to be untouched after a correct, cancelled, tampered, and interrupted update. The Store
  keeps using its own channel.
- **A Windows Package Manager entry.** Every build leaves the winget manifest generated from the
  archive itself: the hash is the published one, the executable it declares has been found inside the
  ZIP, and the descriptions come from the two READMEs. winget costs nothing and requires no
  certificate, so it is the first way to install this that will be available.
- **A native ARM64 package.** `win-arm64` MSIX and ZIP, built and verified: every binary in the
  payload carries ARM64 in its header, LibVLC travels where the loader looks for it, and the
  application is identical to the x64 one file for file. It is built on every verification and every
  release.
- **Automatic intro, recap, and credits detection.** It compares each series' episodes with each
  other locally and finds the audio that recurs; nothing leaves the machine and not a single
  connection is opened. Detections are stored per episode with their confidence, a manual marker of
  the same kind always suppresses them, and accepting or correcting one protects it from every
  later run. The work yields to playback, and the option stays off until you turn it on. Evaluated
  against a held-out corpus of synthetic series: every approved threshold passed on the first
  measurement, with zero spurious detections on the segment-free episodes.

- **A permanent guard against the house defect.** The audit found components registered that the
  application never invokes, and every case was hunted by hand; an architecture test now requires
  every registered service to be resolved at least once outside its own registration. Its first run
  enumerated 32 orphans — not the estimated ~12 — and uncovered new faces: the audio device
  selection never reaches the engine, stored playback preferences are never applied, the watched
  toggle is wired to nothing, there is no way to remove a library folder, and choosing a duplicate
  version does nothing. Each debt lives inside the test under its own identifier, and a second
  assertion evicts the entry the moment its wiring lands: the list can only shrink.

- **The application checks its own wiring as it is built.** A component asking for something nobody
  registered used to be a failure that waited for the first screen that needed it — possibly yours,
  in a corner no test opened. That check now happens as the application is assembled, so the failure
  shows up at any test's startup instead of in front of somebody. It covers 109 of the 156
  registrations: the 45 built through a function of their own stay opaque to the check, and saying so
  beats letting anyone believe they are covered. It costs 0.22 milliseconds per startup. On its first
  run it found not one broken wire.

- **Language preference.** Settings → Appearance lets you choose Spanish or English. The interface,
  the update summaries, and the metadata speak the same language — the interface used to be pinned
  to Spanish while the updater's summary and the metadata followed the machine's language, and
  could arrive in another. Metadata uses the new language after a restart.
- **The window returns to where it was.** Position, size, and state (maximized or not) survive the
  close, whatever path closes it. A position stored on a monitor that is no longer connected is
  discarded instead of opening the window off every screen, and a window closed maximized reopens
  maximized over its restore bounds.

### Changed

- **On the way out, the application lets go of what it took.** The native player, the database, the
  tray icon, the media-key registrations and the network clients lived in a static field that nothing
  ever released: the process ended and left Windows to reclaim its own, which is trusting rather than
  closing. The application is now an object with an owner and it is released on exit, whether the exit
  comes from the window or from the tray. There is a second effect, more visible in the tests than on
  screen: two applications can exist at once in one process without seeing each other, and the clause
  forcing the two full-journey suites to run one after another came off — which is how the ownership
  is shown to be real rather than merely tidier. What is still not released, on purpose and written
  down, is the native LibVLC instance: creating and destroying it repeatedly is a known failure mode,
  so it lives as long as the process does.
- **The application's registration stops being a three-hundred-line list.** Everything the
  application assembles was declared in one chain, and finding out what a piece depended on meant
  reading all of it. There are now eight modules by area — data, playback, personalisation, library,
  settings and backups, updates, appearance, and identification — each short enough that a missing
  piece is visible. Behaviour is unchanged, and the tests that walk the genuinely assembled
  application are what say so.
- **The logic that picks which copy to offer when the library will not open can now be measured.** It
  lived inside the composition file, where the only way to reach it was to make a real database fail,
  and it decides what you are offered on the worst day your library has. It is now a piece of its own
  with five tests, two of them covering what nobody checked before: that a copy the record names but
  which is not on disk is not offered, and that another database's copy is not mistaken for yours.

### Legal

- **Credits shows TMDB's logo, not only their name.** Their terms ask that TMDB's use be identified
  with their mark, less prominent than the product's own; it was the last point of those terms left
  open. The file is the one TMDB publishes, and that it is can be checked: the SHA-256 they embed in
  the asset's address matches the versioned file's, and a test compares them. What is drawn is their
  vector rather than an imitation — another test contrasts the view's geometry against the file's,
  character for character — and it is drawn at 16 px against the 24 px of the product name. The
  specification said 48 px for that name; it was measured, it did not exist, and the figure was
  corrected instead of inherited. It carries alternative text in both languages and is not a link.
- **The text of every third-party licence travels inside the package.** Naming a component and its
  licence is not delivering the licence, and several of them require it: LGPL-2.1, GPL-2.0 and
  Apache-2.0 ask for a copy to accompany the binary, and MIT and BSD-3-Clause for their copyright
  notice to be reproduced. VideoLAN's package carries none, so nobody was supplying them. The
  `licenses/` folder of both artifacts now carries the five full texts and the notices of ANGLE, Skia,
  HarfBuzz, BouncyCastle, SQLitePCLRaw, SQLite and VideoLAN — including Microsoft's own file covering
  the twenty-odd libraries Skia and HarfBuzz carry inside them, from freetype to zlib, which appeared
  nowhere until now. Notices a package publishes are copied from it and a test compares them byte for
  byte against the package the build consumed, so a version bump that changes a notice turns red
  instead of quietly distributing the previous one. The canonical texts were taken from a source that
  already distributed them and contrasted with a second, independent copy; GPL-2.0 came from VLC's own
  tree, which is the licence binding its plugins.
- **Every source file states the licence it is under.** The licence lived only in `LICENSE`, and a
  licence that lives only there stops being attached to a file the moment somebody copies it out of
  the tree. All 556 code files, 51 interface files, and 17 build scripts now carry their
  `SPDX-License-Identifier: GPL-3.0-or-later` header next to the copyright holder, and the formatting
  gate that already ran rejects a new file that arrives without one.
- **The third-party notices name what the package actually carries.** They listed eight components —
  the ones somebody remembered asking for — while the artifact carried thirty, among them ANGLE under
  BSD-3-Clause, Skia, HarfBuzz, BouncyCastle, and the .NET runtime itself, all with notices that must
  travel with the binary. The list now comes from the build's real inventory, explains the
  self-contained runtime and VideoLAN's plugins separately, and a test stops a dependency from
  entering the artifact without appearing in both languages' notices.
- **The TMDB attribution says the sentence TMDB requires.** It displayed a summary of the mandated
  sentence; it now states the required one — "uses TMDB and the TMDB APIs but is not endorsed,
  certified, or otherwise approved" — in Credits, in the notice, and in both READMEs, with a test
  pinning it word for word in both languages.
- **Nothing from TMDB is kept longer than six months.** Their terms forbid it and the cache's expiry
  did not guarantee it: when the network failed or you removed the token, the program kept serving the
  stored copy however old it was. There is now a hard floor of 180 days; past it the entry is not
  served and is deleted from disk.
- **VideoLAN's plugins are confirmed compatible.** This was recorded as a question for an opinion: a
  `GPL-2.0-only` plugin would clash with this program's licence. It was checked at the source and they
  carry the "or any later version" clause, so they fit. The same pass turned up the opposite of good
  news: VideoLAN's package carries no licence file at all, and the artifact did not carry the text of
  the other licences either, which several of them require to accompany it. That is the gap the first
  entry in this section closes.
- **A legal status that also states what is missing.** A new page in both languages gathers the
  licence, the warranty disclaimer, third parties, TMDB's and GitHub's terms, and the export note for
  the cryptography the package carries, and names without decoration the five points that remain the
  owner's, the professional legal opinion among them.

### Security

- **Playing outside the application opens video and nothing else.** The button that hands a file to
  whatever player Windows has registered trusted its callers to have filtered by the container list
  already. What happened otherwise was measured: of five file types the library does not catalogue,
  **three opened their handler**, among them a `.ps1` and a file with no extension. The check now sits
  where the call is made, against the same list that decides what enters your library.
- **Restoring a backup applies its own size limits.** The ceilings — 512 MB per file, 2 GB in total —
  were set by the inspection step, which is a step somebody has to remember; unpacking now applies
  them too. Along the way it was checked, by forging an archive that declares one byte where it holds
  a whole database, that no entry can hand back more than it declares: the risk was in the
  declaration, not in the copy, and that is where it is now cut off.
- **Continuous integration can no longer reach a personal machine.** While the repository was
  private, verification could be routed to a self-hosted runner through a repository variable; now
  that it is public, that hatch was a standing invitation for a fork pull request's CI to run on the
  owner's machine the moment the variable reappeared. The workflow no longer consults it and always
  runs on hosted runners, which are free on public repositories.
- **The pipeline's three actions move up to their current major.** `checkout` 7.0.1, `setup-dotnet`
  6.0.0, and `upload-artifact` 7.0.1, with every SHA checked against the tag it claims to be.
  `checkout`'s breaking change hardens exactly the triggers this project does not use, and
  `upload-artifact`'s only alters naming when archiving is turned off, which here it is not.
- **The one external tool that seals a release is pinned by version.** Every action was already
  SHA-pinned and NuGet was locked, but ffmpeg was installed from the community feed at whatever the
  latest version happened to be: it was the only unpinned third-party executable on the machine that
  packages and signs. It now installs a specific version that moves only by a deliberate edit, just
  like the action SHAs.
- **The published digests are signed, and the updater demands the signature.** The hash an update
  is checked against travelled in the same unsigned answer as the package it vouches for: whoever
  altered the answer could alter both at once. Every release now signs its digests with a minisign
  key whose public half travels inside the binary; the updater verifies that signature before
  believing any hash, reads the hash only from the signed block, and a version without the
  signature — or with altered lines — is refused with its reason. The private key lives outside the
  repository, the release pipeline signs and refuses to publish unsigned, and the privacy statement
  explains what this layer proves and what it still does not (Windows code signing is a separate
  layer, still pending its economic decision).
- **The updater only accepts bytes from the declared domains, on every hop.** Redirects already
  required HTTPS but accepted any destination; every hop now has to stay inside the list the
  privacy statement publishes (GitHub and its storage), a hop outside is refused with its reason on
  screen, and the statement finally names the storage domain GitHub redirects to — with a test that
  keeps code and promise from diverging again. Title artwork is equally confined to its declared
  domain.
- **Every network answer has a ceiling.** Release metadata is cut off at one megabyte, the package
  is cut off the moment more bytes arrive than the release declared (and the poisoned partial is
  deleted), and a poster over ten megabytes is refused mid-stream while the previous artwork
  survives.

### Fixed

- **A verification that wedged no longer spends an hour saying nothing.** Six of the ten continuous
  integration runs of 10 August died at the sixty-minute ceiling, and the log said nothing between
  "build succeeded" and the cancellation fifty-six minutes later. The step after the build produces
  the container matrix with FFmpeg and started it with no bound: one wedged encode burned the whole
  job and reported as an infrastructure hiccup. Every call to the encoder now has a ceiling, every
  sample is announced before it is produced, and a process that does not come back is killed with the
  recipe it was on named. Producing all sixteen samples costs 1.6 seconds, so that ceiling is not a
  performance budget: it is the difference between a named failure and a job that dies without saying
  why.
- **And with the name in front of us, the recipe that wedged.** The first run with the ceiling in
  place failed in four minutes instead of dying at sixty, and pointed at
  `mkv-dual-audio-english-first`. The eleven recipes that ended in `-shortest` now bound their output
  duration explicitly: `-shortest` has a documented deadlock where interleaving and flushing cross,
  which is exactly what made some runs take twenty-two minutes and others hang forever. Every input
  already lasted three seconds, so the file is the same one: the 114 container-matrix tests confirm it
  property by property.
- **Stored playback preferences are actually applied.** The audio track and subtitles you chose —
  per file, per series, or globally, falling back by language when a track is absent — were
  resolved and never applied: every session opened with whatever the engine picked. They now apply
  the moment the video opens, and the track selector shows what was actually applied. Along the
  way, six dead container registrations (duplicates of what the application builds another way)
  are removed instead of left silent.
- **Choosing the audio output device now changes where the sound goes.** The selector existed and
  the engine never heard about it: your choice was stored while the audio kept coming out wherever
  VLC pleased. Picking a device now pauses, reroutes, and resumes — never restarting the video —,
  the stored global choice applies when each session opens, and a device that vanishes mid-play
  never cuts playback: it falls back to the default without forgetting your preference.
- **Switch versions without leaving playback.** If the title you are watching has more than one
  version (another resolution, another codec, HDR), the player lists them and you can jump to one
  mid-session. Your position is written first; when the lengths make a safe transfer impossible,
  the application asks with the proposed second in sight — continue there, start again, or cancel.
  An unavailable version is never offered as openable.
- **Moving a video to another folder no longer costs it its history.** Every scan captures a
  lightweight identity of what it catalogs (the disk's stable id and a bounded content
  fingerprint) and reconciles: a file that appeared under a new path and vanished from the old one
  becomes the same entry again, progress and your decisions intact. A copy that coexists with the
  original keeps being treated as a copy (versions, as always). And when there is doubt — two
  known copies and an identical newcomer — the review inbox asks you: "same file, reassign" keeps
  the history under the new path; "it is a new file" leaves it as its own entry. The offer returns
  with every scan until you decide; nothing is decided silently.
- **A folder can be removed from the library.** The library finally lists its folders, and each
  one has a removal action with a confirmation that tells the truth: the folder leaves the
  catalog, no video on disk is touched, and adding it again catalogs it anew. The catalog on
  screen reloads right away.
- **Marking something watched is now actually stored.** The card's watched toggle was built with
  no handler: every mark went nowhere and the card forgot it on reload. A decision of yours is now
  stored as manual — nothing the player computes afterwards changes it — and undoing it hands the
  state back to the automatic rules. The watched threshold (how much you need to reach) is finally
  configurable under recommendation settings, between 50 and 100 %; moving it recomputes only the
  automatic states and says how many moved.
- **A video that fails to open no longer drags the preferences down with it.** Applying preferences
  assumed a live session: with a file the engine could not open, the track selection blew up
  internally after the screen had already shown the diagnosis. A session that never opened, or
  closed underneath, now simply has nothing to apply; any other failure still speaks up.
- **Renaming with the file open in another program now says what to do.** The failure was stored in
  the audit as "IOException" and the screen said nothing: the rename simply did not happen. The
  surface now says whether another program has the file open, whether Windows denied permission, or
  whether the drive failed — each with its action — and the audit keeps the reason under a useful
  name.
- **Diagnostics say what your machine did, not what a constant promised.** The reported video
  acceleration is what the engine actually used for the last video, the library size is the real
  one (bucketed, as ever), and the errors are the ones the application recorded — no paths, no file
  names, as the allowlist demands.
- **The manual explains the orphaned startup entry.** Uninstalling with "start with Windows" on
  leaves a harmless registry entry; the manual says why it does nothing, how to remove it by hand,
  and that reinstalling repairs it on its own.
- **The screen follows the engine for the whole session.** The on-screen state only changed at
  open: pausing paused the engine while the interface kept saying “playing” forever, with the
  resume controls unreachable. The method that applied the transitions existed and was tested; in
  the assembled application nobody called it. It was found by the physical walk of the packaged
  artifact — three re-runnable scenes with a real disk, real SQLite, and real decoding: the watcher
  cataloguing a dropped file and grouping two copies, the keys operating a playing video, and two
  episodes chaining on their own — which stays on as a permanent guard.
- **Long paths and per-monitor scaling, declared instead of inherited.** The application shipped no
  manifest of its own: the 260-character limit still applied even where Windows had lifted it — a
  library under a deep folder silently lost files — and DPI awareness was whatever the runtime
  guessed. Both are now written into the process manifest.
- **A rewritten migration no longer goes unnoticed, and integrity is asked once.** Startup only
  compared version numbers: if an applied migration's text changed, the schema on disk and the one
  the code assumes diverged silently. Every stored checksum is now compared against the build's,
  and a mismatch is refused by name. Along the way, the integrity check — the slowest part of
  opening a large library — runs once per start, not twice.
- **A detection can no longer outrun its episode.** Manual markers always validated against the
  duration; detected ones were judged blind. The detector now clamps what it emits to the episode
  it measured, and the policy applies the manual-marker rule to detections.
- **The manual says what happens to your data when you uninstall.** Nothing is deleted: catalog,
  progress, and backups stay in their folder and a reinstall finds them; the manual also explains
  how to really erase everything.
- **The player window coordinator had two owners.** It was registered in the service container and
  simultaneously built by hand by the main view: two instances, one holding geometry nobody would
  ever read. The view — the one that owns the mini player window — stays as the single owner and
  the dead registration is removed.
- **A marker made during playback did not work until the episode was reopened.** The session's
  markers were a photo taken at open: saving, deleting, accepting, or correcting a marker changed
  the stores while the skip button kept reading the old photo. Every change now recomposes the
  session's markers on the spot, so the button appears (or disappears) without closing anything.
- **Two copies of the same film never grouped on their own.** Version grouping existed with its
  repository, its conservative policy, and its tests, and nothing invoked it: groups were only ever
  created by tests. Every scan now groups the copies whose names declare them the same, a material
  duration difference waits for confirmation instead of grouping silently, the preference you pin
  survives rescans, the group is found from any of the copies, and no file is ever deleted or
  hidden.
- **Neither keyboard shortcuts nor media keys did anything.** Every piece existed — the shortcut
  map with its defaults, the conflict-refusing editor, the router that prevents duplicate actions,
  the media key service — and none of them touched another: the player never read the keyboard and
  the service never started. The player now answers the shared map (Space pauses, arrows skip, M
  mutes, F goes fullscreen…), the hardware media keys operate the session while one exists and are
  released when it closes, a key arriving through two paths acts exactly once, and the Settings
  editor edits the very map the keys read.
- **Finishing an episode never offered the next one.** The engine did not even learn the video had
  ended — the state stayed at "playing" forever —, the countdown that was tested end to end was
  never registered, and the card's buttons did nothing. The end of the media is now a real state,
  finishing an episode offers the next with its cancelable countdown, "Play now" opens without the
  wait, and when there is no next episode or its file is gone, the application returns to the
  details.
- **Folder watching never started.** The watch coordinator, the debounced watcher, and the fallback
  scheduler existed, were tested, and nothing started them: the application only scanned when a
  button was pressed. Watching now starts with the window and stops on the way out, a freshly added
  folder is followed from its first scan without a relaunch, a root configured as manual is not
  watched behind its owner's back, and the fallback scan for USB and NAS roots actually recovers
  lost events every fifteen minutes. And every scan — watcher-triggered or manual — hands what it
  found to identification, not only the manual one.
- **Identification never ran, so the review inbox was always empty.** The use case existed complete
  — it parses the name, scores candidates, asks the provider only when needed, and stores the
  result — and nothing ever invoked it. Every scan now hands what it found to identification: the
  confident match resolves on its own, the ambiguity shows up in the inbox, a file somebody already
  decided is left alone on every later scan, and files no earlier scan ever identified heal on the
  next one. Without the provider token everything stays local and no connection is opened.
- **"Continue where you left off" left the video at zero.** The resume decision was computed after
  the media had opened, nobody passed the start position to the engine (which has always accepted
  one), and the prompt's buttons were wired to nothing. The decision now exists before the open, the
  media opens at the stored position, and "Restart" genuinely seeks to zero. Three assembly
  assertions, one unit test, and one real-decoding test cover the whole chain.
- **Background detection could not be stopped and survived exit.** The use case has accepted
  cancellation from the start and the scheduler called it bare; closing the application could leave
  a process decoding in the background. Every detection now runs under a shutdown token and leaving
  the application stops it, together with the session's save loop.
- **An unreadable answer from the update source could take the application down at startup.** A
  captive portal (hotel wifi) answers `200` with a login page; that threw an untranslated exception
  which, on the startup automatic check, escaped onto the interface thread. A body that is not a
  release now reads as "source unreachable", the update surface always lands on a state, and the
  three startup jobs (check, tray exit, loose file) observe their exceptions instead of handing
  them to the interface thread.
- **The periodic position save never started.** The five-second loop was only ever invoked by
  tests: in the application only the orderly close and the version switch wrote, so a power cut
  lost the whole session. The session now starts the loop on open and cancels it on close, pausing
  writes the position, and every seek — from the transport, the skips, or the skip button — writes
  the chosen target. Along the way, position handlers detach from the engine when each session
  ends, instead of stacking one per episode.
- **Segment detection released LibVLC in the order that crashes the process.** The fingerprint
  extractor stopped a still-playing player, released it before its media, and disposed the media
  with no quiescence window — the three rules the code itself has written down as the native
  failure mode, on the same instance playback uses. It now keeps the engine's own order, with a
  deferred-release queue on the factory whose drain survives a failing dispose. A twenty-cycle
  drill over ten episodes stays behind as a permanent check.
- **Packaging failed on machines with exactly one Windows SDK.** The `makeappx.exe` lookup returned
  a bare string instead of a list when a single version was installed, and indexing it produced its
  first character: sealing then tried to run a program called `C`. The lookup was copied into three
  scripts (package x64, package ARM64, and verify the package) and all three copies carried the same
  defect. This is what broke continuous verification on the updated runner; it was never seen
  locally because two SDKs are installed. The same image update removed ffmpeg from the runner, so
  the workflow now installs it explicitly: the codec matrix, the segment corpus, and the
  file-association phase are measured again on every push.
- **The suite's first full pass in CI uncovered seven machine assumptions.** Two backup progress
  tests were flaky by construction (`Progress<T>` queues its callbacks and a loaded machine reaches
  the assertion before the last stages; they now report synchronously). The other five depended on
  the machine: with no audio endpoint the catalog declares a block instead of failing, an HDR sample
  generated without colour metadata declares its precondition broken, and the ±5 s promise and the
  frame budget declare themselves out of a shared runner's reach — their gates keep being measured
  on the local physical harness, as always.
- **The privacy statement did not mention the updater.** The connection table still described the
  application from before T44: it listed the two metadata destinations and denied that any update
  check existed, when the updater — optional and off out of the box — talks to `api.github.com` and
  `github.com`. The table now lists all four destinations, the other documents scope "no connection"
  to metadata, and a new test fails if the network purpose registry and the table diverge again in
  either language.
- **Ten matrix statuses said more than was demonstrated.** The 2026-08-08 audit found a family of one
  defect: components built, registered, and tested that no path in the assembled application invokes
  — identification, folder watching, duplicate grouping, the periodic save loop, resume, the
  next-episode countdown, and the shortcut and media keys — plus an MSIX cycle verified against a
  re-signed copy that the unsigned artifact cannot repeat. Those rows (PRD-002, LIB-002/003/006/007/008,
  PLY-008/011/014, and REL-003) return to `IMPLEMENTED`, each with its blocker, owner, and unblock
  condition in the verification manifest. The per-component evidence still stands; what was missing
  was the assembly, and the record now says so.
- **The automatic-check box did nothing.** It was in Settings and was written to disk, but no path in
  the application ever asked for an automatic check: it only ever checked when the button was
  pressed. T44's physical verification found it, not the tests.
- **The updater asked a repository that does not exist.** It asked for `ap-solutions/ap-reelume`
  rather than `apvisualsolutions/ap-reelume`; GitHub would have answered 404 and the application
  would have said "you already have the most recent version" indefinitely. A test now compares that
  address against the one the changelogs publish.
- **A summary that began with a subheading arrived empty,** because the section reader broke on `###`
  as well as `##`. A release like that would have been offered to nobody.
- **The notice that Windows did not open the package said "you can try again".** On a machine with no
  App Installer, trying again never works. It now says the file is downloaded and verified, and how
  to install it by hand.
- **The playback resource gate failed about one run in three with no regression at all.** It compared
  the working set of the whole process — test host and coverage collector included — against a 32 MiB
  bound, and seven runs with no code change ranged from −7.9 to +37.6 MiB. Fitting a slope does not
  help either: measured, it ranges from −170 to +1107 KiB per cycle. The bound is now 128 MiB, which
  is what a gross regression looks like; what catches a leak are the exact counters already asserted
  on all fifty cycles.
- **The test media matrix could not be generated from nothing.** Several samples mux a subtitle track
  from a companion file, and their recipe named it with a placeholder the generator never wrote or
  substituted. Two separate things hid it: on a machine that already had the output tree the samples
  were reused rather than produced, and without ffmpeg the script returns before it could try.
  Moving the project to another folder is what found it.
- **An earlier redaction of the personal library was incomplete.** A show's Spanish title had been
  replaced and the same title in English was left, because the pattern being searched for was written
  in Spanish. The check no longer depends on anybody remembering: `RepositoryPrivacyTests` walks the
  versioned files on every run and derives what it looks for from the machine, writing no personal
  data into the code.
- **A recovery test failed about one run in two,** because it waited for the signal file to *exist*
  and then read it: existing and being finished are different moments, and reading between them
  collides with the process still holding it open. The wait is now the read.
- **The skip-marker button never received any data.** It had been built since the MVP and nothing
  in the assembly ever handed it the markers or the position, so skipping intros existed only in
  the tests. It now follows the playhead with the composed markers — manual and detected — the
  editor loads the series' real markers, and an episode resolves its real series instead of
  treating every file as a series of its own.
- **The default branch can no longer be left behind at publishing time.** The personal-library
  redaction lived only on the working branch, and `main`'s tree kept showing what had been redacted
  until the audit found it; `main` was fast-forwarded to the branch and `prepare-release.ps1` now
  blocks any publication with `main` left behind.
- **The ARM64 build had never worked.** The Windows project pinned `PlatformTarget` to `x64`
  unconditionally, so `-r win-arm64` failed with `NETSDK1032`. The early check that existed to catch
  this was a `dotnet restore`, which resolves packages without compiling anything: it stayed green
  while the build was impossible. CI now builds the ARM64 package, which contains that check and also
  answers it.

### Note

- **Four assembly guards stopped reading the code as text.** They asserted their promise by
  searching for characters in the composition file — which a comment or a dead registration can
  satisfy, and had already done three times. They now assert the registered descriptors and, for
  the updater, the address on the object the application actually builds, compared against the
  one both changelogs publish. The two halves no descriptor can express stay declared as text
  until the startup path is reformed.
- **New code can no longer arrive without its tests.** Every verification now requires each
  source file that is new against `main` to arrive with at least 96% of its lines and branches
  covered by the suites, with the per-file verdict written next to the results. The gate was
  calibrated against the previous session and found three real files below the bar — the happy
  paths were walked end to end; their error branches were not — which are named as visible debt
  in the evidence rather than lowering the bar to hide them.
- **The low-resolution quality enhancement is deferred, with its measurement.** A measurable
  spike over real media investigated whether VLC 3's video filters (sharpening, denoising,
  deblocking, scaling) can improve sub-720p shows: none of them processes a single frame on this
  application's video path — VLC itself builds the chain and removes it whole when it cannot
  match the formats, under every decoder and output format. The feature is not promised: its row
  is deferred with the measured evidence, and the real alternatives (VLC 4, an own enhancement
  over the already-decoded frames, or another engine) are documented with their cost, so the
  decision is made with knowledge rather than intuition.
- **The performance budgets no longer block on shared runners.** Two budgets failed in CI measuring
  the neighbour's noise, never locally. CI still runs them and archives their verdict with the test
  results, but cannot fail because of them; they keep blocking on the local physical harness, which
  is where they mean something. The WAL durability test also gains a bounded retry (3 attempts /
  1 s) only around the reopen after the kill: the runner disk's transient "disk I/O error" is not
  the phenomenon under test.
- **Neither the updater nor winget works while the repository is private.** Both read the GitHub
  releases address, which for a private repository answers nobody; since the absence of a release is
  a settled answer, the application would tell everybody they are up to date. Making the repository
  public and cutting a release are requirements for either to work, not decoration.
  `eng/build-winget-manifest.ps1 -Verify` checks it by asking the address.
- `PRD-003` is **blocked**, not verified: there is no Windows 11 ARM64 machine on which to certify
  playback, and emulating one would measure the emulation. The six physical phases are declared in
  `arm64-matrix.json` with their reason. Detail in [T42-arm64.md](evidence/stable/T42-arm64.md).
- Intel Quick Sync decoding and the OpenGL video outputs do not exist on ARM64: VideoLAN does not
  build them for that architecture. All fifteen native differences between the two packages are
  listed in the package report.

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

While assembling and packaging, walking the real application found defects no headless test could
see:

- Consenting to the first scan scanned nothing, so a new install stayed empty forever.
- Adding a folder twice closed the process instead of refusing it with a sentence.
- A scanned, unidentified file opened the series card, which offers nothing to play.
- Choosing an audio track neither applied nor stored it.
- The session never fed the progress tracker, so the resume offer never came back.
- Withdrawing diagnostics consent left the exported report on disk.
- The video status indicator was never fed: it stayed blank while the engine decoded on the GPU.
- An older release opened and wrote over a database a later one had already migrated.
- **Installed as an MSIX, the data was not going where this documentation promises**: Windows
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
- **One class of video adapter.** The matrix ran in full on the available discrete adapter; this
  machine has no integrated graphics, so Intel Quick Sync's decode path has never been exercised.
- **`PLY-004` blocked:** 5.1 and 7.1 selection has not been exercised because no audio endpoint
  declares more than two channels.
- **No ARM64,** no Store, and no updater: those arrive with the first stable release.
- **Automatic version grouping is not wired.** The version comparison exists and is tested, but
  nothing creates groups today, so in the artifact it appears only if a group arrived some other way.

[0.1.0]: https://github.com/apvisualsolutions/ap-reelume/releases/tag/v0.1.0
