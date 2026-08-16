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

- **Refresh the oldest entries on their own, if you turn it on.** When the application opens, entries
  whose details are more than 90 days old can be asked about again: at most 20 at a time, oldest
  first, and only for titles that are already identified. It ships off, the switch lives in Settings
  → Privacy, and it **is not even offered if you have not consented to the connection**, because
  without one there would be nothing to ask. It never happens while a scan is running or a video is
  open, and that is checked before each entry rather than only at the start. With the switch off no
  connection is opened at all: measured with the network canary, which in the same run counts zero
  with it off and two with it on.
- **A permanent guard against the house defect.** The audit found components registered that the
  application never invokes, and every case was hunted by hand; an architecture test now requires
  every registered service to be resolved at least once outside its own registration. Its first run
  enumerated 32 orphans — not the estimated ~12 — and uncovered new faces: the audio device
  selection never reaches the engine, stored playback preferences are never applied, the watched
  toggle is wired to nothing, there is no way to remove a library folder, and choosing a duplicate
  version does nothing. Each debt lives inside the test under its own identifier, and a second
  assertion evicts the entry the moment its wiring lands: the list can only shrink.

- **Opening a video can no longer leave the window sitting still waiting on the keyboard.** As each
  playback starts, the application claims the keyboard's media keys, and it stood still — the thread
  that draws the window included — until that registration answered, with no deadline at all. If it
  never answered there was no way out: the same trapped thread was holding the latch needed to cancel
  it. The wait now has a deadline and happens outside the latch, and if it runs out playback starts
  anyway: the keyboard's media keys are an extra, and a session without them beats a session that does
  not start.

- **No button can close the application by failing any more.** Every surface with buttons brought a
  hand-written command class of its own — twenty-four of them — and not one picked up a failure, so an
  error in the work behind a button ended the program. There is one class now, and it always picks the
  failure up: two places in the whole codebase are left where an await can end with nobody watching,
  and both of them catch. Unifying them surfaced two behaviours only one surface had: the rating on a
  card checked the value before storing it — a check that would have been lost in silence had a test
  not been there — and the player's skips refuse the next one while one is under way. Both stayed.

- **A failure can no longer take the application down with it.** Until now, when the work behind a
  button went wrong, the exception was returned to nobody: it was rethrown on the interface thread,
  where the only thing waiting for it was the end of the program. There is a net now — what reaches
  the top of the process is written down as a code instead of ending it, and a task that fails with
  nobody watching is caught before it becomes a shutdown. And the diagnostics report, which has
  existed for a while and could only ever talk about renames, finally covers the rest: until now, in a
  session where nobody renamed anything, an application that was failing looked like a healthy one.
  What is written down lives in memory and only for that session: nothing is written to your disk, and
  what travels from an exception is its type, never its message.

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

- **The tool that signs releases did not compile, and nothing could say so until a publication.** It
  was missing the licence header this project requires in every file, and that rule is a build error
  — but its project **was not in the solution**, so none of the checks that run on every change ever
  built it. The first place it would have surfaced was a real publication, at the step that verifies
  the signature is good. The project is inside now, so every check that already existed covers it,
  and a new test fails the moment another project appears outside.
- **Every published release now carries, beside it, the source code of the libraries that play your
  video.** LibVLC's licence and its plugins' require that code to be within your reach, and the
  clearest way to do that is the one they describe themselves: offer it from the same place you
  download the program from. So the release attaches `vlc-3.0.23.tar.xz` — checked against the digest
  VideoLAN publishes — and the LibVLCSharp archive, on top of the written offer that already existed,
  which stays for anyone who receives the program by another route. It also corrected a notice that
  named a VLC version which does not exist: the package's `3.0.23.1` is its own numbering, and the
  source is published as `3.0.23`.
