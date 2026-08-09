# AP Reelume privacy

AP Reelume is a local application. It works without an account, without sync, and without a connection,
and this page describes exactly what leaves the machine and under what conditions.

## What never leaves

These never leave the machine, with consent or without it:

- Folder and file paths.
- File names.
- Titles from your library, real or invented.
- Content or provider identifiers.
- Your playback history, your progress, and your personal marks.
- Your search terms.
- Any token, password, or credential.
- Your Windows user name and your machine name.

## What connections the application can make

| Destination | When | For what |
|---|---|---|
| `api.themoviedb.org` | Only when you ask to identify or refresh a title's metadata | Fetching that title's data |
| `image.tmdb.org` | Only for a title already identified | Downloading its artwork |
| `api.github.com` | Only when you press "Check for updates", or at startup if you enabled the automatic check in Settings | Asking whether a newer release exists |
| `github.com` | Only while downloading an update you confirmed; its storage may redirect to another GitHub domain | Downloading that release's package |
| `objects.githubusercontent.com` | Only as the redirect target of that same download; it is GitHub's storage | Receiving the bytes of the confirmed package |
| `*.githubusercontent.com` | Only if GitHub's storage redirects to another of its subdomains; no other domain is accepted | The same confirmed package, from another GitHub subdomain |

There is no other connection. There is no telemetry and no background report. The update check is
**off out of the box**: no installation ships with it enabled, and until you turn it on in Settings —
or press the button yourself — the application asks nobody anything. Every component that can open a
connection has its purpose declared in the code, and a test fails if one appears without it or if
this table stops matching that declaration.

Verifying a downloaded update has two layers. The published SHA-256 digests are signed with a
minisign key whose public half travels inside the binary: the updater refuses any version whose
digests do not carry that signature, so the expected digest no longer comes from the same unsigned
answer as the package it vouches for. The digest and the size then prove the downloaded bytes are
the published ones. What this signature does **not** do is authenticate the package to Windows: the
artifact still carries no Authenticode signature, SmartScreen will still warn, and whoever controlled
both the GitHub account and the signing key could publish a version the updater would accept — the
two live apart precisely so that one compromise is not enough.

## Diagnostics

Diagnostics are **off** until you turn them on in Settings. Once you do:

1. You can read the whole report on screen before saving it. What you see is exactly what would be
   saved, word for word.
2. The report is written as a file in your data folder. **It is not sent anywhere.**
3. Turning them off again clears the consent and the preview, and nothing further is produced.

The report is built from a **closed allowlist**: a field that is not on the list does not travel, even
if somebody adds it to the rest of the application later. The list is this:

- Application, Windows, and runtime versions.
- Interface language.
- Machine capabilities, in aggregate form.
- Error codes and the exception type, never its message.
- Counts as buckets: `0`, `1`, `2-5`, `6-20`, `21-100`, `100+`.

Counts travel as buckets because the exact number of items in your library is itself a fact about your
library. Exception messages are dropped whole: they are written by whoever threw the exception, and
there is no way to know in advance what they decided to include.

## Backups and export

A backup holds your local database, your preferences, the images you chose yourself, and a manifest
with the SHA-256 of all of it. It never holds videos, images downloaded from the internet, diagnostics,
or credentials. The backup stays wherever you put it: the application does not send it anywhere.

## Network credentials

NAS credentials belong to Windows. AP Reelume does not ask for them, does not store them, and owns no
credential store of its own; a test checks that the code uses none.

## Your TMDB token

If you use remote metadata, the token is read from an environment variable in your session. It is not
written to the database, it does not enter backups, and it does not appear in diagnostics.

## How this is checked

- Canaries are seeded into paths, file names, titles, identifiers, history, search terms, a token, and
  a decoy credential, and the report is searched for every one of them.
- A local canary server counts the requests it receives during operations that should make none.
- An in-process observer records every HTTP request, every connection, and every name resolution,
  before encryption.
- That same observer is checked against a real request, to show it does see what is there.

## Scope of these claims

The above describes what this process does. It is not a capture of the machine's network: if another
program on the system does something, this neither sees it nor claims to.
