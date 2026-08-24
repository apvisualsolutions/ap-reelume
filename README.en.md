# AP Reelume

A local video library and its player, for Windows 11 x64. It catalogues videos **where they are**,
identifies them, plays them, and remembers where you left off. No account, no subscription, and
nothing sent anywhere.

*Lea esto en [español](README.es.md).*

![The home screen: what was left part-way through, what is in progress, and what arrived recently](docs/assets/home.png)

**Windows 11 x64** · **GPL-3.0-or-later** · [Download the latest release](https://github.com/apvisualsolutions/ap-reelume/releases/latest)

Every commit that reaches `main` has been through [the full
verification](https://github.com/apvisualsolutions/ap-reelume/actions/workflows/ci.yml) on a hosted
runner: format, build, the whole test suite, the accessibility and recovery gates twice over,
packaging, and the coverage floors. There is no status badge above on purpose — the workflow runs on
the working branch and `main` receives that same commit by fast-forward, so a badge pointing at
`main` would freeze on whatever it last saw and go on saying it.

## What it is

One person, one PC, their files. AP Reelume reads the folders you point it at — local, USB, or NAS
over UNC — and builds a catalogue from them. It **never copies or moves a video** in order to
catalogue it.

- **Library.** Initial, startup, manual, and incremental scanning, cancellable and resumable, across
  10,000 files without blocking the interface.
- **Identification.** It detects movie, show, season, and episode from names and folders, and asks
  TMDB in Spanish with a fallback language. Anything ambiguous goes to a review inbox rather than
  classifying itself.
- **Playback.** Embedded LibVLC, with external opening as a fallback. The usual containers and
  codecs, H.264, HEVC, and AV1 included; HDR10 with SDR tone mapping when the display cannot take it.
- **Continuity.** It saves progress every five seconds and on pause, seek, and close; it resumes
  within ±5 s even after an unexpected shutdown.
- **Yours.** Favourites, watch later, a personal rating, and local recommendations that explain
  themselves and can be turned off.

## What it looks like

The library in these captures is **seeded and fictional**, and that is a rule rather than a
convenience: a screenshot of a real library carries somebody's titles and somebody's paths inside a
PNG that no test can read, so the captures are taken from a run with its own data root over invented
films. They are taken by a script against the built application, which is why they can be retaken in
the commit that changes a view.

![The library: identified titles with their year, files still waiting to be identified by their file name, and one marked unavailable](docs/assets/library.png)

A series card: the season picker, the episodes underneath, and the personal actions that belong to
the whole series.

![A series card, with its season picker, its two episodes and the personal actions](docs/assets/show.png)

Playback, with the track column open. The scrubber says where the session is; closing the window and
opening it again resumes within five seconds of it.

![The player: video, the transport with its scrubber and clocks, and the audio and subtitle column](docs/assets/player.png)

What the identification could not settle waits here rather than deciding for itself, and every
proposal says why it was proposed.

![The review inbox: three proposed candidates, each with its confidence and the reasons behind it](docs/assets/review.png)

## What it is not

There are no accounts, no sync, and no cloud. It does not transcode or edit video. It does not play
several at once. The full list, with identifiers, is in the
[roadmap](docs/roadmap/README.en.md).

## Privacy

Zero telemetry without consent, and consent is reversible. Remote identification works only if you
place a TMDB token by hand in `AP_LOCALMEDIA_TMDB_TOKEN`: **the artifact carries none**, so without
that deliberate act the application opens no metadata connection. The update checker is the other
possible connection, also under your control and off out of the box; the complete table of
destinations is in the privacy statement. Diagnostics are opt-in and sanitised, and
withdrawing consent deletes the report.

The detail is in the [privacy statement](docs/privacy/PRIVACY.en.md).

## Installing

Download the release ZIP, extract it wherever you like, and run
`ApSolutions.LocalMedia.Windows.exe`. It needs no installation, no administrator rights, and writes
nothing to the registry.

**Windows will show a SmartScreen warning.** That is correct: this release is **not signed**, and we
do not claim otherwise. What to check instead — the published hash and the reproducible build — is in
[SMARTSCREEN.en.md](docs/release/SMARTSCREEN.en.md).

Your data goes to `%LOCALAPPDATA%\APSolutions\LocalMedia` unless you name another folder with
`AP_LOCALMEDIA_DATA_ROOT`. To uninstall, delete the folder you extracted; your data stays where it
was.

## Documentation

| | |
|---|---|
| [User guide](docs/user-guide/README.en.md) | How to do each thing |
| [Troubleshooting](docs/troubleshooting/README.en.md) | What to do when something goes wrong |
| [Roadmap](docs/roadmap/README.en.md) | What is coming and what will not be done |
| [Feature matrix](docs/FEATURES.md) | The canonical scope record |
| [Privacy](docs/privacy/PRIVACY.en.md) | What is stored and what never leaves |
| [Legal status](docs/legal/LEGAL.en.md) | Licence, third parties, and what stays open |
| [Changelog](docs/CHANGELOG.en.md) | What changed in each release |
| [Development guide](docs/development/README.en.md) | How to build and verify |
| [Releasing](docs/release/RELEASING.en.md) | How a release is cut |
| [Decisions](docs/adr) | Why the project is the way it is |

## Licence

GPL-3.0-or-later. See [LICENSE](LICENSE), [NOTICE](NOTICE), and the
[third-party notices](docs/release/THIRD-PARTY-NOTICES.en.md). This product uses TMDB and the TMDB
APIs but is not endorsed, certified, or otherwise approved by TMDB.

The program comes with **no warranty whatsoever**, to the extent permitted by applicable law: see
sections 15 to 17 of the [licence](LICENSE). The legal limits that remain open — among them the
professional opinion under `REL-004` — are named in [the legal status](docs/legal/LEGAL.en.md).