- **And if you do not have the trailer, the card opens it in your browser.** When TMDB knows one, a
  button appears on the film's or the series' card and opens it in the browser you already use.
  **The application does not connect to YouTube**: it hands the address to Windows and your browser
  is what goes there, with your settings and your extensions — which is why the list of connections
  this application declares does not grow by a single host. What is stored is the video's key and
  never an address, and only a key with the exact shape YouTube uses ever composes a link: anything
  else offers no button. The value rides on the same metadata request that was already being made,
  so there is no extra call. It does not play inside the application, and that is not a technical
  limitation: doing so would break YouTube's terms.
- **If you keep the trailer next to the film, the card plays it.** The convention Plex, Jellyfin and
  Kodi already use works: a `<film>-trailer.<extension>` file beside it, or a `Trailers` folder inside
  the film's own. The button only appears when that file really exists, it opens the way a video you
  drag onto the application opens — without being added to your library — and it accepts only the
  containers this release plays. **Nothing is downloaded**: if your trailer is on YouTube, that is
  not a file of yours and it does not play in here.
- **A film's or a series' card shows its synopsis.** The overview was already downloaded, stored,
  merged while respecting whatever you had locked, and editable by hand — but it was readable
  nowhere: you could write it in the editor and never find it again. It appears on both cards now,
  wrapped and capped so it cannot push the versions or the seasons off the screen, and announced by
  name for anybody using a screen reader. A blank synopsis takes no room. No new connection is
  opened: the text was already on your disk.

- **Renaming files proposed no name at all, and now it is known why.** The rename preview opens,
  shows its confirmation box and its two buttons, and **never offers a single operation**: the
  application was asking to rename each file to the name it already had, so the safety check
  correctly discarded it as "no change". What is missing is the piece that decides what a file should
  be called from its entry, and that is a product decision rather than a loose wire: it is written
  down rather than invented. Nothing on your disk was touched, and none is.

- **The player's controls can be used with a mouse now.** Three separate things had them unusable,
  and all three looked the same: a button on screen that did nothing. The video status notice — the
  one saying whether it is running on hardware or at standard range — **covered the whole player
  surface**, opaque, over the video and over the control bar, so it swallowed every click; it is a
  badge in a corner now. The buttons never re-checked whether they could be used, so **you could
  pause with the mouse and then not resume**. And the volume slider moved without reaching playback:
  it changed a number on screen and nothing you could hear. All of it still worked from the keyboard,
  which is exactly why nobody had seen it.

- **Windows now describes the application in your language before you open it.** The package
  declared Spanish and English all along, but its description was **one sentence with a slash in the
  middle** — "Biblioteca y reproductor de vídeo local / Local video library and player" — which
  Windows showed the same way to everybody: declaring a language translates nothing by itself. The
  package now carries one text per language and Windows picks yours. The text is not written by hand
  anywhere: it comes from the first paragraph of the matching README, which is where the winget entry
  already took its own, so both installation routes say exactly the same thing. The name is
  deliberately not translated: "AP Reelume" is the product's name in both languages.

- **A gate counts how many of the application's buttons are really pressed with a mouse.** Until now
  the automatic walk drove the application from the keyboard and **two** of its 129 command controls
  ever received a click; the rest could be visible, enabled and incapable of doing anything with
  nothing to say so — which is exactly what happened to a pair of buttons that survived a whole
  audit. There is now an inventory of all 129, a list of what is not pressed yet **with the reason
  written beside it**, and a ratchet: that list may only shrink. The first batch covers the library
  and the card — filter, sort, apply, open an entry, go back, mark watched, favourite, watch later,
  rate and play — and a control only counts once it has been really pressed, once what changed has
  been checked, and once a click beside it has done nothing. What counts is recorded while running,
  not by reading the test code. Turning it on found **three** defects in the harness itself: it could
  not tell apart two identical Back buttons with only one of them on screen; the same button was
  pressed under one name and reported missing under another; and the checking click that was meant to
  do nothing **turned the favourite button back off** in the row above, with nothing to say so.

