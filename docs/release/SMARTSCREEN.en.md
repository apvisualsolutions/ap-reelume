# SmartScreen and this download

AP Reelume is distributed **unsigned**. There is no certificate behind this artifact, neither a test
one nor a purchased one. This document explains what you will see when you install it, why, and what
you can check for yourself instead of trusting a signature that does not exist.

The Spanish version is at [SMARTSCREEN.es.md](SMARTSCREEN.es.md).

## What you will see

The first time you open the MSIX or the executable from the ZIP, Windows will show a
**Microsoft Defender SmartScreen** warning: "Windows protected your PC" or "Unknown publisher". The
button that continues is usually behind "More info".

That warning is correct. It is not a false positive and not an error to work around: Windows is
saying exactly the truth, which is that **it does not know who published this file**.

## Why it is not signed

A code-signing certificate is issued by a commercial authority and costs an annual fee. This project
is free and has no revenue, so there is none. A new certificate would not remove the warning
immediately either: SmartScreen builds reputation over time and over download counts, so a
freshly-issued signature still warns for a while.

What this project will **not** do is imply that it is signed. The package states its own condition:
the artifact report carries `"signed": false`, and a suite fails if it ever said otherwise.

## What you can check instead of the signature

A signature answers "who published this?". Without one there are two questions you can answer
yourself, and together they cover nearly the same ground.

### 1. That the file is the one that was published

Every release includes `SHA256SUMS.txt`. Compare the hash of what you downloaded:

```powershell
Get-FileHash .\APSolutions.LocalMedia_0.1.0_x64.msix -Algorithm SHA256
```

The result must match, ignoring case, the corresponding line in `SHA256SUMS.txt`. If it does not, the
file is not the published one and you should not open it.

`SHA256SUMS.txt` is itself signed: every release includes `SHA256SUMS.txt.minisig`, a
[minisign](https://jedisct1.github.io/minisign/) signature made with the project's key, whose public
half lives in the repository (`eng/release-signing.pub`) and inside the binary. With minisign
installed:

```powershell
minisign -Vm SHA256SUMS.txt -p release-signing.pub
```

The built-in updater runs this check on every update by itself; doing it by hand is only needed when
you download the files yourself.

### 2. That what was published corresponds to the source

Builds are reproducible. Two builds of the same commit, from two clean copies of the repository in
two different directories, produce the same contents file by file. You can check this yourself:

```powershell
pwsh ./eng/package-x64.ps1
pwsh ./eng/verify-package.ps1 -Mode Verify
```

`artifacts/package/reproducibility.json` records the comparison. The MSIX container itself is **not**
identical between builds — a package records the moment it was sealed — but everything inside it is.

## What the package contains

- The SBOM travels inside the artifact, under `sbom/`, in CycloneDX and SPDX formats.
- The GPL-3.0-or-later licence and the third-party notices travel in `LICENSE`, `NOTICE`, and
  `licenses/`.
- The package declares **no** capability beyond `runFullTrust`, which any desktop application needs.
  It asks for no network, no location, and no access to system libraries.
- The package carries **no** access token. Remote identification works only if you place one by hand
  in `AP_LOCALMEDIA_TMDB_TOKEN`; without that deliberate act, the application opens no metadata
  connection. The update checker, off out of the box, is the other possible connection; the complete
  table is in the privacy statement. Verifying an update (SHA-256 and size) proves the download was
  not altered in transit; authenticity rests on the GitHub account that publishes the releases,
  because the artifact is not signed.

## Installing

**System requirement.** The application targets **Windows 11** (22H2, build 22621, or later), and
the package declares that as its minimum. This is a deliberate decision, not an oversight:
Windows 10 is not a target of this application.

**MSIX.** Windows requires a package to be signed by a certificate it trusts. Unsigned, this
release's MSIX is for inspection and archival rather than for double-click installation. Use the ZIP
path.

**ZIP.** Extract it wherever you like and run `ApSolutions.LocalMedia.Windows.exe`. It needs no
installation, no administrator rights, and writes nothing to the registry. Data goes to
`%LOCALAPPDATA%\APSolutions\LocalMedia` unless you name another folder with `AP_LOCALMEDIA_DATA_ROOT`.

To uninstall, delete the folder you extracted. Your data stays where it was; delete it separately if
that is what you want.

## What we do not do

- We do not ask you to disable SmartScreen, Defender, or any other protection.
- We do not publish instructions for bypassing the warning beyond what Windows already offers.
- We do not claim anywhere, in the interface or in this documentation, that the application is signed
  or verified by Microsoft.
