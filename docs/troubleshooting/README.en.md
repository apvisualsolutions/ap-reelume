# Troubleshooting

What to do when something goes wrong. The Spanish version is at [README.es.md](README.es.md).

## Windows warns when opening the download

That is expected: this release is **not signed**. The SmartScreen warning is telling the truth, which
is that Windows does not know who published the file.

Before continuing, check the hash:

```powershell
Get-FileHash .\ApReelume-0.1.0-win-x64.zip -Algorithm SHA256
```

It must match the corresponding line in `SHA256SUMS.txt`. If it does not, do not open it.
[SMARTSCREEN.en.md](../release/SMARTSCREEN.en.md) explains what else you can check.

## The MSIX will not install

Windows only installs a package signed by a certificate it trusts. This one is not, so this release's
MSIX is for inspection and archival. **Use the ZIP**: extract it and run
`ApSolutions.LocalMedia.Windows.exe`.

## I added a folder and the library is still empty

- Did you confirm the first scan? The application asks before scanning for the first time.
- Did you press **Apply** afterwards? The catalogue refreshes when you apply.
- Does the folder contain one of the recognised containers? They are `.mp4`, `.mkv`, `.avi`, `.mov`,
  `.webm`, `.m4v`, `.ts`, `.m2ts`, and `.flv`. Other formats are not catalogued.

## It says the folder cannot be added

Three possible reasons, and the screen says which:

| Message | What it means |
|---|---|
| Already in the library | The folder was already added. It is not added twice and nothing is touched. |
| Inside another, or contains one | Roots cannot overlap. Choose one that does not. |
| That path cannot be used | The folder does not exist or the path is incomplete. Type it in full. |

## A video shows as "unavailable"

Its drive is not connected. The catalogue is **not lost**: reconnect the drive and the video comes
back on its own, without duplicating. If the library moved for good, restore a backup pointing at the
new path and the paths are remapped.

## It identifies nothing

The TMDB request needs an access token in the `AP_LOCALMEDIA_TMDB_TOKEN` environment variable. **The
download carries none**, on purpose: without that deliberate act, the application opens no metadata
connection.

Without a token, identification works from whatever is already in the local cache and from what the
filename yields. You can correct any title by hand under **Review**.

## A video will not play

- The **video status** on screen says what happened. If it says the format is not supported, that
  codec is not implemented in this release.
- If the player offers **Retry** or **Open externally**, the engine failed to open the file. Opening
  it externally plays it in your default player, but exact progress is then not promised.
- A corrupt file is detected when opening and does not take the application down.

## There is picture but no sound, or the reverse

Check the **audio track** and the **audio output** in the session panel. The device is chosen by a
stable identifier, so the preference survives unplugging and plugging back in.

If your machine offers no output with more than two channels, you will only see stereo: the list
shows what the endpoint declares today, not what your hardware could do in another configuration.

## Hardware acceleration does not appear

The indicator says **"Hardware-accelerated decoding"** when it was asked for and has not fallen back,
and **"Hardware acceleration was unavailable; decoding in software"** when it has. If you see the
second, playback continues anyway: it is not a failure, it is the alternative path.

## I closed the application and lost my place

You should not have: progress is written every five seconds and additionally on pause, seek, and
close. After an unexpected shutdown it is recovered within ±5 s.

If the card does not offer to continue, check you are looking at the same content: progress follows
the content rather than the file, so a file reassigned to another title takes its progress with it.

## The application will not open and shows a recovery screen

The database could not be opened. The screen says why. The two usual cases:

- **A damaged database.** The backups folder is offered. Restore the most recent one.
- **A later release migrated this database.** You are running an older version over data a newer one
  has already updated. The application **writes nothing** in that case. Install the newer release
  again, or restore a backup taken before it.

## I want it to leave my data folder alone

Set `AP_LOCALMEDIA_DATA_ROOT` to the path you want before starting. The variable is read once at
startup and a blank value is the same as not setting it. It is also how you try the application
without touching your real data.

Redirecting `LOCALAPPDATA` does **not** work: .NET resolves that folder through a system call that
never reads the variable.

## I want to uninstall without losing my data

Delete the folder you extracted. Your data stays in `%LOCALAPPDATA%\APSolutions\LocalMedia`. Delete
it separately if that is what you want: the application never does it for you.

## Sending a diagnostic

Under **Settings → Privacy** you can turn diagnostics on. They are off by default, they are sanitised
— never carrying paths, full names, library, or history — and you can see the exact report before
deciding. **Withdrawing consent deletes the report** already written, and its folder with it.