- **The coverage gate now watches code that is not new.** It only looked at files appearing for the
  first time, so an old one that got worse was watched by nobody — and that is not a hypothesis: on
  re-measuring the three files carrying debt, two were exactly where they were a day earlier and the
  third had **gone backwards** by fifteen points, because an earlier tidy-up removed code from it
  and took the tested parts with it. Nothing said a word. There is now an explicit list of watched
  files measured on every run, each with the bar its code meets today: if it drops, the verification
  fails; if it rises, it **also** fails until the new bar is recorded, so a debt that gets paid
  cannot quietly come back. Two of the three ended up fully covered along the way; the third stays
  watched, with its name and its number in view on every run.
- **The last coverage debt is paid.** The use case that reconciles what a scan finds — the one that
  keeps a video moved to another folder being the same entry — had its happy path tested and not its
  decisions: what it refuses to touch, what it counts as a failure without costing the rest of the
  scan, and what it stores. Those are tested one by one now, and the file goes from 86.73% of lines
  and 76.00% of branches to 100% of both, with the bar raised behind it. Measuring the list of gaps
  before writing anything cut it by a third — five of the entries noted by reading the code were
  already covered — and turned up one no reading would have found: no test ever read the counter of
  files attempted.
- **The window no longer waits for the database to be ready before it exists.** On startup the
  application brings the database up to date — and checks its integrity if that rewrote the file —
  and until now it did that work on the very thread that draws: nothing was on screen until it
  finished, and on a large library the check grows with the file. The window now appears
  immediately with a startup screen, the work happens elsewhere, and when it ends the library takes
  its place, or, if the database cannot be opened, the same recovery screen as always: what changes
  is when the decision is taken, not what is decided. There is no progress bar, on purpose —
  nothing at that moment knows how much is left, and a bar that moves without meaning anything is a
  picture of progress rather than progress. The measurement that settled it: writing "await" in the
  code is not enough to free the thread, so it was checked, and the thread was not free for a
  single millisecond.
- **The package verification now says how long the window took to appear, not just that it did.** It
  recorded a yes or a no, and that yes covered both an instant launch and one that arrived just
  before the ninety-second deadline ran out, so a degradation stayed invisible until it was a
  failure. It now reports the time — measured from before the process starts, because starting is
  part of the wait — for the three cycles that open the application, and a test demands the figure
  and bounds it against that deadline. Three numbers rather than one, because a single number cannot
  say whether it was the launch that got worse or the machine. The first measurement settled two
  things: all five cycles migrate a new database rather than only the first one, as had been
  written, and the first launch is not the slowest of the three either.
- **Cataloguing and playing share the native engine instead of starting one each.** Reading a file's
  technical data spun up its own LibVLC instance with the same options as the playback one, so a
  process that catalogued and played kept two native engines open — and the count that says "one per
  option set" could not see the second. There is one owner now, and a test that fails the moment
  another appears. The second media-release queue goes with it: the probe's did not guard its own
  disposal, so a single failing release would have left its worker dead for good and everything
  catalogued afterwards would have leaked with nothing saying a word. The one that remains already
  carries that guard.
- **The compiler watches that a stored number does not depend on the system's language.** A size, a
  date or a comparison written with the rules of the reader's language is read wrongly on another
  machine, and that error does not announce itself: it shows up once it is already stored. Three
  checks that shipped switched off are on as errors now. There was nothing to fix — measured first:
  zero cases across the project — and that zero was checked by compiling a deliberate violation of
  each rule, to know the checks were actually running.
- **The updater introduces itself with the version you actually have.** Asking whether a new release
  exists, it identified itself as "1.0", a number typed by hand that never existed: the declared
  version is 0.1.0. It comes from the program itself now, and a test compares it against the one
  place this project declares its version, reading the header that actually leaves rather than the
  constant in the code.
- **The tests find the project root in one place.** The same walk upwards was pasted into fifty-nine
  files, and it was not even the same walk: two of them looked for a document and the rest for the
  solution file, so the repository held two definitions of its own root. There is one now, shared,
  and a test that fails if anybody writes their own again. Eight hundred lines lighter.
