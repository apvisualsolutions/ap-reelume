#!/bin/sh
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Builds LibVLC for Windows without GPL third-party libraries, the way VideoLAN builds it: inside
# VideoLAN's own CI image, cross-compiling from Linux with extras/package/win32/build.sh unmodified
# except for the patches in ./patches (ENG-013).
#
# Why this route and not the MSYS2 one the 2026-09-14 spike took: seven of that spike's nine traps
# were 2026 tools refusing 2014-2018 code (gcc 16, cmake 4, yasm). VideoLAN's image pins the
# toolchain it releases 3.0.x with, and the same route cross-compiles aarch64, so ARM64 is not a
# second project. The images are the ones extras/ci/gitlab-ci.yml names at the tag.
#
# What this script does NOT decide is which plugins are GPL. contrib/bootstrap --disable-gpl only
# drops third-party libraries; VLC's configure has no GPL switch for its own modules, so some of
# them are built either way. That is read afterwards from the sources by scan-plugin-licenses.ps1
# and enforced by verify-nogpl.ps1 (ENG-027) — a hand list here would go stale the day a module
# changes licence.
#
# Usage, inside the image:  eng/libvlc/build-nogpl.sh <x86_64|aarch64> <output-dir>
# Leaves <output-dir>/install (the `make install` prefix, stripped) and <output-dir>/vlc-src (the
# patched tree the plugins were built from, which the licence scan reads).

set -eu

ARCH=${1:?usage: build-nogpl.sh <x86_64|aarch64> <output-dir>}
OUT=${2:?usage: build-nogpl.sh <x86_64|aarch64> <output-dir>}

VLC_TAG=3.0.23
VLC_COMMIT=578d28f6c9f2379164516e689418f92ac74a3445
VLC_REPO=https://github.com/videolan/vlc.git

HERE=$(cd "$(dirname "$0")" && pwd -P)
mkdir -p "$OUT"
OUT=$(cd "$OUT" && pwd -P)

case "$ARCH" in
    x86_64)  ARCH_FLAGS="" ;;
    # The flags VideoLAN's win64-arm-llvm job passes (UWP_EXTRA_BUILD_FLAGS in gitlab-ci.yml):
    # the UCRT runtime and the Windows 10 API level. -x only adds warnings as errors.
    aarch64) ARCH_FLAGS="-u -S 0x0A000006" ;;
    *) echo "unsupported architecture: $ARCH" >&2; exit 2 ;;
esac

SRC="$OUT/vlc-src"
if [ ! -d "$SRC/.git" ]; then
    git clone --depth 1 --branch "$VLC_TAG" "$VLC_REPO" "$SRC"
fi
actual=$(git -C "$SRC" rev-parse HEAD)
if [ "$actual" != "$VLC_COMMIT" ]; then
    echo "tag $VLC_TAG resolves to $actual, expected $VLC_COMMIT: refusing to build unknown code" >&2
    exit 1
fi
for patch in "$HERE"/patches/*.patch; do
    if git -C "$SRC" apply --check "$patch" 2>/dev/null; then
        git -C "$SRC" apply "$patch"
    elif git -C "$SRC" apply --reverse --check "$patch" 2>/dev/null; then
        echo "already applied: $(basename "$patch")"
    else
        echo "patch does not apply: $(basename "$patch")" >&2
        exit 1
    fi
done

# Third-party libraries. --disable-gpl refuses every contrib whose recipe says REQUIRE_GPL;
# x264 and x265 have to be deselected by name (the spike saw bootstrap list them as "manually
# deselected"). --enable-ad-clauses brings freetype2 back under its FTL licence, which the owner
# chose on 2026-09-14 (ENG-025) — without it there are no subtitles.
export CONTRIBFLAGS="--disable-gpl --disable-x264 --disable-x265 --enable-ad-clauses"

# VLC's own configure. configure.sh asks for these by name, and a named module whose library is
# missing aborts configure instead of warning:
#   faad            its library is GPL, so contrib/bootstrap no longer builds it
#   lua, realrtsp,  their own sources are GPL (lua) or the module is (realrtsp, mpc); disabling them
#   mpc             here saves building what the licence scan would drop anyway
#   update-check    VLC's own updater, which needs libgcrypt and this application does not use
# These come after configure.sh's own options, and the last one wins.
export CONFIGFLAGS="--disable-faad --disable-lua --disable-realrtsp --disable-mpc --disable-update-check"

cd "$SRC"
# -r release (optimised, no --disable-optim), -z libvlc only (no Qt, no skins, no vlc.exe),
# -o install prefix. Everything else is VideoLAN's script as published.
#
# -i none is not decoration. -r also sets INSTALLER=r, and build.sh's last step checks INSTALLER
# BEFORE the install path: with -r alone it runs `make package-win32` (the 7z/NSIS release
# packaging) and never reaches `make package-win-install`, so -o would be silently ignored. Any
# value that is not n, r or u skips the installer branches.
# shellcheck disable=SC2086
extras/package/win32/build.sh -r -i none -z -a "$ARCH" $ARCH_FLAGS -o "$OUT/install"

STRIP="$ARCH-w64-mingw32-strip"
command -v "$STRIP" >/dev/null 2>&1 || STRIP=llvm-strip
find "$OUT/install" -name '*.dll' -exec "$STRIP" --strip-unneeded {} +

# The HRTF data the headphone spatialiser reads; VideoLAN's package ships it next to the DLLs.
cp -r "$SRC/share/hrtfs" "$OUT/install/hrtfs"

echo "build-nogpl: $ARCH built from $VLC_TAG ($VLC_COMMIT) into $OUT/install"
