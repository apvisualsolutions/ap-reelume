#!/bin/sh
# SPDX-FileCopyrightText: 2026 Carlos Armando Puche Martín
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Builds LibVLC for Windows without GPL third-party libraries, the way VideoLAN builds it: inside
# VideoLAN's own CI image, cross-compiling from Linux with extras/package/win32/build.sh unmodified
# except for the patches in ./patches.
#
# Why this route and not the MSYS2 one the 2026-09-14 spike took: seven of that spike's nine traps
# were 2026 tools refusing 2014-2018 code (gcc 16, cmake 4, yasm). VideoLAN's image pins the
# toolchain it releases 3.0.x with, and the same route cross-compiles aarch64, so ARM64 is not a
# second project. The images are the ones extras/ci/gitlab-ci.yml names at the tag.
#
# What this script does NOT decide is which plugins are GPL. contrib/bootstrap --disable-gpl only
# drops third-party libraries; VLC's configure has no GPL switch for its own modules, so some of
# them are built either way. That is read afterwards from the sources by scan-plugin-licenses.ps1
# and enforced by verify-nogpl.ps1 — a hand list here would go stale the day a module
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
# deselected"). --enable-ad-clauses brings freetype2 back under its FTL licence, the one chosen for
# FreeType on 2026-09-14 — without it there are no subtitles.
#
# --disable-zvbi is the same kind of deselection, and --disable-gpl cannot make it: libzvbi 0.2.35
# carries two GPL-2.0-only files, src/packet-830.c and src/pdc.c, and contrib/src/zvbi/rules.mak
# does not say REQUIRE_GPL. Nothing downstream would notice either: the licence scan reads VLC's
# own zvbi module, which is LGPL, and the GPL code arrives inside the library it links. No other
# contrib depends on zvbi, so deselecting it cannot bring it back as a dependency.
export CONTRIBFLAGS="--disable-gpl --disable-x264 --disable-x265 --enable-ad-clauses --disable-zvbi"

# VLC's own configure. configure.sh asks for these by name, and a named module whose library is
# missing aborts configure instead of warning:
#   faad            its library is GPL, so contrib/bootstrap no longer builds it
#   lua, realrtsp,  their own sources are GPL (lua) or the module is (realrtsp, mpc); disabling them
#   mpc             here saves building what the licence scan would drop anyway
#   update-check    VLC's own updater, which needs libgcrypt and this application does not use
#   zvbi            configure.sh passes --enable-zvbi; without the contrib above configure only
#                   warns, and naming it keeps out a zvbi-0.2 that pkg-config finds some other way
#   telx            the teletext decoder VLC offers when zvbi is absent. configure.sh already
#                   disables it, and it is named here so that stays true if that script changes:
#                   modules/codec/telx.c declares LGPL but says some of its code was converted from
#                   the ProjectX DVB decoder, which is GPL. No teletext decoder is built at all
# These come after configure.sh's own options, and the last one wins.
export CONFIGFLAGS="--disable-faad --disable-lua --disable-realrtsp --disable-mpc --disable-update-check --disable-zvbi --disable-telx"

cd "$SRC"

# Fetch the contrib sources first, with retries. build.sh runs `make -j fetch` once and gives up on
# the first failed download, and the first run of this workflow (2026-09-18) died on exactly that:
# a SourceForge mirror that did not resolve. VideoLAN's own CI rarely fetches at all — it uses
# prebuilt contribs, which here are useless because they are built with GPL on. The tarballs land
# in contrib/tarballs, which is where build.sh's own fetch looks, so it finds them and only checks
# their sums. The folder is not named contrib-* on purpose: verify-nogpl.ps1 expects exactly one
# of those, the one build.sh configures.
#
# The retries were not the cure. The second run failed five times in a row on the same host:
# contrib/src/main.mak pins SourceForge to one mirror, `SF := https://netcologne.dl.sourceforge.net/`,
# and that name does not resolve from GitHub's runners. Twelve contribs download through $(SF),
# mingw-w64 (winpthreads, x86_64 only) among them. SourceForge's own entry point redirects to a
# mirror that answers; a command-line variable overrides the makefile's, and the contrib's
# SHA512SUMS still checks every tarball, so the bytes cannot change with the mirror. Measured the
# same day: mingw-w64-v10.0.0.tar.bz2 from that URL has the SHA-512 the contrib pins.
SF_ENTRY=https://downloads.sourceforge.net/project/
mkdir -p contrib/prefetch
(
    cd contrib/prefetch
    ../bootstrap --host="$ARCH-w64-mingw32" $CONTRIBFLAGS >/dev/null
    for attempt in 1 2 3 4 5; do
        if make -j4 fetch SF="$SF_ENTRY"; then exit 0; fi
        echo "contrib fetch failed (attempt $attempt), retrying in 30 s" >&2
        sleep 30
    done
    exit 1
)

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