- **The test that watches for unreachable screens no longer believes a comment.** It looked for the
  view's name in the files' text, so a **commented-out** reference counted as if the screen could be
  opened: the very orphan screen that test exists to find could hide behind a comment while the gate
  stayed green. Comments are stripped before matching now. Whether anything was already hiding that
  way was measured first — nothing was — and the trimming errs the safe way: cutting too much loses a
  reference and produces a loud warning, never a silent pass.
- **And the player no longer keeps one of its own either.** A third queue was left, the video
  engine's, with the same unguarded disposal: one failing release would have ended its worker and
  everything opened from then on would have leaked in silence. There is **one** queue for the whole
  process now, the one that already carries the guard. Closing the player waits for its videos to be
  let go before handing it back — that order is what keeps the native teardown from taking the
  process with it — and that wait has a ceiling, so a library busy cataloguing cannot hold up an
  exit. The one-second rest before a video is released, which is the number that stopped the
  crashes, is untouched.
- **A launch that never paints now leaves a diagnosis instead of a mute exit code.** The
  verification kills the process when the window deadline runs out, and the only thing written down
  was that kill — `exit code -1` — which says nothing about the launch. Now, **before** killing it,
  the verification records whether the process was still alive, how much processor time it had used
  across how many threads — which is what separates spinning from waiting — whether the database
  exists and how many migrations it has been through, and what the data folder holds. All of it
  lands in the same line CI prints when the phase fails. None of those reads can break anything: a
  diagnosis that fails would replace the failure it was called to explain, so whatever goes wrong is
  reported inside the sentence itself. The ninety-second deadline has not been raised, which would
  turn the only signal there is into silence.
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

- **The automated verification now presses the whole of settings with the mouse.** The settings page
  is taller than the window, and the walk that drives the built application only knew how to go down
  it: once something had been pressed, everything above it became unreachable. It now returns to the
  top of the page and scrolls only when it has to, so each press stands on its own. All twenty
  settings controls are covered by mouse: the three themes, both languages, local-folder watching,
  segment detection, the tray and closing to it, start-with-Windows — asked for, declined, asked
  again and granted — the diagnostics consent, the automatic refresh, the preview, the report export,
  the recommendations switch, its threshold and its recalculation, and restoring the shortcuts. Each
  one is checked by its real effect rather than by its checkbox: the export is read from the file on
  disk and the startup entry from the registry.
- **A copy of the application that keeps its data somewhere of its own now keeps its
  start-with-Windows entry there too.** Every copy used to write its sign-in entry to the same place,
  so an automated check could not even test that button without registering the copy it was testing
  on your machine. Your normal installation is unchanged: it still writes where Windows reads.
- **The review inbox's Search button can be pressed now, and it actually searches.** It had two
  faults stacked on each other: typing into the box **did not enable it** — it stayed off however much
  you typed — and had it been on, pressing it would have done nothing, because what it asked for was
  listened to by nobody. Now you type the title and year with a card selected from the list, and the
  application searches **for that file**: if what it finds leaves no doubt it is applied without
  asking, and if it does, it waits in the inbox for you to decide. The button is available when both
  halves are there — something typed and a card chosen — because without a card there is no file to
  search about.
- **Accept and Reject are now checked with the mouse to decide the card you chose.** That is not
  boilerplate: the automated check turned out to be able to decide a different card than the one it
  clicked, so the check now asks **which** card ends up accepted or rejected, reading it from the
  catalogue rather than from the screen. "Load more" is pressed too, and brings in the rest of the list.
- **The button that confirms a moved file sat off the side of the screen, so there was no way to press
  it.** When the application finds a file that may be one you already had, it shows you each
  candidate's path with a "Same file, reassign" button beside it. The path laid itself out across the
  row without ever wrapping, and with a real library path — yours are — it pushed the button out of
  the window, with nothing to scroll sideways to reach it: **the reassignment could not be confirmed
  at all**. The path now wraps into the space there is, and the button stays in view.
