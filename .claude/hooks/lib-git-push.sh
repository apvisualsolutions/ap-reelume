#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Lo que dos hooks necesitan saber de un comando: si es de verdad un `git push`, y
# a que refspec. Vive aqui desde el 2026-09-19, cuando llego el segundo hook que lo
# preguntaba (pre-push-closing.sh): dos copias de este regex habrian acabado
# diferentes, y la historia de post-push.sh es precisamente la de corregirlo dos
# veces —la cadena suelta y el espacio del heredoc—.

# El comando sin sus heredocs, que es donde viven los mensajes de commit. Se tiran
# todas las lineas entre <<DELIM y DELIM, admitiendo espacio detras de <<: sin el,
# la forma que usa este repositorio en cada commit pasaba como si no fuera un
# heredoc (medido el 2026-09-01).
strip_heredocs() {
  printf '%s\n' "$1" | awk '
    /^[[:space:]]*[A-Za-z_][A-Za-z0-9_]*[[:space:]]*$/ && skip && $1 == delim { skip = 0; next }
    skip { next }
    {
      if (match($0, /<<-?[[:space:]]*[\047"]?[A-Za-z_][A-Za-z0-9_]*[\047"]?/)) {
        d = substr($0, RSTART, RLENGTH)
        gsub(/^<<-?[[:space:]]*[\047"]?|[\047"]?$/, "", d)
        delim = d
        skip = 1
      }
      print
    }'
}

# Verdadero si el comando (ya sin heredocs) lleva `git push` en posicion de
# comando: inicio de linea, o detras de ; & | ( && o ||. Texto que solo lo cita
# —un echo, `git pushd`— no cuenta.
is_git_push() {
  printf '%s\n' "$1" | grep -Eq '(^|[;&|(]|&&|\|\|)[[:space:]]*git[[:space:]]+push([[:space:]]|$)'
}

# El primer refspec que nombra el push, o nada: se recorta desde `git push` hasta
# el final de ESE comando, se tiran banderas y comillas y el remoto, y lo que queda
# es el refspec. No lo interpreta; quien lo usa decide que significa.
push_refspec() {
  printf '%s\n' "$1" \
    | sed -n 's/.*git[[:space:]][[:space:]]*push[[:space:]]*//p' \
    | sed 's/[;&|].*//' \
    | tr -d '\042\047' \
    | tr ' \t' '\n\n' \
    | grep -v '^-' \
    | grep -v '^$' \
    | tail -n +2 \
    | head -1
}
