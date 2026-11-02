#!/bin/sh
# SPDX-FileCopyrightText: 2026 Carlos Armando Puche Martín
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# The Dolby Digital switch of the engine build: AC-3 and E-AC-3 are decoded by FFmpeg by
# default, and `off` applies optional/dolby-digital-off.patch, which builds FFmpeg without its ac3,
# ac3_fixed and eac3 decoders. It was decided on 2026-10-30 to keep them while Dolby has not
# answered, and to make dropping them a switch rather than a rewrite of the recipe under pressure.
#
# The patch sits outside patches/ so build-nogpl.sh's loop never applies it, and is applied after
# that loop because its context is patch 0004's block. Its battery is dolby-digital-switch.test.sh.
#
# Usage: dolby-digital-switch.sh <vlc-source-tree> <on|off>
# Exit 0 when the tree is as asked, 1 when it cannot be made so, 2 when the request is malformed:
# anything but on or off stops the build, because a typo must not ship the decoders silently.

set -u

SRC=${1:-}
VALUE=${2-}
PATCH="$(cd "$(dirname "$0")" && pwd -P)/optional/dolby-digital-off.patch"

if [ -z "$SRC" ] || [ $# -ne 2 ]; then
    echo "usage: dolby-digital-switch.sh <vlc-source-tree> <on|off>" >&2
    exit 2
fi

applied() { git -C "$SRC" apply --reverse --check "$PATCH" 2>/dev/null; }

case "$VALUE" in
    on)
        # A tree reused from an earlier `off` build still lacks the decoders; saying "inside" over
        # it would be the false record this switch exists to prevent.
        if applied; then
            echo "dolby-digital-switch: the tree already leaves Dolby Digital out; build from a fresh tree" >&2
            exit 1
        fi
        echo "build-nogpl: Dolby Digital (AC-3, E-AC-3) decoders inside" ;;
    off)
        if ! applied; then
            if ! git -C "$SRC" apply --check "$PATCH" 2>/dev/null; then
                echo "patch does not apply: $(basename "$PATCH")" >&2
                exit 1
            fi
            git -C "$SRC" apply "$PATCH"
        fi
        echo "build-nogpl: Dolby Digital (AC-3, E-AC-3) decoders left out" ;;
    *)
        echo "DOLBY_DIGITAL must be on or off, not '$VALUE'" >&2
        exit 2 ;;
esac