- **And when there are several candidates, which one each button confirms is now clear.** The
  application only asks you when **two** entries in your catalogue could be that file, so the button
  appears more than once; both were called the same, and choosing wrong is no detail: it decides which
  of your entries keeps its progress and your decisions under the new path. Each button now says which
  path it belongs to, for screen reader users as well. Both decisions — "same file" and "it is a new
  file" — are covered by mouse, reading from the catalogue which entry ended up decided.
- **Choosing which copy plays is now checked with the mouse to store your choice.** When you have two
  copies of one title, the comparison lets you mark the one you want; the verification presses the one
  that is **not** the copy that would play anyway, and reads the result from your catalogue rather than
  from the screen, so "the better copy" and "the copy you chose" cannot be confused.
- **Where "Watch the trailer" leads is now checked, on the film card and on the series card.** The
  button opens whatever browser you use, which is why no automated check could press it: it would
  have opened windows on the machine doing the checking. A copy that keeps its data somewhere of its
  own now **writes the address down** instead of opening it, so the verification presses the button
  on both cards and reads the exact address that would have opened — that entry's trailer and no
  other. Your normal installation still opens the browser as before, and what may be opened is
  unchanged: `https` only, and only to the site the address names.
- **The rename now renames.** It proposed the name the file already had, so the preview was always
  empty and the Rename and Undo buttons could do nothing however often they were pressed. It now
  proposes the name the entry deserves, in the convention Plex, Jellyfin and Kodi all read:
  `Title (Year).ext` for a film and `Show (Year) - SxxEyy - Title.ext` for an episode. Across the
  twelve name shapes the catalogue recognises, eight get a name different from the current one and
  the one already following the convention is left alone. When nobody has identified the entry and
  the name cannot be read with confidence, nothing is proposed: a rename does not guess. What decides
  which characters are safe, and what to do about two files that want the same name, is unchanged.
- **A watched folder no longer stops being watched exactly when a lot of files arrive.** When Windows
  reported that it had dropped changes — which is what happens when a whole season is copied in at
  once — live watching of that folder ended in silence and did not come back until the application
  was started again. No file was lost, because a full pass over the folder was run, but from then on
  anything added took until the next pass to appear. A report like that now means "sweep the whole
  folder and keep watching", the space Windows uses to report changes is asked for at the maximum it
  allows, and watching that really does fall over — an unplugged disk, a network folder that stops
  answering — starts itself again on the next pass.
- **The automated verification now presses buttons with the mouse.** The walk that drives the built
  application only ever used the keyboard, and that gap is how a pair of buttons that were on screen
  and did nothing got through. It now opens an identified film's entry, clicks "Refresh from
  provider" with the mouse and checks the entry changes — and, first, that clicking beside it changes
  nothing, so the result cannot be down to something else.
- **The editor's two provider buttons now do something.** "Refresh from provider" and "Restore
  provider fields" were visible and enabled and could not work: they waited on details only a test
  ever gave them. The refresh now works out for itself which provider entry the title is, from what
  was stored when it was identified. And when it can do nothing, it says so: an unidentified entry
  and a provider with no answer are different things, and neither is an error.
- **Saving a freshly added entry now saves.** Editing a title nobody had touched did not create its
  row, so Save was pressed and nothing happened, with no warning.
- **Two windows editing the same entry can no longer both win.** The check that stops one person
  overwriting another's work was comparing against a number that never changed, so the second window
  silently overwrote the first. The second one is now told its copy is stale, which is what should
  always have happened.
- **Identifying a film now changes what the library shows.** Until now it did not: accepting a match
  in the inbox marked the review and nothing else, so the entry went on showing whatever the parser
  pulled out of the file name. The synopsis and the trailer key existed end to end and were only ever
  filled in by hand. An identification — accepted by you, or automatic once confidence passes 90% —
  now asks the provider for the details and stores them along with whose they are and when they were
  asked for. **Anything you locked still wins**: the identification merges over what was already
  there, exactly as a manual refresh does. With no consented connection the provider serves only what
  it already holds, so a library without network permission stays exactly as it was, which is not an
  error.
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
