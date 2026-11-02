#!/bin/sh
# SPDX-FileCopyrightText: 2026 Carlos Armando Puche Martín
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Battery for dolby-digital-switch.sh, against throwaway trees that carry only the FFmpeg
# recipe the optional patch edits: the block patch 0004 leaves, written here byte for byte.
# Cases that must pass the switch sit beside cases that must stop it, so a script that always says
# yes and a script that always says no both fail. Run: sh eng/libvlc/dolby-digital-switch.test.sh

set -u

HERE=$(cd "$(dirname "$0")" && pwd -P)
SWITCH="$HERE/dolby-digital-switch.sh"
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

failures=0
cases=0

# The recipe as patch 0004 leaves it, from the line the optional patch's context starts at.
tree() {
    dir="$WORK/$1"
    mkdir -p "$dir/contrib/src/ffmpeg"
    {
        printf '%s\n' 'FFMPEGCONF = \'
        printf '\t%s\n' '--disable-libvpx \' '--disable-avresample \' '--enable-bsf=vp9_superframe'
        printf '\n'
        if [ "$2" = with-0004 ]; then
            printf '%s\n' \
                '# Modified by AP Solutions on 2026-10-24: FFmpeg is built without its H.264, HEVC and AAC' \
                '# decoders and its AAC encoder, because those formats are under patents that are still' \
                '# licensed; VLC decodes them through Media Foundation (the mft plugin) instead.' \
                'FFMPEGCONF += --disable-decoder=h264,hevc,aac,aac_fixed,aac_latm --disable-encoder=aac'
            printf '\n'
        fi
        printf '%s\n' 'ifdef USE_FFMPEG' 'FFMPEGCONF += \'
        printf '\t%s\n' '--disable-swresample \' '--disable-iconv'
        printf '%s\n' 'endif'
    } > "$dir/contrib/src/ffmpeg/rules.mak"
    git -C "$dir" init -q
    echo "$dir"
}

# expect <name> <exit code> <ac3 line expected: yes|no> <tree> <value...>
expect() {
    name=$1; code=$2; dropped=$3; dir=$4; shift 4
    cases=$((cases + 1))
    sh "$SWITCH" "$dir" "$@" > "$WORK/out" 2>&1
    actual=$?
    has=no
    grep -q 'disable-decoder=ac3,ac3_fixed,eac3' "$dir/contrib/src/ffmpeg/rules.mak" && has=yes
    if [ "$actual" -ne "$code" ] || [ "$has" != "$dropped" ]; then
        failures=$((failures + 1))
        echo "FAIL $name: exit $actual (want $code), decoders dropped $has (want $dropped)"
        sed 's/^/    /' "$WORK/out"
    else
        echo "pass $name"
    fi
}

# Must pass.
expect 'on leaves the recipe alone' 0 no "$(tree on with-0004)" on
expect 'off drops the three decoders' 0 yes "$(tree off with-0004)" off
t=$(tree twice with-0004)
sh "$SWITCH" "$t" off > /dev/null 2>&1
expect 'off on a tree already switched off is a no-op' 0 yes "$t" off

# Must stop.
t=$(tree stale with-0004)
sh "$SWITCH" "$t" off > /dev/null 2>&1
expect 'on over a tree left switched off refuses' 1 yes "$t" on
expect 'a value that is neither on nor off refuses' 2 no "$(tree typo with-0004)" OFF
expect 'an empty value refuses' 2 no "$(tree empty with-0004)" ''
expect 'off without patch 0004 underneath refuses' 1 no "$(tree bare without-0004)" off
expect 'no value at all refuses' 2 no "$(tree none with-0004)"

# The switch must say which way it went: the build log is the only record of it.
out=$(sh "$SWITCH" "$(tree say-on with-0004)" on 2>&1)
cases=$((cases + 1))
case "$out" in *inside*) echo 'pass on says inside' ;; *) failures=$((failures + 1)); echo "FAIL on says inside: $out" ;; esac
out=$(sh "$SWITCH" "$(tree say-off with-0004)" off 2>&1)
cases=$((cases + 1))
case "$out" in *'left out'*) echo 'pass off says left out' ;; *) failures=$((failures + 1)); echo "FAIL off says left out: $out" ;; esac

echo "dolby-digital-switch: $((cases - failures)) of $cases"
[ "$failures" -eq 0 ]
